namespace Navius.Primitives.Components.Select;

/// <summary>
/// Cascaded by <c>NaviusSelectItem</c> to its child parts (<c>NaviusSelectItemText</c>,
/// <c>NaviusSelectItemIndicator</c>) so they can read the owning item's value and
/// selected state without prop-drilling. Mirrors the spec's SelectItemContext.
/// </summary>
public sealed class SelectItemContext
{
    private readonly SelectContext _select;

    public SelectItemContext(SelectContext select, string value, bool disabled)
    {
        _select = select;
        Value = value;
        Disabled = disabled;
    }

    public string Value { get; }
    public bool Disabled { get; }

    public bool IsSelected => _select.IsSelected(Value);

    /// <summary>
    /// The item registers its rendered label text against its value so the trigger's
    /// <c>NaviusSelectValue</c> can show the human label for the selected key. Called
    /// by <c>NaviusSelectItemText</c> (preferred) or the item itself (fallback).
    /// </summary>
    public void RegisterText(string? text) => _select.RegisterText(Value, text);
}
