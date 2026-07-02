namespace ProjectManager.Web.Pages.Shared;

public sealed class PercentBarModel
{
    public PercentBarModel(decimal value, bool compact = true, string? label = null)
    {
        Value = value;
        Compact = compact;
        Label = label;
    }

    public decimal Value { get; }

    public bool Compact { get; }

    public string? Label { get; }
}
