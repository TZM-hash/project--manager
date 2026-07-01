using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class SettlementService(ApplicationDbContext db)
{
    public async Task<CreateSettlementResult> CreateAsync(
        CreateSettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Month is < 1 or > 12)
        {
            return new CreateSettlementResult(
                false,
                null,
                ["Settlement month must be between 1 and 12."]);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var nextBatchNumber = await db.MonthlySettlementBatches
            .Where(x => x.Year == request.Year && x.Month == request.Month)
            .Select(x => (int?)x.BatchNumber)
            .MaxAsync(cancellationToken) ?? 0;
        nextBatchNumber++;

        var batch = new MonthlySettlementBatch
        {
            Year = request.Year,
            Month = request.Month,
            BatchNumber = nextBatchNumber,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes
        };

        var projects = await db.Projects
            .Include(x => x.Status)
            .Include(x => x.UpdatedByUser)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.PurchaseStaff)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.SubCaseContact)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Year)
            .ThenBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);

        foreach (var project in projects)
        {
            var purchaseRequests = project.PurchaseRequests
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.RequestNumber)
                .ToList();

            batch.Items.Add(new MonthlySettlementItem
            {
                ProjectId = project.Id,
                ParentCaseNumber = project.ParentCaseNumber,
                ProjectNumber = project.ProjectNumber,
                ProjectName = project.Name,
                ProjectPersonnelText = JoinDistinct(project.Assignments.Select(x => DisplayName(x.User))),
                ProgressPercent = project.ProgressPercent,
                ProjectAmount = project.ProjectAmount,
                CollectionPercent = project.CollectionPercent,
                StatusName = project.Status?.Name ?? string.Empty,
                IsClosed = project.Status?.IsClosed ?? false,
                ClosedYearMonth = project.ClosedYearMonth,
                PurchaseRequestSummary = string.Join("; ", purchaseRequests.Select(x => x.RequestNumber)),
                PurchaseAmountTotal = purchaseRequests.Sum(x => x.PurchaseAmount),
                SubCaseContactSummary = JoinDistinct(purchaseRequests.Select(x => DisplayName(x.SubCaseContact))),
                PaymentPercentSummary = string.Join(
                    "; ",
                    purchaseRequests.Select(x => x.PaymentPercent.ToString("0.##", CultureInfo.InvariantCulture) + "%")),
                ActualPaidAmountTotal = purchaseRequests.Sum(x => x.ActualPaidAmount),
                ProgressDescription = project.ProgressDescription,
                UpdatedByUserName = DisplayName(project.UpdatedByUser),
                SourceUpdatedAt = project.UpdatedAt
            });
        }

        db.MonthlySettlementBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateSettlementResult(true, batch.Id, []);
    }

    private static string DisplayName(ApplicationUser? user)
    {
        if (user is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName ?? string.Empty
            : user.DisplayName;
    }

    private static string JoinDistinct(IEnumerable<string> values)
    {
        return string.Join(
            "; ",
            values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x));
    }
}

public sealed record CreateSettlementRequest(
    int Year,
    int Month,
    string CreatedByUserId,
    string? Notes);

public sealed record CreateSettlementResult(
    bool Success,
    int? BatchId,
    IReadOnlyList<string> Errors);
