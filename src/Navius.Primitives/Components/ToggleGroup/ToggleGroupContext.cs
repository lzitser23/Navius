using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ToggleGroup;

/// <summary>
/// Shared state for a toggle group. The root owns the authoritative selection and
/// pushes it here; items read <see cref="IsPressed"/> and request toggles. In
/// <c>"single"</c> mode at most one value is pressed (radio-like); in
/// <c>"multiple"</c> mode any number can be pressed.
/// </summary>
public sealed class ToggleGroupContext
{
    private readonly Func<string, Task> _requestToggle;
    private readonly HashSet<string> _value = new(StringComparer.Ordinal);
    private readonly List<(string Value, Func<bool> Disabled)> _items = new();

    public ToggleGroupContext(
        Func<string, Task> requestToggle,
        bool multiple,
        string orientation,
        bool disabled,
        bool rovingFocus = true)
    {
        _requestToggle = requestToggle;
        Multiple = multiple;
        Orientation = orientation;
        Disabled = disabled;
        RovingFocus = rovingFocus;
    }

    /// <summary>When <c>true</c>, multiple items can be pressed at once; default is single (radio-like).</summary>
    public bool Multiple { get; }

    /// <summary><c>"horizontal"</c> or <c>"vertical"</c>.</summary>
    public string Orientation { get; }

    /// <summary>When <c>true</c> every item is disabled regardless of its own flag.</summary>
    public bool Disabled { get; }

    /// <summary>
    /// When <c>true</c> (default) the group has a single roving Tab stop; when
    /// <c>false</c> every enabled item is a Tab stop (tabindex=0) and arrows are off.
    /// </summary>
    public bool RovingFocus { get; }

    public event Func<Task>? Changed;

    public bool IsPressed(string value) => _value.Contains(value);

    public void Register(string value, Func<bool> disabled)
    {
        if (!_items.Any(i => i.Value == value))
        {
            _items.Add((value, disabled));
        }
    }

    public void Unregister(string value) => _items.RemoveAll(i => i.Value == value);

    /// <summary>
    /// True if <paramref name="value"/> should hold the single seat in the Tab order
    /// initially: the first pressed-and-enabled item, else the first enabled item.
    /// The engine's roving controller takes over once focus moves.
    /// </summary>
    public bool IsTabStop(string value)
    {
        var enabled = _items.Where(i => !i.Disabled()).Select(i => i.Value).ToList();
        if (enabled.Count == 0)
        {
            return false;
        }

        // rovingFocus=false: every enabled item is in the Tab order (tabindex=0).
        if (!RovingFocus)
        {
            return enabled.Contains(value, StringComparer.Ordinal);
        }

        var firstPressed = enabled.FirstOrDefault(v => _value.Contains(v));
        var seat = firstPressed ?? enabled[0];
        return string.Equals(seat, value, StringComparison.Ordinal);
    }

    /// <summary>Replace the authoritative pressed set and re-render parts if it changed.</summary>
    internal async Task SetValueInternalAsync(IEnumerable<string> values)
    {
        var next = new HashSet<string>(values, StringComparer.Ordinal);
        if (next.SetEquals(_value))
        {
            return;
        }

        _value.Clear();
        foreach (var v in next)
        {
            _value.Add(v);
        }

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    /// <summary>Item entry point: ask the root to toggle <paramref name="value"/>.</summary>
    public Task RequestToggleAsync(string value) => _requestToggle(value);
}
