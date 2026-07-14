using Microsoft.Extensions.Options;

namespace ProjectManager.Web.Services.Operations;

public sealed class OperationStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
}

public sealed class OperationFileStore(IOptions<OperationStorageOptions> options)
{
    private readonly string rootPath = PrepareRoot(options.Value.RootPath);

    public async Task<OperationStoredFile> SaveAsync(
        string area,
        string fileName,
        Stream source,
        CancellationToken cancellationToken)
    {
        if (area is not ("input" or "output"))
        {
            throw new ArgumentException("不支援的工作檔案區域。", nameof(area));
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "operation-file";
        }

        var directory = Resolve(area);
        Directory.CreateDirectory(directory);
        var storedName = $"{Guid.NewGuid():N}-{safeName}";
        var absolutePath = Resolve($"{area}/{storedName}");
        await using var target = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(target, cancellationToken);
        return new OperationStoredFile($"{area}/{storedName}", safeName, target.Length);
    }

    public FileStream OpenRead(string relativePath)
    {
        var absolutePath = Resolve(relativePath);
        return new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public string Resolve(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(rootPath, normalized));
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(absolutePath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("工作檔案路徑超出允許範圍。");
        }

        return absolutePath;
    }

    private static string PrepareRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException("尚未設定工作檔案根目錄。");
        }

        var root = Path.GetFullPath(configuredRoot);
        Directory.CreateDirectory(root);
        return root;
    }
}

public sealed record OperationStoredFile(string RelativePath, string OriginalFileName, long Length);
