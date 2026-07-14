using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services.DataViews;

namespace ProjectManager.Tests.Services;

public sealed class SavedDataViewServiceTests
{
    [Fact]
    public async Task Save_filters_unknown_keys_and_columns()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        db.Users.Add(CreateUser("user-1"));
        await db.SaveChangesAsync();
        var service = new SavedDataViewService(db, new DataViewRegistry(), TimeProvider.System);

        var saved = await service.SaveAsync(
            "user-1",
            new SaveDataViewCommand(
                "admin-projects",
                "我的檢視",
                new Dictionary<string, string?>
                {
                    ["Name"] = "測試",
                    ["Unknown"] = "discard"
                },
                ["projectNumber", "name", "unknown-column"],
                DataViewRowDensity.Compact,
                false),
            CancellationToken.None);

        saved.Filters.Should().ContainKey("Name").WhoseValue.Should().Be("測試");
        saved.Filters.Should().NotContainKey("Unknown");
        saved.VisibleColumns.Should().Equal("projectNumber", "name", "checkbox", "actions");
    }

    [Fact]
    public async Task Setting_default_clears_previous_default_for_same_user_and_page()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        db.Users.Add(CreateUser("user-1"));
        await db.SaveChangesAsync();
        var service = new SavedDataViewService(db, new DataViewRegistry(), TimeProvider.System);

        await service.SaveAsync("user-1", Command("第一個", true), CancellationToken.None);
        await service.SaveAsync("user-1", Command("第二個", true), CancellationToken.None);

        var views = await service.ListAsync("user-1", "admin-projects", CancellationToken.None);
        views.Count(x => x.IsDefault).Should().Be(1);
        views.Single(x => x.IsDefault).Name.Should().Be("第二個");
    }

    [Fact]
    public async Task Users_cannot_delete_another_users_view()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        db.Users.AddRange(CreateUser("user-1"), CreateUser("user-2"));
        await db.SaveChangesAsync();
        var service = new SavedDataViewService(db, new DataViewRegistry(), TimeProvider.System);
        var saved = await service.SaveAsync("user-1", Command("私人檢視", false), CancellationToken.None);

        var deleted = await service.DeleteAsync("user-2", saved.Id, CancellationToken.None);

        deleted.Should().BeFalse();
        (await db.SavedDataViews.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Unsupported_page_key_is_rejected()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new SavedDataViewService(db, new DataViewRegistry(), TimeProvider.System);

        var action = () => service.ListAsync("user-1", "unknown-page", CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    private static SaveDataViewCommand Command(string name, bool isDefault)
    {
        return new(
            "admin-projects",
            name,
            new Dictionary<string, string?> { ["Name"] = "" },
            ["projectNumber", "name", "status"],
            DataViewRowDensity.Normal,
            isDefault);
    }

    private static ApplicationUser CreateUser(string id)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = id,
            DisplayName = id,
            IsActive = true
        };
    }
}
