using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class SystemSettingsServiceTests
{
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
}
