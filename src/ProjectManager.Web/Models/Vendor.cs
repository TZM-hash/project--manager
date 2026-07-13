namespace ProjectManager.Web.Models;

public sealed class Vendor
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }

    public ICollection<VendorContact> Contacts { get; set; } = [];
}

public sealed class VendorContact
{
    public int Id { get; set; }

    public int VendorId { get; set; }

    public Vendor? Vendor { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }
}