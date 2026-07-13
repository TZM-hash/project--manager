using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class SystemSettingsServiceTests
{
    [Fact]
    public async Task GetVisualThemeAsync_returns_default_when_setting_is_missing()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new SystemSettingsService(db);

        var theme = await service.GetVisualThemeAsync(CancellationToken.None);

        theme.Should().Be(SystemSettingsService.VisualTheme.Default);
        SystemSettingsService.ToThemeCssClass(theme).Should().Be("theme-default");
    }

    [Fact]
    public async Task SetVisualThemeAsync_persists_clear_glass_theme()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new SystemSettingsService(db);

        await service.SetVisualThemeAsync(SystemSettingsService.VisualTheme.ClearGlass, CancellationToken.None);

        (await service.GetVisualThemeAsync(CancellationToken.None))
            .Should().Be(SystemSettingsService.VisualTheme.ClearGlass);
        db.SystemSettings.Should().ContainSingle(x => x.Key == SystemSettingsService.VisualThemeKey);
    }

    [Fact]
    public async Task GetUiEffectsLevelAsync_returns_medium_when_setting_is_missing()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new SystemSettingsService(db);

        var level = await service.GetUiEffectsLevelAsync(CancellationToken.None);

        level.Should().Be(UiEffectsLevel.Medium);
    }

    [Fact]
    public async Task SetUiEffectsLevelAsync_persists_level_and_updates_existing_setting()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new SystemSettingsService(db);

        await service.SetUiEffectsLevelAsync(UiEffectsLevel.High, CancellationToken.None);
        await service.SetUiEffectsLevelAsync(UiEffectsLevel.Low, CancellationToken.None);

        var level = await service.GetUiEffectsLevelAsync(CancellationToken.None);
        level.Should().Be(UiEffectsLevel.Low);
        db.SystemSettings.Should().ContainSingle(x => x.Key == SystemSettingsService.UiEffectsLevelKey);
    }

    [Fact]
    public async Task SetArchiveDateAsync_persists_global_gantt_archive_date()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new SystemSettingsService(db);

        await service.SetArchiveDateAsync(new DateOnly(2026, 8, 15), CancellationToken.None);

        (await service.GetArchiveDateAsync(CancellationToken.None)).Should().Be(new DateOnly(2026, 8, 15));
        db.SystemSettings.Should().ContainSingle(x => x.Key == SystemSettingsService.ArchiveDateKey);
    }
}
