using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class ProjectGanttService(ApplicationDbContext db)
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ProjectGanttInputModel> BuildInputAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        var plan = await db.ProjectGanttPlans
            .AsNoTracking()
            .Include(x => x.Tasks)
            .SingleOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        if (plan is null)
        {
            return new ProjectGanttInputModel();
        }

        return new ProjectGanttInputModel
        {
            StartDate = plan.StartDate,
            FinishDate = plan.FinishDate,
            ProgressNote = plan.ProgressNote,
            Tasks = plan.Tasks
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
                .ToList()
        };
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
            .Include(x => x.GanttPlan)
            .ThenInclude(x => x!.Tasks)
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new InvalidOperationException("Project was not found.");
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Gantt");
        WriteGanttWorksheet(sheet, project);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExportFile(
            $"gantt-{project.ProjectNumber}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    public static IReadOnlyList<GanttMonth> BuildMonths(ProjectGanttInputModel input)
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
            var today = DateOnly.FromDateTime(DateTime.Today);
            dates.Add(new DateOnly(today.Year, today.Month, 1));
            dates.Add(new DateOnly(today.Year, today.Month, 1).AddMonths(5));
        }

        var start = new DateOnly(dates.Min().Year, dates.Min().Month, 1);
        var finish = new DateOnly(dates.Max().Year, dates.Max().Month, 1);
        if (finish < start)
        {
            (start, finish) = (finish, start);
        }

        if (finish < start.AddMonths(5))
        {
            finish = start.AddMonths(5);
        }

        var totalMonths = CountInclusiveMonths(start, finish);
        var stepMonths = ResolveTimelineStepMonths(totalMonths);
        var months = new List<GanttMonth>();
        for (var month = start; month <= finish; month = month.AddMonths(stepMonths))
        {
            var endMonth = month.AddMonths(stepMonths - 1);
            if (endMonth > finish)
            {
                endMonth = finish;
            }

            months.Add(new GanttMonth(month, endMonth, month.ToString("yyyy-MM"), stepMonths));
        }

        return months;
    }

    public static GanttBar BuildBar(ProjectGanttTaskInputModel task, IReadOnlyList<GanttMonth> months)
    {
        if (task.PlannedStartDate is null || task.PlannedFinishDate is null || months.Count == 0)
        {
            return new GanttBar(0, 0, 0);
        }

        var startMonth = new DateOnly(task.PlannedStartDate.Value.Year, task.PlannedStartDate.Value.Month, 1);
        var finishMonth = new DateOnly(task.PlannedFinishDate.Value.Year, task.PlannedFinishDate.Value.Month, 1);
        if (finishMonth < startMonth)
        {
            (startMonth, finishMonth) = (finishMonth, startMonth);
        }

        var startIndex = Math.Max(0, FindBucketIndex(months, startMonth));
        var finishIndex = FindBucketIndex(months, finishMonth);
        finishIndex = finishIndex < 0 ? startIndex : finishIndex;
        var left = startIndex / (decimal)months.Count * 100;
        var width = Math.Max(1, (finishIndex - startIndex + 1) / (decimal)months.Count * 100);
        return new GanttBar(left, width, ClampPercent(task.ProgressPercent));
    }

    private static List<string> Validate(ProjectGanttInputModel input)
    {
        var errors = new List<string>();
        if (input.StartDate is not null &&
            input.FinishDate is not null &&
            input.FinishDate < input.StartDate)
        {
            errors.Add("整体完成日不能早于整体开始日。");
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
                errors.Add($"第 {index} 项细分工作的预计完成日不能早于预计开始日。");
            }

            if (task.ProgressPercent < 0 || task.ProgressPercent > 100)
            {
                errors.Add($"第 {index} 项细分工作的当前进度必须在 0 到 100 之间。");
            }
        }

        return errors;
    }

    private static void WriteGanttWorksheet(IXLWorksheet sheet, Project project)
    {
        var plan = project.GanttPlan;
        var tasks = plan?.Tasks.OrderBy(x => x.SortOrder).ToList() ?? [];
        var input = ToInput(plan);
        var months = BuildMonths(input);

        const int titleRow = 1;
        const int infoTopRow = 3;
        const int yearRow = 6;
        const int monthRow = 7;
        const int firstTaskRow = 8;
        const int leftColumns = 3;
        var monthStartColumn = leftColumns + 1;
        var ratioColumn = monthStartColumn + months.Count;
        var actualColumn = ratioColumn + 1;
        var lastColumn = actualColumn;
        var visibleTaskCount = Math.Max(tasks.Count, 1);
        var lastTaskRow = firstTaskRow + visibleTaskCount - 1;
        var noteTopRow = lastTaskRow + 1;
        var noteBottomRow = noteTopRow + 4;

        sheet.Style.Font.FontName = "Microsoft YaHei";
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        SetMergedText(sheet, titleRow, 1, titleRow, lastColumn, $"{project.ProjectNumber}{project.Name}-预估进度");
        sheet.Cell(titleRow, 1).Style.Font.Bold = true;
        sheet.Cell(titleRow, 1).Style.Font.FontSize = 22;
        sheet.Cell(titleRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Row(titleRow).Height = 34;

        WriteGanttProjectInfo(sheet, project, infoTopRow, monthStartColumn, lastColumn);
        WriteGanttTimeHeader(sheet, months, yearRow, monthRow, monthStartColumn, ratioColumn, actualColumn);
        WriteGanttBody(sheet, tasks, months, firstTaskRow, monthStartColumn, ratioColumn, actualColumn, visibleTaskCount);
        WriteGanttFooter(sheet, project, noteTopRow, noteBottomRow, monthStartColumn, ratioColumn, actualColumn, tasks);

        var usedRange = sheet.Range(1, 1, noteBottomRow, lastColumn);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.Column(1).Width = 5;
        sheet.Column(2).Width = 24;
        sheet.Column(3).Width = 5;
        for (var column = monthStartColumn; column < ratioColumn; column++)
        {
            sheet.Column(column).Width = 4.2;
        }

        sheet.Column(ratioColumn).Width = 10;
        sheet.Column(actualColumn).Width = 12;
        sheet.Rows(firstTaskRow, lastTaskRow).Height = 22;
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
        int topRow,
        int monthStartColumn,
        int lastColumn)
    {
        var plan = project.GanttPlan;
        var ownerText = string.Join("; ", project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId));
        var rightStartColumn = Math.Max(monthStartColumn + 2, lastColumn - 5);

        SetMergedText(sheet, topRow, 1, topRow + 2, 3, "台塑\n电子材料部");
        sheet.Cell(topRow, 1).Style.Alignment.WrapText = true;
        sheet.Cell(topRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        SetMergedText(sheet, topRow, monthStartColumn, topRow + 1, rightStartColumn - 1, project.Name);
        sheet.Range(topRow, monthStartColumn, topRow + 1, rightStartColumn - 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#dbeafe");
        sheet.Cell(topRow, monthStartColumn).Style.Font.Bold = true;
        sheet.Cell(topRow, monthStartColumn).Style.Font.FontSize = 14;
        sheet.Cell(topRow, monthStartColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        sheet.Cell(topRow, rightStartColumn).Value = "专案编号";
        SetMergedText(sheet, topRow, rightStartColumn + 1, topRow, rightStartColumn + 2, project.ProjectNumber);
        sheet.Cell(topRow + 1, rightStartColumn).Value = "客户";
        SetMergedText(sheet, topRow + 1, rightStartColumn + 1, topRow + 1, rightStartColumn + 2, ownerText);
        sheet.Cell(topRow, rightStartColumn + 3).Value = "文件编号";
        SetMergedText(sheet, topRow, rightStartColumn + 4, topRow, lastColumn, $"GANTT-{project.Id}");
        sheet.Cell(topRow + 1, rightStartColumn + 3).Value = "档案日期";
        SetMergedText(sheet, topRow + 1, rightStartColumn + 4, topRow + 1, lastColumn, DateTime.Today.ToString("yyyy/M/d"));

        sheet.Range(topRow, rightStartColumn, topRow + 1, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#dbeafe");
        sheet.Range(topRow, rightStartColumn, topRow + 1, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        SetMergedText(
            sheet,
            topRow + 2,
            monthStartColumn,
            topRow + 2,
            lastColumn,
            $"整体开始日：{plan?.StartDate?.ToString("yyyy-MM-dd") ?? "-"}    整体完成日：{plan?.FinishDate?.ToString("yyyy-MM-dd") ?? "-"}    状态：{project.Status?.Name ?? "-"}");
        sheet.Cell(topRow + 2, monthStartColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#eff6ff");
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
        SetMergedText(sheet, yearRow, 2, monthRow, 3, "名称");
        SetMergedText(sheet, yearRow, ratioColumn, yearRow, actualColumn, "进度");
        sheet.Cell(monthRow, ratioColumn).Value = "比例";
        sheet.Cell(monthRow, actualColumn).Value = "实际完成";

        foreach (var group in months.Select((month, index) => new { month.Month.Year, Index = index }).GroupBy(x => x.Year))
        {
            var start = monthStartColumn + group.Min(x => x.Index);
            var end = monthStartColumn + group.Max(x => x.Index);
            SetMergedText(sheet, yearRow, start, yearRow, end, group.Key.ToString());
            sheet.Range(yearRow, start, yearRow, end).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (var i = 0; i < months.Count; i++)
        {
            sheet.Cell(monthRow, monthStartColumn + i).Value = months[i].Month.Month;
            sheet.Cell(monthRow, monthStartColumn + i).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var headerRange = sheet.Range(yearRow, 1, monthRow, actualColumn);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void WriteGanttBody(
        IXLWorksheet sheet,
        IReadOnlyList<ProjectGanttTask> tasks,
        IReadOnlyList<GanttMonth> months,
        int firstTaskRow,
        int monthStartColumn,
        int ratioColumn,
        int actualColumn,
        int visibleTaskCount)
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

        var weight = 1m / visibleTaskCount;
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var row = firstTaskRow + i;
            sheet.Cell(row, 1).Value = task.SortOrder;
            SetMergedText(sheet, row, 2, row, 3, task.Name);
            sheet.Cell(row, ratioColumn).Value = weight;
            sheet.Cell(row, ratioColumn).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, actualColumn).Value = task.ProgressPercent / 100m;
            sheet.Cell(row, actualColumn).Style.NumberFormat.Format = "0.00%";
            sheet.Range(row, 1, row, actualColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            PaintTaskBar(sheet, task, months, row, monthStartColumn);
        }
    }

    private static void PaintTaskBar(
        IXLWorksheet sheet,
        ProjectGanttTask task,
        IReadOnlyList<GanttMonth> months,
        int row,
        int monthStartColumn)
    {
        if (task.PlannedStartDate is null || task.PlannedFinishDate is null || months.Count == 0)
        {
            return;
        }

        var startMonth = new DateOnly(task.PlannedStartDate.Value.Year, task.PlannedStartDate.Value.Month, 1);
        var finishMonth = new DateOnly(task.PlannedFinishDate.Value.Year, task.PlannedFinishDate.Value.Month, 1);
        if (finishMonth < startMonth)
        {
            (startMonth, finishMonth) = (finishMonth, startMonth);
        }

        var startIndex = FindBucketIndex(months, startMonth);
        var finishIndex = FindBucketIndex(months, finishMonth);
        if (startIndex < 0 || finishIndex < 0)
        {
            return;
        }

        var plannedCells = finishIndex - startIndex + 1;
        var completedCells = (int)Math.Ceiling(plannedCells * ClampPercent(task.ProgressPercent) / 100m);

        for (var index = startIndex; index <= finishIndex; index++)
        {
            var cell = sheet.Cell(row, monthStartColumn + index);
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#bbf7d0");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#16a34a");
        }

        for (var index = startIndex; index < startIndex + completedCells && index <= finishIndex; index++)
        {
            sheet.Cell(row, monthStartColumn + index).Style.Fill.BackgroundColor = XLColor.FromHtml("#22c55e");
        }
    }

    private static void WriteGanttFooter(
        IXLWorksheet sheet,
        Project project,
        int noteTopRow,
        int noteBottomRow,
        int monthStartColumn,
        int ratioColumn,
        int actualColumn,
        IReadOnlyList<ProjectGanttTask> tasks)
    {
        SetMergedText(sheet, noteTopRow, 1, noteBottomRow, 1, "进度说明");
        sheet.Cell(noteTopRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var noteRightColumn = Math.Max(monthStartColumn, ratioColumn - 2);
        SetMergedText(sheet, noteTopRow, 2, noteBottomRow, noteRightColumn, BuildProgressNote(project));
        sheet.Cell(noteTopRow, 2).Style.Alignment.WrapText = true;
        sheet.Cell(noteTopRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        SetMergedText(sheet, noteTopRow, ratioColumn - 1, noteTopRow, ratioColumn, "应达进度(%)");
        sheet.Cell(noteTopRow, actualColumn).Value = "实际进度(%)";
        sheet.Range(noteTopRow, ratioColumn - 1, noteTopRow, actualColumn).Style.Font.Bold = true;
        sheet.Range(noteTopRow, ratioColumn - 1, noteTopRow, actualColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, ratioColumn).Merge();
        sheet.Cell(noteTopRow + 1, ratioColumn - 1).Value = CalculateExpectedProgress(project.GanttPlan);
        sheet.Range(noteTopRow + 1, actualColumn, noteBottomRow, actualColumn).Merge();
        sheet.Cell(noteTopRow + 1, actualColumn).Value = CalculateActualProgress(tasks);
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.NumberFormat.Format = "0.00%";
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.Font.FontSize = 18;
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(noteTopRow + 1, ratioColumn - 1, noteBottomRow, actualColumn).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static ProjectGanttInputModel ToInput(ProjectGanttPlan? plan)
    {
        if (plan is null)
        {
            return new ProjectGanttInputModel();
        }

        return new ProjectGanttInputModel
        {
            StartDate = plan.StartDate,
            FinishDate = plan.FinishDate,
            ProgressNote = plan.ProgressNote,
            Tasks = plan.Tasks
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProjectGanttTaskInputModel
                {
                    Name = x.Name,
                    PlannedStartDate = x.PlannedStartDate,
                    PlannedFinishDate = x.PlannedFinishDate,
                    ProgressPercent = x.ProgressPercent,
                    ProgressDescription = x.ProgressDescription
                })
                .ToList()
        };
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

        return notes.Count == 0 ? "暂无进度说明。" : string.Join(Environment.NewLine, notes);
    }

    private static decimal CalculateExpectedProgress(ProjectGanttPlan? plan)
    {
        if (plan?.StartDate is null || plan.FinishDate is null)
        {
            return 0;
        }

        var start = plan.StartDate.Value.ToDateTime(TimeOnly.MinValue);
        var finish = plan.FinishDate.Value.ToDateTime(TimeOnly.MinValue);
        if (finish <= start)
        {
            return DateTime.Today >= finish ? 1 : 0;
        }

        var totalDays = (decimal)(finish - start).TotalDays;
        var elapsedDays = Math.Clamp((decimal)(DateTime.Today - start).TotalDays, 0, totalDays);
        return elapsedDays / totalDays;
    }

    private static decimal CalculateActualProgress(IReadOnlyList<ProjectGanttTask> tasks)
    {
        return tasks.Count == 0 ? 0 : tasks.Average(x => x.ProgressPercent) / 100m;
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
        sheet.Cell(1, 1).Value = $"{project.ProjectNumber} {project.Name} 预估进度";
        sheet.Range(1, 1, 1, 8).Merge().Style.Font.SetBold().Font.SetFontSize(18);

        var rows = new (string Label, string Value)[]
        {
            ("项目名称", project.Name),
            ("项目工号", project.ProjectNumber),
            ("母案案号", project.ParentCaseNumber ?? string.Empty),
            ("年度", project.Year.ToString()),
            ("客户/负责人", string.Join("; ", project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId))),
            ("状态", project.Status?.Name ?? string.Empty),
            ("整体开始日", plan?.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty),
            ("整体完成日", plan?.FinishDate?.ToString("yyyy-MM-dd") ?? string.Empty),
            ("进度说明", plan?.ProgressNote ?? string.Empty)
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
        var headers = new[] { "序号", "细分工作内容", "预计开始日", "预计完成日", "当前进度", "进度说明" };
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

    private static decimal ClampPercent(decimal value)
    {
        return Math.Min(100, Math.Max(0, value));
    }

    private static int CountInclusiveMonths(DateOnly start, DateOnly finish)
    {
        return (finish.Year - start.Year) * 12 + finish.Month - start.Month + 1;
    }

    private static int ResolveTimelineStepMonths(int totalMonths)
    {
        return totalMonths switch
        {
            <= 12 => 1,
            <= 24 => 2,
            <= 36 => 3,
            <= 72 => 6,
            _ => 12
        };
    }

    private static int FindBucketIndex(IReadOnlyList<GanttMonth> months, DateOnly value)
    {
        var month = new DateOnly(value.Year, value.Month, 1);
        for (var i = 0; i < months.Count; i++)
        {
            if (month >= months[i].Month && month <= months[i].EndMonth)
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

public sealed record GanttMonth(DateOnly Month, DateOnly EndMonth, string Label, int StepMonths);

public sealed record GanttBar(decimal LeftPercent, decimal WidthPercent, decimal ProgressPercent);
