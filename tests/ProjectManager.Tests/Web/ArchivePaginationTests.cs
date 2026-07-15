using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;
using ArchiveIndexModel = ProjectManager.Web.Pages.Admin.Archives.IndexModel;

namespace ProjectManager.Tests.Web;

public sealed class ArchivePaginationTests
{
    [Fact]
    public async Task Archive_index_returns_only_the_requested_page()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        for (var index = 1; index <= 25; index++)
        {
            db.ProjectArchives.Add(new ProjectArchive
            {
                OriginalProjectId = index,
                Year = 2026,
                ProjectNumber = $"ARCHIVE-{index:00}",
                Name = $"Archive {index}",
                StatusName = "已結案",
                ArchivedAt = start.AddDays(index),
                OriginalCreatedAt = start,
                OriginalUpdatedAt = start
            });
        }
        await db.SaveChangesAsync();

        var model = new ArchiveIndexModel(db, new ProjectArchiveService(db, TimeProvider.System));
        var pageNumber = typeof(ArchiveIndexModel).GetProperty("PageNumber");
        var pageSize = typeof(ArchiveIndexModel).GetProperty("PageSize");
        var totalCount = typeof(ArchiveIndexModel).GetProperty("TotalCount");
        var totalPages = typeof(ArchiveIndexModel).GetProperty("TotalPages");

        pageNumber.Should().NotBeNull();
        pageSize.Should().NotBeNull();
        totalCount.Should().NotBeNull();
        totalPages.Should().NotBeNull();
        pageNumber!.SetValue(model, 2);
        pageSize!.SetValue(model, 10);

        await model.OnGetAsync(CancellationToken.None);

        model.Archives.Should().HaveCount(10);
        model.Archives.Select(x => x.ProjectNumber).Should().Equal(
            "ARCHIVE-15", "ARCHIVE-14", "ARCHIVE-13", "ARCHIVE-12", "ARCHIVE-11",
            "ARCHIVE-10", "ARCHIVE-09", "ARCHIVE-08", "ARCHIVE-07", "ARCHIVE-06");
        totalCount!.GetValue(model).Should().Be(25);
        totalPages!.GetValue(model).Should().Be(3);
    }
}
