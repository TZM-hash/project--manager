using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class SystemSettingsService(ApplicationDbContext db)
{
    public const string UiEffectsLevelKey = "UiEffectsLevel";
    public const string VisualThemeKey = "VisualTheme";
    public const string DisplayLanguageKey = "DisplayLanguage";
    public const string ArchiveDateKey = "ArchiveDate";

    public enum VisualTheme
    {
        Default = 0,
        ClearGlass = 1
    }

    public enum DisplayLanguage
    {
        TraditionalChinese = 0,
        SimplifiedChinese = 1
    }

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

    public async Task<VisualTheme> GetVisualThemeAsync(CancellationToken cancellationToken)
    {
        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == VisualThemeKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return Enum.TryParse<VisualTheme>(value, ignoreCase: true, out var theme)
            && Enum.IsDefined(theme)
                ? theme
                : VisualTheme.Default;
    }

    public async Task SetVisualThemeAsync(
        VisualTheme theme,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unknown visual theme.");
        }

        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(x => x.Key == VisualThemeKey, cancellationToken);

        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = VisualThemeKey
            };
            db.SystemSettings.Add(setting);
        }

        setting.Value = theme.ToString();
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string ToThemeCssClass(VisualTheme theme)
    {
        return theme switch
        {
            VisualTheme.ClearGlass => "theme-clear-glass",
            _ => "theme-default"
        };
    }

    public async Task<DisplayLanguage> GetDisplayLanguageAsync(CancellationToken cancellationToken)
    {
        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == DisplayLanguageKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return Enum.TryParse<DisplayLanguage>(value, ignoreCase: true, out var lang)
            && Enum.IsDefined(lang)
                ? lang
                : DisplayLanguage.TraditionalChinese;
    }

    public async Task SetDisplayLanguageAsync(
        DisplayLanguage language,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown display language.");
        }

        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(x => x.Key == DisplayLanguageKey, cancellationToken);

        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = DisplayLanguageKey
            };
            db.SystemSettings.Add(setting);
        }

        setting.Value = language.ToString();
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string ToLanguageClass(DisplayLanguage language)
    {
        return language switch
        {
            DisplayLanguage.SimplifiedChinese => "lang-simplified",
            _ => "lang-traditional"
        };
    }

    public async Task<DateOnly> GetArchiveDateAsync(CancellationToken cancellationToken)
    {
        var value = await db.SystemSettings
            .AsNoTracking()
            .Where(x => x.Key == ArchiveDateKey)
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
                ? date
                : DateOnly.FromDateTime(DateTime.Today);
    }

    public async Task SetArchiveDateAsync(
        DateOnly archiveDate,
        CancellationToken cancellationToken)
    {
        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(x => x.Key == ArchiveDateKey, cancellationToken);

        if (setting is null)
        {
            setting = new SystemSetting
            {
                Key = ArchiveDateKey
            };
            db.SystemSettings.Add(setting);
        }

        setting.Value = archiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
