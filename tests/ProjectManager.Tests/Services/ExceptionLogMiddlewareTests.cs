using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectManager.Web.Middleware;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Tests.Services;

public sealed class ExceptionLogMiddlewareTests
{
    [Fact]
    public async Task Request_abort_does_not_replace_the_original_exception()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var middleware = CreateMiddleware();
            var context = new DefaultHttpContext
            {
                RequestAborted = new CancellationToken(canceled: true)
            };

            var action = () => middleware.InvokeAsync(context, CreateStore(root));

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("original failure");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Log_write_failure_does_not_replace_the_original_exception()
    {
        var root = CreateTemporaryDirectory();
        var store = CreateStore(root);
        Directory.Delete(root, recursive: true);
        await File.WriteAllTextAsync(root, "blocks the log directory");
        try
        {
            var middleware = CreateMiddleware();
            var action = () => middleware.InvokeAsync(new DefaultHttpContext(), store);

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("original failure");
        }
        finally
        {
            File.Delete(root);
        }
    }

    [Fact]
    public async Task Exception_log_preserves_inner_exception_details()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(root);
            var exception = new InvalidOperationException(
                "outer failure",
                new ApplicationException("database constraint details"));

            await store.WriteAsync(exception, "/test", "user-1", "trace-1", CancellationToken.None);

            var log = await File.ReadAllTextAsync(Directory.GetFiles(root, "exceptions-*.jsonl").Single());
            log.Should().Contain("outer failure");
            log.Should().Contain("database constraint details");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ExceptionLogMiddleware CreateMiddleware() =>
        new(
            _ => throw new InvalidOperationException("original failure"),
            NullLogger<ExceptionLogMiddleware>.Instance);

    private static ExceptionLogStore CreateStore(string root) =>
        new(Options.Create(new OperationalMonitoringOptions
        {
            LogRootPath = root,
            DataRootPath = root
        }));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ProjectManager.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
