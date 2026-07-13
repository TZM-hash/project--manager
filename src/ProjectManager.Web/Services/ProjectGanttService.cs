using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class ProjectGanttService(
    ApplicationDbContext db,
    SystemSettingsService systemSettingsService)
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] BlockedKeywords =
        ["阻塞", "卡住", "缺少", "無法", "无法", "blocked"];

    private static readonly string[] WaitingKeywords =
        ["等待", "待確認", "待确认", "待回覆", "待回复", "pending"];

    public async Task<ProjectGanttInputModel> BuildInputAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(x => x.GanttPlan)
            .ThenInclude(x => x!.Tasks)
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Id == projectId, cancellationToken);

        if (project is null)
        {
            return new ProjectGanttInputModel();
        }

        return ToInput(project);
    }

    public async Task<IReadOnlyList<string>> SaveAsync(
        int projectId,
        ProjectGanttInputModel input,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var errors = Validate(input);
        if (errors.Count > 0)
        {
            return errors;
        }

        var projectExists = await db.Projects
            .AnyAsync(x => !x.IsDeleted && x.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            return ["Project was not found."];
        }

        var plan = await db.ProjectGanttPlans
            .Include(x => x.Tasks)
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (plan is null)
        {
            plan = new ProjectGanttPlan
            {
                ProjectId = projectId,
                CreatedAt = now
            };
            db.ProjectGanttPlans.Add(plan);
        }

        plan.StartDate = input.StartDate;
        plan.FinishDate = input.FinishDate;
        plan.ProgressNote = EmptyToNull(input.ProgressNote);
        plan.UpdatedByUserId = currentUserId;
        plan.UpdatedAt = now;

        db.ProjectGanttTasks.RemoveRange(plan.Tasks);
        var sortOrder = 1;
        foreach (var row in input.Tasks.Where(HasTaskData))
        {
            plan.Tasks.Add(new ProjectGanttTask
            {
                SortOrder = sortOrder++,
                Name = RequiredOrDefault(row.Name, $"工作 {sortOrder - 1}"),
                PlannedStartDate = row.PlannedStartDate,
                PlannedFinishDate = row.PlannedFinishDate,
                ProgressPercent = ClampPercent(row.ProgressPercent),
                ProgressDescription = EmptyToNull(row.ProgressDescription)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return [];
    }

    public async Task<ExportFile> ExportAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(x => x.Status)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.User)
            .Include(x => x.UpdatedByUser)
            .Include(x => x.GanttPlan)
            .ThenInclude(x => x!.Tasks)
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new InvalidOperationException("Project was not found.");
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Gantt");
        var archiveDate = await systemSettingsService.GetArchiveDateAsync(cancellationToken);
        WriteGanttWorksheet(sheet, project, archiveDate);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExportFile(
            $"gantt-{project.ProjectNumber}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    public static IReadOnlyList<GanttMonth> BuildMonths(
        ProjectGanttInputModel input,
        DateOnly? markerDate = null)
    {
        var dates = new List<DateOnly>();
        if (input.StartDate is not null)
        {
            dates.Add(input.StartDate.Value);
        }

        if (input.FinishDate is not null)
        {
            dates.Add(input.FinishDate.Value);
        }

        foreach (var task in input.Tasks)
        {
            if (task.PlannedStartDate is not null)
            {
                dates.Add(task.PlannedStartDate.Value);
            }

            if (task.PlannedFinishDate is not null)
            {
                dates.Add(task.PlannedFinishDate.Value);
            }
        }

        if (dates.Count == 0)
        {
            var anchor = markerDate ?? DateOnly.FromDateTime(DateTime.Today);
            dates.Add(anchor);
            dates.Add(anchor.AddMonths(1));
        }

        var start = dates.Min();
        var finish = dates.Max();
        if (finish < start)
        {
            (start, finish) = (finish, start);
        }

        var totalDays = finish.DayNumber - start.DayNumber + 1;
        var unit = totalDays switch
        {
            <= 21 => GanttTimeUnit.Day,
            <= 126 => GanttTimeUnit.Week,
            <= 548 => GanttTimeUnit.Month,
            <= 1644 => GanttTimeUnit.Quarter,
            _ => GanttTimeUnit.Year
        };
        var stepYears = unit == GanttTimeUnit.Year
            ? Math.Max(1, (int)Math.Ceiling((finish.Year - start.Year + 1) / 18m))
            : 1;
        start = AlignBucketStart(start, unit, stepYears);
        finish = AlignBucketEnd(finish, unit, stepYears);
        var months = new List<GanttMonth>();
        for (var bucketStart = start; bucketStart <= finish;)
        {
            var bucketEnd = GetBucketEnd(bucketStart, unit, stepYears);
            months.Add(new GanttMonth(
                bucketStart,
                bucketEnd,
                BuildBucketLabel(bucketStart, bucketEnd, unit),
                ResolveStepMonths(unit, stepYears),
                unit));
            bucketStart = bucketEnd.AddDays(1);
        }

        return months;
    }

    public static string GetTimelineScaleText(IReadOnlyList<GanttMonth> months)
    {
        if (months.Count == 0)
        {
            return "自动";
        }

        var bucket = months[0];
        return bucket.Unit switch
        {
            GanttTimeUnit.Day => "每 1 日 / 格",
            GanttTimeUnit.Week => "每 1 週 / 格",
            GanttTimeUnit.Month => "每 1 月 / 格",
            GanttTimeUnit.Quarter => "每 1 季 / 格",
            _ when bucket.StepMonths > 12 => $"每 {bucket.StepMonths / 12} 年 / 格",
            _ => "每 1 年 / 格"
        };
    }

    public static ArchiveDemandInfo BuildArchiveDemandInfo(Project project, DateOnly archiveDate)
    {
        var person = project.UpdatedByUser?.DisplayName ?? project.UpdatedByUser?.UserName;
        if (string.IsNullOrWhiteSpace(person))
        {
            person = string.Join("、", project.Assignments
                .Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId)
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return new ArchiveDemandInfo(archiveDate, string.IsNullOrWhiteSpace(person) ? "-" : person);
    }

    public static decimal GetTimelinePositionPercent(
        DateOnly date,
        IReadOnlyList<GanttMonth> months)
    {
        if (months.Count == 0)
        {
            return 0;
        }

        var timelineStart = months[0].Month;
        var timelineFinish = months[^1].EndMonth;
        var totalDays = Math.Max(1, timelineFinish.DayNumber - timelineStart.DayNumber + 1);
        var offsetDays = date.DayNumber - timelineStart.DayNumber;
        return Math.Clamp(offsetDays / (decimal)totalDays * 100m, 0m, 100m);
    }

    public static IReadOnlyList<decimal> BuildTaskWeights(int taskCount)
    {
        if (taskCount <= 0)
        {
            return [];
        }

        if (taskCount == 10)
        {
            return [5m, 5m, 5m, 10m, 5m, 10m, 15m, 15m, 20m, 10m];
        }

        if (taskCount == 11)
        {
            return [5m, 5m, 5m, 5m, 5m, 10m, 10m, 10m, 10m, 20m, 15m];
        }

        var equalWeight = Math.Floor(10000m / taskCount) / 100m;
        var weights = Enumerable.Repeat(equalWeight, taskCount).ToList();
        weights[^1] += 100m - weights.Sum();
        return weights;
    }

    public static decimal CalculateExpectedProgress(
        IReadOnlyList<ProjectGanttTaskInputModel> tasks,
        IReadOnlyList<decimal> weights,
        DateOnly asOfDate)
    {
        decimal result = 0;
        for (var i = 0; i < tasks.Count && i < weights.Count; i++)
        {
            var task = tasks[i];
            if (task.PlannedStartDate is null || task.PlannedFinishDate is null)
            {
                continue;
            }

            var start = task.PlannedStartDate.Value;
            var finish = task.PlannedFinishDate.Value;
            if (finish < start)
            {
                (start, finish) = (finish, start);
            }

            decimal completion;
            if (asOfDate < start)
            {
                completion = 0;
            }
            else if (asOfDate >= finish)
            {
                completion = 1;
            }
            else
            {
                var totalDays = Math.Max(1, finish.DayNumber - start.DayNumber + 1);
                completion = (asOfDate.DayNumber - start.DayNumber + 1m) / totalDays;
            }

            result += weights[i] * completion;
        }

        return Math.Clamp(result, 0m, 100m);
    }

    public static decimal CalculateActualProgress(
        IReadOnlyList<ProjectGanttTaskInputModel> tasks,
        IReadOnlyList<decimal> weights)
    {
        decimal result = 0;
        for (var i = 0; i < tasks.Count && i < weights.Count; i++)
        {
            result += weights[i] * ClampPercent(tasks[i].ProgressPercent) / 100m;
        }

        return Math.Clamp(result, 0m, 100m);
    }

    public static GanttBar BuildBar(ProjectGanttTaskInputModel task, IReadOnlyList<GanttMonth> months)
    {
        if (task.PlannedStartDate is null || task.PlannedFinishDate is null || months.Count == 0)
        {
            return new GanttBar(0, 0, 0);
        }

        var startDate = task.PlannedStartDate.Value;
        var finishDate = task.PlannedFinishDate.Value;
        if (finishDate < startDate)
        {
            (startDate, finishDate) = (finishDate, startDate);
        }

        var left = GetTimelinePositionPercent(startDate, months);
        var finish = GetTimelinePositionPercent(finishDate.AddDays(1), months);
        var width = Math.Max(0.35m, finish - left);
        return new GanttBar(left, width, ClampPercent(task.ProgressPercent));
    }

    public static GanttTaskVisualState GetTaskVisualState(
        ProjectGanttTaskInputModel task,
        DateOnly archiveDate,
        GanttProgressState progressState)
    {
        var progress = ClampPercent(task.ProgressPercent);
        if (progress >= 100m)
        {
            return GanttTaskVisualState.Completed;
        }

        var description = task.ProgressDescription ?? string.Empty;
        if (ContainsAny(description, BlockedKeywords))
        {
            return GanttTaskVisualState.Blocked;
        }

        if (ContainsAny(description, WaitingKeywords))
        {
            return GanttTaskVisualState.Waiting;
        }

        if (progress <= 0m
            && task.PlannedStartDate is { } plannedStart
            && plannedStart > archiveDate)
        {
            return GanttTaskVisualState.NotStarted;
        }

        if (task.PlannedFinishDate is { } plannedFinish
            && plannedFinish < archiveDate)
        {
            return GanttTaskVisualState.AtRisk;
        }

        return progressState switch
        {
            GanttProgressState.Ahead => GanttTaskVisualState.Ahead,
            GanttProgressState.Behind => GanttTaskVisualState.AtRisk,
            _ => progress <= 0m
                ? GanttTaskVisualState.NotStarted
                : GanttTaskVisualState.InProgress
        };
    }

    public static string GetTaskVisualStateLabel(GanttTaskVisualState state)
    {
        return state switch
        {
            GanttTaskVisualState.Completed => "已完成",
            GanttTaskVisualState.Ahead => "進度超前",
            GanttTaskVisualState.AtRisk => "有風險",
            GanttTaskVisualState.Waiting => "等待中",
            GanttTaskVisualState.Blocked => "阻塞",
            GanttTaskVisualState.NotStarted => "未開始",
            _ => "進行中"
        };
    }

    public static string GetTaskVisualStateCssClass(GanttTaskVisualState state)
    {
        return state switch
        {
            GanttTaskVisualState.Completed => "gantt-task-state-completed",
            GanttTaskVisualState.Ahead => "gantt-task-state-ahead",
            GanttTaskVisualState.AtRisk => "gantt-task-state-at-risk",
            GanttTaskVisualState.Waiting => "gantt-task-state-waiting",
            GanttTaskVisualState.Blocked => "gantt-task-state-blocked",
            GanttTaskVisualState.NotStarted => "gantt-task-state-not-started",
            _ => "gantt-task-state-in-progress"
        };
    }

    public static IReadOnlyList<GanttProgressPoint> BuildProgressLinePoints(
        IReadOnlyList<ProjectGanttTaskInputModel> tasks,
        DateOnly archiveDate,
        IReadOnlyList<GanttMonth> months)
    {
        var baseline = GetTimelinePositionPercent(archiveDate, months);
        return tasks.Select(task =>
        {
            if (task.PlannedStartDate is null || task.PlannedFinishDate is null)
            {
                return new GanttProgressPoint(baseline, GanttProgressState.OnSchedule, 0);
            }

            var start = task.PlannedStartDate.Value;
            var finish = task.PlannedFinishDate.Value;
            if (finish < start)
            {
                (start, finish) = (finish, start);
            }

            var expected = CalculateTaskExpectedPercent(start, finish, archiveDate);
            var actual = ClampPercent(task.ProgressPercent);
            if (Math.Abs(actual - expected) < 0.5m)
            {
                return new GanttProgressPoint(baseline, GanttProgressState.OnSchedule, expected);
            }

            var duration = Math.Max(0, finish.DayNumber - start.DayNumber);
            var actualOffset = (int)Math.Round(duration * actual / 100m, MidpointRounding.AwayFromZero);
            var actualDate = start.AddDays(actualOffset);
            var state = actual < expected ? GanttProgressState.Behind : GanttProgressState.Ahead;
            return new GanttProgressPoint(GetTimelinePositionPercent(actualDate, months), state, expected);
        }).ToList();
    }

    private static List<string> Validate(ProjectGanttInputModel input)
    {
        var errors = new List<string>();
        if (input.StartDate is not null &&
            input.FinishDate is not null &&
            input.FinishDate < input.StartDate)
        {
            errors.Add("整体完成日不能早于整体開始日。");
        }

        foreach (var (task, index) in input.Tasks.Select((task, index) => (task, index + 1)))
        {
            if (!HasTaskData(task))
            {
                continue;
            }

            if (task.PlannedStartDate is not null &&
                task.PlannedFinishDate is not null &&
                task.PlannedFinishDate < task.PlannedStartDate)
            {
                errors.Add($"第 {index} 项细分工作的预计完成日不能早于预计開始日。");
            }

            if (task.ProgressPercent < 0 || task.ProgressPercent > 100)
            {
                errors.Add($"第 {index} 项细分工作的当前進度必须在 0 到 100 之间。");
            }
        }

        return errors;
    }

    private static void WriteGanttWorksheet(
        IXLWorksheet sheet,
        Project project,
        DateOnly archiveDate)
    {
        var input = ToInput(project);
        var archiveDemand = BuildArchiveDemandInfo(project, archiveDate);
        var months = BuildMonths(input, archiveDemand.Date);
        var tasks = input.Tasks.Where(HasTaskData).OrderBy(x => x.SortOrder).ToList();
        var weights = BuildTaskWeights(tasks.Count);

        const int infoTopRow = 1;
        const int yearRow = 4;
        const int monthRow = 5;
        const int firstTaskRow = 6;
        const int leftColumns = 3;
        var monthStartColumn = leftColumns + 1;
        var ratioColumn = monthStartColumn + months.Count;
        var actualColumn = ratioColumn + 1;
        var lastColumn = actualColumn;
        var visibleTaskCount = Math.Max(tasks.Count, 1);
        var lastTaskRow = firstTaskRow + visibleTaskCount - 1;
        var noteTopRow = lastTaskRow + 1;
        var noteBottomRow = noteTopRow + 6;

        sheet.Style.Font.FontName = "Microsoft YaHei";
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        WriteGanttProjectInfo(sheet, project, archiveDemand, infoTopRow, monthStartColumn, lastColumn);
        WriteGanttTimeHeader(sheet, months, yearRow, monthRow, monthStartColumn, ratioColumn, actualColumn);
        WriteGanttBody(sheet, tasks, weights, months, firstTaskRow, monthStartColumn, ratioColumn, actualColumn);
        PaintArchiveProgressLine(
            sheet,
            archiveDemand.Date,
            tasks,
            months,
            monthRow,
            firstTaskRow,
            lastTaskRow,
            monthStartColumn);
        WriteGanttFooter(sheet, project, archiveDemand.Date, noteTopRow, noteBottomRow, monthStartColumn, ratioColumn, actualColumn, tasks, weights);

        var usedRange = sheet.Range(1, 1, noteBottomRow, lastColumn);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.Column(1).Width = 5;
        sheet.Column(2).Width = 30;
        sheet.Column(3).Width = 5;
        for (var column = monthStartColumn; column < ratioColumn; column++)
        {
            sheet.Column(column).Width = 7.5;
        }

        sheet.Column(ratioColumn).Width = 10;
        sheet.Column(actualColumn).Width = 13;
        sheet.Rows(firstTaskRow, lastTaskRow).Height = 28;
        sheet.Rows(noteTopRow, noteBottomRow).Height = 24;
        sheet.SheetView.FreezeRows(monthRow);
        sheet.SheetView.FreezeColumns(leftColumns);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PagesWide = 1;
        sheet.PageSetup.Margins.Top = 0.35;
        sheet.PageSetup.Margins.Bottom = 0.35;
        sheet.PageSetup.Margins.Left = 0.25;
        sheet.PageSetup.Margins.Right = 0.25;
    }

    private static void WriteGanttProjectInfo(
        IXLWorksheet sheet,
        Project project,
        ArchiveDemandInfo archiveDemand,
        int topRow,
        int monthStartColumn,
        int lastColumn)
    {
        var ownerText = string.Join("; ", project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId));
        var rightStartColumn = Math.Max(monthStartColumn + 2, lastColumn - 5);

        SetMergedText(sheet, topRow, 1, topRow + 2, 3, "台塑\n电子材料部");
        sheet.Cell(topRow, 1).Style.Alignment.WrapText = true;
        sheet.Cell(topRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        SetMergedText(sheet, topRow, monthStartColumn, topRow + 1, rightStartColumn - 1, project.Name);
        sheet.Cell(topRow, monthStartColumn).Style.Font.Bold = true;
        sheet.Cell(topRow, monthStartColumn).Style.Font.FontSize = 14;
        sheet.Cell(topRow, monthStartColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Cell(topRow, monthStartColumn).Style.Alignment.WrapText = true;

        sheet.Cell(topRow, rightStartColumn).Value = "专案编号";
        SetMergedText(sheet, topRow, rightStartColumn + 1, topRow, rightStartColumn + 2, project.ProjectNumber);
        sheet.Cell(topRow + 1, rightStartColumn).Value = "客戶";
        SetMergedText(sheet, topRow + 1, rightStartColumn + 1, topRow + 1, rightStartColumn + 2, ownerText);
        sheet.Cell(topRow, rightStartColumn + 3).Value = "檔案编号";
        SetMergedText(sheet, topRow, rightStartColumn + 4, topRow, lastColumn, $"GANTT-{project.Id}");
        sheet.Cell(topRow + 1, rightStartColumn + 3).Value = "档案日期";
        SetMergedText(sheet, topRow + 1, rightStartColumn + 4, topRow + 1, lastColumn, archiveDemand.Date.ToString("yyyy/M/d"));

        sheet.Range(topRow, rightStartColumn, topRow + 1, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        SetMergedText(
            sheet,
            topRow + 2,
            monthStartColumn,
            topRow + 2,
            lastColumn,
            $"進度線負責人：{archiveDemand.Person}    整体開始日：{project.GanttPlan?.StartDate?.ToString("yyyy-MM-dd") ?? "-"}    整体完成日：{project.GanttPlan?.FinishDate?.ToString("yyyy-MM-dd") ?? "-"}    狀態：{project.Status?.Name ?? "-"}");
        sheet.Cell(topRow + 2, monthStartColumn).Style.Alignment.WrapText = true;
    }

    private static void WriteGanttTimeHeader(
        IXLWorksheet sheet,
        IReadOnlyList<GanttMonth> months,
        int yearRow,
        int monthRow,
        int monthStartColumn,
        int ratioColumn,
        int actualColumn)
    {
        SetMergedText(sheet, yearRow, 1, monthRow, 1, "项次");
        SetMergedText(sheet, yearRow, 2, monthRow, 3, "名稱");
        SetMergedText(sheet, yearRow, ratioColumn, yearRow, actualColumn, "進度");
        sheet.Cell(monthRow, ratioColumn).Value = "比例";
        sheet.Cell(monthRow, actualColumn).Value = "實際完成";

        foreach (var group in months.Select((month, index) => new { month.Month.Year, Index = index }).GroupBy(x => x.Year))
        {
            var start = monthStartColumn + group.Min(x => x.Index);
            var end = monthStartColumn + group.Max(x => x.Index);
            SetMergedText(sheet, yearRow, start, yearRow, end, group.Key.ToString());
            sheet.Range(yearRow, start, yearRow, end).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (var i = 0; i < months.Count; i++)
        {
            sheet.Cell(monthRow, monthStartColumn + i).Value = months[i].Label;
            sheet.Cell(monthRow, monthStartColumn + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var headerRange = sheet.Range(yearRow, 1, monthRow, actualColumn);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void WriteGanttBody(
        IXLWorksheet sheet,
        IReadOnlyList<ProjectGanttTaskInputModel> tasks,
        IReadOnlyList<decimal> weights,
        IReadOnlyList<GanttMonth> months,
        int firstTaskRow,
        int monthStartColumn,
        int ratioColumn,
        int actualColumn)
    {
        if (tasks.Count == 0)
        {
            var row = firstTaskRow;
            sheet.Cell(row, 1).Value = 1;
            SetMergedText(sheet, row, 2, row, 3, "请先在系统内维护细分工作");
            sheet.Cell(row, ratioColumn).Value = 0;
            sheet.Cell(row, actualColumn).Value = 0;
            sheet.Cell(row, ratioColumn).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, actualColumn).Style.NumberFormat.Format = "0.00%";
            return;
        }

        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var row = firstTaskRow + i;
            sheet.Cell(row, 1).Value = task.SortOrder > 0 ? task.SortOrder : i + 1;
            SetMergedText(sheet, row, 2, row, 3, task.Name ?? string.Empty);
            sheet.Cell(row, ratioColumn).Value = weights[i] / 100m;
            sheet.Cell(row, ratioColumn).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, actualColumn).Value = task.ProgressPercent / 100m;
            sheet.Cell(row, actualColumn).Style.NumberFormat.Format = "0.00%";
            sheet.Range(row, 1, row, actualColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            sheet.Cell(row, 2).Style.Alignment.WrapText = true;
            PaintTaskBar(sheet, task, months, row, monthStartColumn);
        }
    }

    private static void PaintTaskBar(
        IXLWorksheet sheet,
        ProjectGanttTaskInputModel task,
        IReadOnlyList<GanttMonth> months,
        int row,
        int monthStartColumn)
    {
        if (task.PlannedStartDate is null || task.PlannedFinishDate is null || months.Count == 0)
        {
            return;
        }

        var startDate = task.PlannedStartDate.Value;
        var finishDate = task.PlannedFinishDate.Value;
        if (finishDate < startDate)
        {
            (startDate, finishDate) = (finishDate, startDate);
        }

        var startIndex = FindBucketIndex(months, startDate);
        var finishIndex = FindBucketIndex(months, finishDate);
        if (startIndex < 0 || finishIndex < 0)
        {
            return;
        }

        var plannedCells = finishIndex - startIndex + 1;
        var completedCells = (int)Math.Ceiling(plannedCells * ClampPercent(task.ProgressPercent) / 100m);

        for (var index = startIndex; index <= finishIndex; index++)
        {
            var cell = sheet.Cell(row, monthStartColumn + index);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.Black;
        }

        for (var index = startIndex; index < startIndex + completedCells && index <= finishIndex; index++)
        {
            sheet.Cell(row, monthStartColumn + index).Style.Fill.BackgroundColor = XLColor.FromHtml("#22c55e");
        }
    }

    private static void PaintArchiveProgressLine(
        IXLWorksheet sheet,
        DateOnly archiveDate,
        IReadOnlyList<ProjectGanttTaskInputModel> tasks,
        IReadOnlyList<GanttMonth> months,
        int headerRow,
        int firstTaskRow,
        int lastRow,
        int monthStartColumn)
    {
        var monthIndex = FindBucketIndex(months, archiveDate);
        if (monthIndex < 0)
        {
            return;
        }

        var archiveBucket = months[monthIndex];
        var archiveColumn = monthStartColumn + monthIndex;
        var useArchiveRightBorder = archiveDate.DayNumber - archiveBucket.Month.DayNumber >
                                    (archiveBucket.EndMonth.DayNumber - archiveBucket.Month.DayNumber) / 2;
        var headerCell = sheet.Cell(headerRow, archiveColumn);
        if (useArchiveRightBorder)
        {
            headerCell.Style.Border.RightBorder = XLBorderStyleValues.Medium;
            headerCell.Style.Border.RightBorderColor = XLColor.Red;
        }
        else
        {
            headerCell.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            headerCell.Style.Border.LeftBorderColor = XLColor.Red;
        }

        var points = BuildProgressLinePoints(tasks, archiveDate, months);
        for (var index = 0; index < points.Count && firstTaskRow + index <= lastRow; index++)
        {
            var point = points[index];
            var timelineDays = months[^1].EndMonth.DayNumber - months[0].Month.DayNumber + 1;
            var pointDateOffset = Math.Clamp(
                (int)Math.Round(point.PositionPercent / 100m * timelineDays),
                0,
                timelineDays - 1);
            var pointDate = months[0].Month.AddDays(pointDateOffset);
            var pointBucketIndex = Math.Max(0, FindBucketIndex(months, pointDate));
            var cell = sheet.Cell(firstTaskRow + index, monthStartColumn + pointBucketIndex);
            var color = point.State == GanttProgressState.Ahead ? XLColor.Blue : XLColor.Red;
            cell.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
            cell.Style.Border.LeftBorderColor = color;
        }
    }

    private static void WriteGanttFooter(
        IXLWorksheet sheet,
        Project project,
        DateOnly archiveDate,
        int noteTopRow,
        int noteBottomRow,
        int monthStartColumn,
        int ratioColumn,
        int actualColumn,
        IReadOnlyList<ProjectGanttTaskInputModel> tasks,
        IReadOnlyList<decimal> weights)
    {
        SetMergedText(sheet, noteTopRow, 1, noteBottomRow, 1, "進度說明");
        sheet.Cell(noteTopRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var noteRightColumn = Math.Max(monthStartColumn, ratioColumn - 2);
        SetMergedText(sheet, noteTopRow, 2, noteBottomRow, noteRightColumn, BuildProgressNote(project));
        sheet.Cell(noteTopRow, 2).Style.Alignment.WrapText = true;
        sheet.Cell(noteTopRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        SetMergedText(sheet, noteTopRow, ratioColumn - 1, noteTopRow, ratioColumn, "应达進度(%)");
        sheet.Cell(noteTopRow, actualColumn).Value = "實際進度(%)";
        sheet.Range(noteTopRow, ratioColumn - 1, noteTopRow, actualColumn).Style.Font.Bold = true;
        sheet.Range(noteTopRow, ratioColumn - 1, noteTopRow, actualColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, ratioColumn).Merge();
        sheet.Cell(noteTopRow + 1, ratioColumn - 1).Value = CalculateExpectedProgress(tasks, weights, archiveDate) / 100m;
        sheet.Range(noteTopRow + 1, actualColumn, noteBottomRow, actualColumn).Merge();
        sheet.Cell(noteTopRow + 1, actualColumn).Value = CalculateActualProgress(tasks, weights) / 100m;
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.NumberFormat.Format = "0.00%";
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.Font.FontSize = 18;
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static ProjectGanttInputModel ToInput(Project project)
    {
        var plan = project.GanttPlan;
        var tasks = plan?.Tasks
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new ProjectGanttTaskInputModel
            {
                Id = x.Id,
                SortOrder = x.SortOrder,
                Name = x.Name,
                PlannedStartDate = x.PlannedStartDate,
                PlannedFinishDate = x.PlannedFinishDate,
                ProgressPercent = x.ProgressPercent,
                ProgressDescription = x.ProgressDescription
            })
            .ToList() ?? [];

        if (tasks.Count == 0)
        {
            tasks = BuildDefaultTasks(project);
        }

        return new ProjectGanttInputModel
        {
            StartDate = plan?.StartDate ?? tasks.Min(x => x.PlannedStartDate),
            FinishDate = plan?.FinishDate ?? tasks.Max(x => x.PlannedFinishDate),
            ProgressNote = plan?.ProgressNote,
            Tasks = tasks
        };
    }

    private static List<ProjectGanttTaskInputModel> BuildDefaultTasks(Project project)
    {
        var year = Math.Clamp(project.Year, 1, 9998);
        var yearStart = new DateOnly(year, 1, 1);
        var templates = new (string Name, int StartMonth, int FinishMonth)[]
        {
            ("配合客戶需求與規劃", 0, 1),
            ("業主請購規範確認", 1, 2),
            ("專案立案", 3, 3),
            ("機械功能設計確認", 4, 5),
            ("電控介面設計確認", 5, 5),
            ("採購與製作排程", 4, 6),
            ("現場安裝與整合", 6, 8),
            ("系統功能測試", 7, 9),
            ("現場試車與細部調校", 9, 11),
            ("教育訓練及驗收", 10, 14)
        };
        var weights = BuildTaskWeights(templates.Length);
        var remainingProgress = ClampPercent(project.ProgressPercent);
        var tasks = new List<ProjectGanttTaskInputModel>(templates.Length);

        for (var index = 0; index < templates.Length; index++)
        {
            var template = templates[index];
            var weightedProgress = Math.Min(remainingProgress, weights[index]);
            var taskProgress = weights[index] == 0 ? 0 : weightedProgress / weights[index] * 100m;
            remainingProgress -= weightedProgress;
            tasks.Add(new ProjectGanttTaskInputModel
            {
                SortOrder = index + 1,
                Name = template.Name,
                PlannedStartDate = yearStart.AddMonths(template.StartMonth),
                PlannedFinishDate = yearStart.AddMonths(template.FinishMonth + 1).AddDays(-1),
                ProgressPercent = taskProgress
            });
        }

        return tasks;
    }

    private static string BuildProgressNote(Project project)
    {
        var notes = project.GanttPlan?.Tasks
            .OrderBy(x => x.SortOrder)
            .Where(x => !string.IsNullOrWhiteSpace(x.ProgressDescription))
            .Select((x, index) => $"{index + 1}. {x.ProgressDescription}")
            .ToList() ?? [];

        if (!string.IsNullOrWhiteSpace(project.GanttPlan?.ProgressNote))
        {
            notes.Insert(0, project.GanttPlan.ProgressNote);
        }

        return notes.Count == 0 ? "暂无進度說明。" : string.Join(Environment.NewLine, notes);
    }

    private static void SetMergedText(
        IXLWorksheet sheet,
        int firstRow,
        int firstColumn,
        int lastRow,
        int lastColumn,
        string value)
    {
        if (firstRow != lastRow || firstColumn != lastColumn)
        {
            sheet.Range(firstRow, firstColumn, lastRow, lastColumn).Merge();
        }

        sheet.Cell(firstRow, firstColumn).Value = value;
    }

    private static void WriteProjectHeader(IXLWorksheet sheet, Project project)
    {
        var plan = project.GanttPlan;
        sheet.Cell(1, 1).Value = $"{project.ProjectNumber} {project.Name} 预估進度";
        sheet.Range(1, 1, 1, 8).Merge().Style.Font.SetBold().Font.SetFontSize(18);

        var rows = new (string Label, string Value)[]
        {
            ("專案名稱", project.Name),
            ("專案工號", project.ProjectNumber),
            ("母案案號", project.ParentCaseNumber ?? string.Empty),
            ("年度", project.Year.ToString()),
            ("客戶/負責人", string.Join("; ", project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId))),
            ("狀態", project.Status?.Name ?? string.Empty),
            ("整体開始日", plan?.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty),
            ("整体完成日", plan?.FinishDate?.ToString("yyyy-MM-dd") ?? string.Empty),
            ("進度說明", plan?.ProgressNote ?? string.Empty)
        };

        var row = 3;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.Value;
            sheet.Range(row, 2, row, 8).Merge();
            row++;
        }

        sheet.Range(3, 1, row - 1, 1).Style.Font.Bold = true;
        sheet.Range(3, 1, row - 1, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(3, 1, row - 1, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(3, 1, row - 1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#eaf1ff");
    }

    private static void WriteTaskTable(IXLWorksheet sheet, Project project)
    {
        var tasks = project.GanttPlan?.Tasks.OrderBy(x => x.SortOrder).ToList() ?? [];
        var startRow = 14;
        var headers = new[] { "序号", "细分工作內容", "预计開始日", "预计完成日", "当前進度", "進度說明" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(startRow, i + 1).Value = headers[i];
        }

        sheet.Range(startRow, 1, startRow, headers.Length).Style.Font.Bold = true;
        sheet.Range(startRow, 1, startRow, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f6fb");

        var row = startRow + 1;
        foreach (var task in tasks)
        {
            sheet.Cell(row, 1).Value = task.SortOrder;
            sheet.Cell(row, 2).Value = task.Name;
            sheet.Cell(row, 3).Value = task.PlannedStartDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            sheet.Cell(row, 4).Value = task.PlannedFinishDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            sheet.Cell(row, 5).Value = task.ProgressPercent / 100;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "0.00%";
            sheet.Cell(row, 6).Value = task.ProgressDescription ?? string.Empty;
            row++;
        }

        var usedLastRow = Math.Max(row - 1, startRow);
        sheet.Range(startRow, 1, usedLastRow, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(startRow, 1, usedLastRow, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.Column(2).Width = Math.Max(sheet.Column(2).Width, 24);
        sheet.Column(6).Width = Math.Max(sheet.Column(6).Width, 36);
        sheet.SheetView.FreezeRows(startRow);
    }

    private static bool HasTaskData(ProjectGanttTaskInputModel task)
    {
        return !string.IsNullOrWhiteSpace(task.Name) ||
               task.PlannedStartDate is not null ||
               task.PlannedFinishDate is not null ||
               task.ProgressPercent != 0 ||
               !string.IsNullOrWhiteSpace(task.ProgressDescription);
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal ClampPercent(decimal value)
    {
        return Math.Min(100, Math.Max(0, value));
    }

    private static decimal CalculateTaskExpectedPercent(
        DateOnly start,
        DateOnly finish,
        DateOnly archiveDate)
    {
        if (archiveDate < start)
        {
            return 0;
        }

        if (archiveDate >= finish)
        {
            return 100;
        }

        var totalDays = Math.Max(1, finish.DayNumber - start.DayNumber + 1);
        return Math.Clamp(
            (archiveDate.DayNumber - start.DayNumber + 1m) / totalDays * 100m,
            0m,
            100m);
    }

    private static DateOnly AlignBucketStart(
        DateOnly date,
        GanttTimeUnit unit,
        int stepYears)
    {
        return unit switch
        {
            GanttTimeUnit.Day => date,
            GanttTimeUnit.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
            GanttTimeUnit.Month => new DateOnly(date.Year, date.Month, 1),
            GanttTimeUnit.Quarter => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
            _ => new DateOnly(date.Year, 1, 1)
        };
    }

    private static DateOnly AlignBucketEnd(
        DateOnly date,
        GanttTimeUnit unit,
        int stepYears)
    {
        var start = AlignBucketStart(date, unit, stepYears);
        return unit == GanttTimeUnit.Year
            ? new DateOnly(date.Year, 12, 31)
            : GetBucketEnd(start, unit, stepYears);
    }

    private static DateOnly GetBucketEnd(
        DateOnly bucketStart,
        GanttTimeUnit unit,
        int stepYears)
    {
        return unit switch
        {
            GanttTimeUnit.Day => bucketStart,
            GanttTimeUnit.Week => bucketStart.AddDays(6),
            GanttTimeUnit.Month => bucketStart.AddMonths(1).AddDays(-1),
            GanttTimeUnit.Quarter => bucketStart.AddMonths(3).AddDays(-1),
            _ => bucketStart.AddYears(stepYears).AddDays(-1)
        };
    }

    private static int ResolveStepMonths(GanttTimeUnit unit, int stepYears)
    {
        return unit switch
        {
            GanttTimeUnit.Month => 1,
            GanttTimeUnit.Quarter => 3,
            GanttTimeUnit.Year => stepYears * 12,
            _ => 0
        };
    }

    private static string BuildBucketLabel(
        DateOnly start,
        DateOnly finish,
        GanttTimeUnit unit)
    {
        return unit switch
        {
            GanttTimeUnit.Day => start.ToString("M/d"),
            GanttTimeUnit.Week => start.ToString("M/d"),
            GanttTimeUnit.Month => start.ToString("yyyy/M"),
            GanttTimeUnit.Quarter => $"{start.Year} Q{(start.Month - 1) / 3 + 1}",
            _ when start.Year != finish.Year => $"{start.Year}-{finish.Year}",
            _ => start.Year.ToString()
        };
    }

    private static int FindBucketIndex(IReadOnlyList<GanttMonth> months, DateOnly value)
    {
        for (var i = 0; i < months.Count; i++)
        {
            if (value >= months[i].Month && value <= months[i].EndMonth)
            {
                return i;
            }
        }

        return -1;
    }

    private static string RequiredOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

public sealed class ProjectGanttInputModel
{
    public DateOnly? StartDate { get; set; }

    public DateOnly? FinishDate { get; set; }

    public string? ProgressNote { get; set; }

    public List<ProjectGanttTaskInputModel> Tasks { get; set; } = [];
}

public sealed class ProjectGanttTaskInputModel
{
    public int Id { get; set; }

    public int SortOrder { get; set; }

    public string? Name { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedFinishDate { get; set; }

    public decimal ProgressPercent { get; set; }

    public string? ProgressDescription { get; set; }
}

public enum GanttTimeUnit
{
    Day,
    Week,
    Month,
    Quarter,
    Year
}

public enum GanttProgressState
{
    OnSchedule,
    Behind,
    Ahead
}

public enum GanttTaskVisualState
{
    NotStarted,
    InProgress,
    Ahead,
    AtRisk,
    Waiting,
    Blocked,
    Completed
}

public sealed record GanttMonth(
    DateOnly Month,
    DateOnly EndMonth,
    string Label,
    int StepMonths,
    GanttTimeUnit Unit);

public sealed record GanttBar(decimal LeftPercent, decimal WidthPercent, decimal ProgressPercent);

public sealed record GanttProgressPoint(
    decimal PositionPercent,
    GanttProgressState State,
    decimal ExpectedPercent);

public sealed record ArchiveDemandInfo(DateOnly Date, string Person);
