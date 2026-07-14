using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ProjectManager.Web.Services.Operations;

public sealed class OperationalMonitoringOptions
{
    public string LogRootPath { get; set; } = string.Empty;

    public string DataRootPath { get; set; } = string.Empty;
}

public sealed class ExceptionLogStore(IOptions<OperationalMonitoringOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly string rootPath = PrepareRoot(options.Value.LogRootPath);

    public async Task WriteAsync(
        Exception exception,
        string path,
        string? userId,
        string traceIdentifier,
        CancellationToken cancellationToken)
    {
        var entry = new ExceptionLogEntry(
            DateTimeOffset.UtcNow,
            path,
            userId,
            traceIdentifier,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.StackTrace);
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
        var file = Path.Combine(rootPath, $"exceptions-{entry.Timestamp:yyyyMMdd}.jsonl");
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(file, line, new UTF8Encoding(false), cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<int> CountRecentAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(window);
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(rootPath, "exceptions-*.jsonl"))
        {
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<ExceptionLogEntry>(line, JsonOptions);
                    if (entry?.Timestamp >= cutoff) count++;
                }
                catch (JsonException)
                {
                    // A partial or legacy line does not make monitoring unavailable.
                }
            }
        }

        return count;
    }

    private static string PrepareRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("尚未設定例外日誌目錄。");
        }

        var root = Path.GetFullPath(path);
        Directory.CreateDirectory(root);
        return root;
    }
}

public sealed record ExceptionLogEntry(
    DateTimeOffset Timestamp,
    string Path,
    string? UserId,
    string TraceIdentifier,
    string ExceptionType,
    string Message,
    string? StackTrace);
