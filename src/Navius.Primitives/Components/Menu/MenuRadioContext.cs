namespace Navius.Primitives.Components.Menu;

/// <summary>
/// Cascaded from <c>NaviusMenuRadioGroup</c> to its
/// <c>NaviusMenuRadioItem</c> children. Holds the group's selected value and
/// a callback the items invoke to request selection. Items compare their own value to
/// <see cref="Value"/> to derive their checked state, mirroring the spec's RadioGroup.
/// </summary>
public sealed class MenuRadioContext
{
    private readonly Func<string, Task> _select;

    public MenuRadioContext(Func<string, Task> select) => _select = select;

    /// <summary>Currently selected value (null = nothing selected).</summary>
    public string? Value { get; internal set; }

    /// <summary>Raised when the selected value changes so items re-render.</summary>
    public event Action? Changed;

    internal void NotifyChanged() => Changed?.Invoke();

    /// <summary>Called by a RadioItem to request its value become the selected one.</summary>
    public Task SelectAsync(string value) => _select(value);
}
