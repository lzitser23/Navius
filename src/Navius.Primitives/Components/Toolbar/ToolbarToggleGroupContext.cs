using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Toolbar;

/// <summary>
/// Shared state for a <c>Toolbar.ToggleGroup</c>. Unlike the standalone ToggleGroup
/// primitive this does NOT own a roving-focus controller: its items seat the
/// surrounding toolbar's single Tab stop (via <see cref="ToolbarContext"/>) and the
/// toolbar's roving controller drives arrow navigation. This context only owns the
/// authoritative pressed selection and the single/multiple toggle semantics.
/// </summary>
public sealed class ToolbarToggleGroupContext
{
    private readonly Func<string, Task> _requestToggle;
    private readonly HashSet<string> _value = new(StringComparer.Ordinal);

    public ToolbarToggleGroupContext(
        Func<string, Task> requestToggle,
        string type,
        bool disabled)
    {
        _requestToggle = requestToggle;
        Type = type;
        Disabled = disabled;
    }

    /// <summary><c>"single"</c> (at most one pressed) or <c>"multiple"</c> (many).</summary>
    public string Type { get; }

    /// <summary>When <c>true</c> every item in the group is disabled.</summary>
    public bool Disabled { get; }

    /// <summary>Re-render items when the pressed set changes.</summary>
    public event Func<Task>? Changed;

    public bool IsPressed(string value) => _value.Contains(value);

    /// <summary>Replace the authoritative pressed set and re-render items if it changed.</summary>
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

    /// <summary>Item entry point: ask the group root to toggle <paramref name="value"/>.</summary>
    public Task RequestToggleAsync(string value) => _requestToggle(value);
}
