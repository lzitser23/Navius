namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Cascaded by a <see cref="NaviusMenubarRadioGroup"/> to its
/// <see cref="NaviusMenubarRadioItem"/> children. Holds the group's currently-selected
/// value and the callback that requests a new selection. Mirrors the spec's
/// <c>Menubar.RadioGroup</c> value/onValueChange contract.
/// </summary>
public sealed class MenubarRadioGroupContext
{
    private readonly Func<string, Task> _requestSelect;

    public MenubarRadioGroupContext(Func<string?> getValue, Func<string, Task> requestSelect)
    {
        GetValue = getValue;
        _requestSelect = requestSelect;
    }

    /// <summary>Reads the group's current value (re-evaluated each access).</summary>
    public Func<string?> GetValue { get; }

    /// <summary>True when <paramref name="value"/> is the selected radio value.</summary>
    public bool IsSelected(string value) =>
        GetValue() is { } v && string.Equals(v, value, StringComparison.Ordinal);

    /// <summary>Request that <paramref name="value"/> becomes the selected radio value.</summary>
    public Task SelectAsync(string value) => _requestSelect(value);
}
