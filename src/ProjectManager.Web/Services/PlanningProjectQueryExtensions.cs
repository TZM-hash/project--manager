using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public static class PlanningProjectQueryExtensions
{
    public static IQueryable<PlanningProject> WhereLeaderAssigned(
        this IQueryable<PlanningProject> query,
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return query;
        }

        var normalizedUserId = userId.Trim();
        return query.Where(x => x.LeaderUserId != null &&
                                (x.LeaderUserId == normalizedUserId ||
                                 x.LeaderUserId.StartsWith(normalizedUserId + ",") ||
                                 x.LeaderUserId.EndsWith("," + normalizedUserId) ||
                                 x.LeaderUserId.Contains("," + normalizedUserId + ",")));
    }
}
