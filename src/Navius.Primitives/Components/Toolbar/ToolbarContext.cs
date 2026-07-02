using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Toolbar;

/// <summary>
/// Shared state for a toolbar. The toolbar is a flat container of focusable controls
/// (buttons, links, toggle groups...) that exposes a single Tab stop and moves focus
/// between its items with the arrow keys (roving tabindex, provided by the engine).
/// The context tracks the registered items in DOM order so the first enabled one can
/// seat the initial <c>tabindex=0</c>; the engine takes over once focus moves.
/// </summary>
public sealed class ToolbarContext
{
    private readonly List<ItemEntry> _items = new();

    public ToolbarContext(string orientation)
    {
        Orientation = orientation;
    }

    /// <summary><c>"horizontal"</c> or <c>"vertical"</c>; drives the arrow-key axis.</summary>
    public string Orientation { get; }

    /// <summary>Raised when the seated Tab stop may have changed so parts re-render.</summary>
    public event Func<Task>? Changed;

    /// <summary>Register an item in DOM order with a live disabled probe.</summary>
    public void Register(object key, Func<bool> disabled)
    {
        if (!_items.Any(i => ReferenceEquals(i.Key, key)))
        {
            _items.Add(new ItemEntry(key, disabled));
        }
    }

    public void Unregister(object key) => _items.RemoveAll(i => ReferenceEquals(i.Key, key));

    /// <summary>
    /// True if <paramref name="key"/> should hold the single seat in the Tab order
    /// initially: the first enabled item. The engine's roving controller takes over
    /// once focus moves, so this only governs the resting state.
    /// </summary>
    public bool IsTabStop(object key)
    {
        var seat = _items.FirstOrDefault(i => !i.Disabled());
        return seat is not null && ReferenceEquals(seat.Key, key);
    }

    /// <summary>
    /// Re-render parts after the item set changed (registration order can move the seat).
    /// </summary>
    public Task NotifyChangedAsync() => Changed is null ? Task.CompletedTask : Changed.Invoke();

    private sealed class ItemEntry
    {
        public ItemEntry(object key, Func<bool> disabled)
        {
            Key = key;
            Disabled = disabled;
        }

        public object Key { get; }

        public Func<bool> Disabled { get; }
    }
}
