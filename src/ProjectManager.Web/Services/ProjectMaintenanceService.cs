using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class ProjectMaintenanceService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<string>> ValidateForSaveAsync(
        Project project,
        IEnumerable<PurchaseRequest> purchaseRequests,
        int? existingProjectId,
        bool statusIsClosed,
        CancellationToken cancellationToken)
    {
        var errors = ProjectRules.ValidateProject(project, statusIsClosed).ToList();

        var duplicateExists = await db.Projects.AnyAsync(
            x => !x.IsDeleted &&
                 x.Year == project.Year &&
                 x.ProjectNumber == project.ProjectNumber &&
                 (existingProjectId == null || x.Id != existingProjectId.Value),
            cancellationToken);

        if (duplicateExists)
        {
            errors.Add("Project number must be unique within the same year.");
        }

        foreach (var request in purchaseRequests)
        {
            errors.AddRange(ProjectRules.ValidatePurchaseRequest(request));
        }

        return errors;
    }
}
