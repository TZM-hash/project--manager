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

        // 專案工號按年度唯一；編輯时需要排除当前專案自身。
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
            // 表单可能一次提交多筆请购，逐筆校验能把所有錯誤一次性返回给頁面。
            errors.AddRange(ProjectRules.ValidatePurchaseRequest(request));
        }

        return errors;
    }
}
