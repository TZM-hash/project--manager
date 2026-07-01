using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;

namespace ProjectManager.Web.Services;

public sealed class StatusMaintenanceService(ApplicationDbContext db)
{
    public async Task<StatusDeleteResult> DeleteAsync(int statusId, CancellationToken cancellationToken)
    {
        var status = await db.ProjectStatuses
            .Include(x => x.Style)
            .SingleOrDefaultAsync(x => x.Id == statusId, cancellationToken);

        if (status is null)
        {
            return new StatusDeleteResult(false, ["Status was not found."]);
        }

        var isUsed = await db.Projects.AnyAsync(
            x => !x.IsDeleted && x.StatusId == statusId,
            cancellationToken);

        if (isUsed)
        {
            return new StatusDeleteResult(
                false,
                ["Status is currently used by projects and cannot be deleted."]);
        }

        if (status.Style is not null)
        {
            db.ProjectStatusStyles.Remove(status.Style);
        }

        db.ProjectStatuses.Remove(status);
        await db.SaveChangesAsync(cancellationToken);
        return new StatusDeleteResult(true, []);
    }
}

public sealed record StatusDeleteResult(bool Success, IReadOnlyList<string> Errors);
