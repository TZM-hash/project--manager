using Microsoft.Extensions.Options;

namespace ProjectManager.Web.Services;

public sealed class ProjectCollaborationAttachmentStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
}

public sealed class ProjectCollaborationAttachmentStore(IOptions<ProjectCollaborationAttachmentStorageOptions> options)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".xlsx", ".xls", ".docx", ".doc", ".pptx", ".txt", ".zip"
    };

    public const long MaxLength = 25 * 1024 * 1024;

    private readonly string rootPath = PrepareRoot(options.Value.RootPath);

    public async Task<StoredCollaborationAttachment> SaveAsync(
        string fileName,
        string contentType,
        long length,
        Stream source,
        CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeName);
        if (string.IsNullOrWhiteSpace(safeName) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("不支援的附件格式。請上傳 PDF、圖片、Office、文字或 ZIP 檔案。");
        }

        if (length <= 0 || length > MaxLength)
        {
            throw new InvalidOperationException("附件大小必須介於 1 位元組與 25 MB 之間。");
        }

        Directory.CreateDirectory(rootPath);
        var storedName = $"{Guid.NewGuid():N}-{safeName}";
        var relativePath = storedName;
        var absolutePath = Resolve(relativePath);
        await using var target = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await source.CopyToAsync(target, cancellationToken);
        if (target.Length > MaxLength)
        {
            target.Close();
            File.Delete(absolutePath);
            throw new InvalidOperationException("附件大小不可超過 25 MB。");
        }

        return new StoredCollaborationAttachment(relativePath, safeName, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, target.Length);
    }

    public FileStream OpenRead(string relativePath) => new(Resolve(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

    private string Resolve(string relativePath)
    {
        var absolute = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("附件路徑超出允許範圍。");
        }

        return absolute;
    }

    private static string PrepareRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("尚未設定協作附件目錄。");
        var root = Path.GetFullPath(path);
        Directory.CreateDirectory(root);
        return root;
    }
}

public sealed record StoredCollaborationAttachment(string RelativePath, string OriginalFileName, string ContentType, long Length);
