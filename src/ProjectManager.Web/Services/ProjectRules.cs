using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public static class ProjectRules
{
    public static IReadOnlyList<string> ValidateProject(Project project, bool statusIsClosed)
    {
        var errors = new List<string>();

        if (project.Year < 2000 || project.Year > 2100)
        {
            errors.Add("Year must be between 2000 and 2100.");
        }

        if (string.IsNullOrWhiteSpace(project.ProjectNumber))
        {
            errors.Add("Project number is required.");
        }

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            errors.Add("Project name is required.");
        }

        if (project.ProjectAmount < 0)
        {
            errors.Add("Project amount cannot be negative.");
        }

        ValidatePercent(project.ProgressPercent, "Progress percent", errors);
        ValidatePercent(project.CollectionPercent, "Collection percent", errors);

        if (statusIsClosed && project.ClosedYearMonth is null)
        {
            errors.Add("Closed year/month is required when project status is closed.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidatePurchaseRequest(PurchaseRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RequestNumber))
        {
            errors.Add("Purchase request number is required.");
        }

        if (request.PurchaseAmount < 0)
        {
            errors.Add("Purchase amount cannot be negative.");
        }

        if (request.ActualPaidAmount < 0)
        {
            errors.Add("Actual paid amount cannot be negative.");
        }

        ValidatePercent(request.PaymentPercent, "Payment percent", errors);
        return errors;
    }

    public static DateOnly? NormalizeClosedYearMonth(DateOnly? value)
    {
        return value is null ? null : new DateOnly(value.Value.Year, value.Value.Month, 1);
    }

    private static void ValidatePercent(decimal value, string label, List<string> errors)
    {
        if (value < 0 || value > 100)
        {
            errors.Add($"{label} must be between 0 and 100.");
        }
    }
}
