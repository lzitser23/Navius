namespace Navius.Primitives.Components.ContextMenu;

/// <summary>
/// Cascaded by a <see cref="NaviusContextMenuRadioGroup"/> to its
/// <see cref="NaviusContextMenuRadioItem"/> children: holds the selected value and
/// lets an item report selection back. Mirrors the spec ContextMenu.RadioGroup.
/// </summary>
public sealed class ContextMenuRadioGroupContext
{
    private readonly Func<string, Task> _requestSelect;

    public ContextMenuRadioGroupContext(Func<string, Task> requestSelect) => _requestSelect = requestSelect;

    /// <summary>The currently selected radio value.</summary>
    public string? Value { get; private set; }

    public event Action? Changed;

    public bool IsSelected(string value) => Value == value;

    internal void SetValue(string? value)
    {
        if (Value == value)
        {
            return;
        }

        Value = value;
        Changed?.Invoke();
    }

    /// <summary>Ask the group to select <paramref name="value"/> (controlled/uncontrolled handled by the group).</summary>
    public Task RequestSelectAsync(string value) => _requestSelect(value);
}
