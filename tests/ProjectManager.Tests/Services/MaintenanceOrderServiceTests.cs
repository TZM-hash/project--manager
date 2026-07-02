using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class MaintenanceOrderServiceTests
{
    [Fact]
    public async Task GetOrdersPageAsync_normalizes_paging_and_returns_totals()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        for (var i = 1; i <= 23; i++)
        {
            db.MaintenanceOrders.Add(CreateOrder($"Customer {i:00}", 2026));
        }
        await db.SaveChangesAsync();
        var service = new MaintenanceOrderService(db);

        var page = await service.GetOrdersPageAsync(
            pageNumber: 0,
            pageSize: 500,
            CancellationToken.None);

        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(20);
        page.TotalCount.Should().Be(23);
        page.TotalPages.Should().Be(2);
        page.Items.Should().HaveCount(20);
    }

    [Fact]
    public async Task DeleteManyAsync_soft_deletes_existing_orders_only()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var first = CreateOrder("First", 2026);
        var second = CreateOrder("Second", 2026);
        db.MaintenanceOrders.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new MaintenanceOrderService(db);

        var deletedCount = await service.DeleteManyAsync([first.Id, second.Id, 999], CancellationToken.None);

        deletedCount.Should().Be(2);
        var remaining = await db.MaintenanceOrders.CountAsync(x => !x.IsDeleted);
        remaining.Should().Be(0);
    }

    private static MaintenanceOrder CreateOrder(string customerName, int year)
    {
        return new MaintenanceOrder
        {
            Year = year,
            CustomerName = customerName,
            MaintenanceStartDate = new DateOnly(year, 1, 1),
            MaintenanceEndDate = new DateOnly(year, 12, 31),
            MaintenanceMethod = MaintenanceMethod.Both,
            OnSiteAnnualCount = 2,
            RemoteAnnualCount = 4,
            HandoverPercent = 30
        };
    }
}
