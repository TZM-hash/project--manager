using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class SystemSettingsService(ApplicationDbContext db)
{
    public const string UiEffectsLevelKey = "UiEffectsLevel";

    public async Task<UiEffectsLevel> GetUiEffectsLevelAsync(CancellationToken cancellationToken)
    {
        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == UiEffectsLevelKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return Enum.TryParse<UiEffectsLevel>(value, ignoreCase: true, out var level)
            && Enum.IsDefined(level)
                ? level
                : UiEffectsLevel.Medium;
    }

    public async Task SetUiEffectsLevelAsync(
        UiEffectsLevel level,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown UI effects level.");
        }

        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(x => x.Key == UiEffectsLevelKey, cancellationToken);

        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = UiEffectsLevelKey
            };
            db.SystemSettings.Add(setting);
        }

        setting.Value = level.ToString();
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string ToCssClass(UiEffectsLevel level)
    {
        return level switch
        {
            UiEffectsLevel.Low => "ui-effects-low",
            UiEffectsLevel.High => "ui-effects-high",
            _ => "ui-effects-medium"
        };
    }
}
