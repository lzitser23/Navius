using Microsoft.AspNetCore.Components.Web;

namespace Navius.Primitives.Components.Sortable;

/// <summary>The layout axis a <c>NaviusSortable</c> reorders along.</summary>
public enum SortableOrientation
{
    /// <summary>A single column; Up/Down (or the engine's vertical midpoints) move items.</summary>
    Vertical,
    /// <summary>A single row; Left/Right (or horizontal midpoints) move items.</summary>
    Horizontal,
    /// <summary>A wrapped grid; the engine picks the nearest cell by 2D distance. Keyboard stays linear (next/prev).</summary>
    Grid,
}

/// <summary>The old/new position of a committed reorder, raised on <c>NaviusSortable.OnReorder</c>.</summary>
public sealed record SortableReorderEventArgs(int OldIndex, int NewIndex);

/// <summary>
/// Shared state for one <c>NaviusSortable</c>. The root owns the ordered key list, the
/// keyboard grab/roving state and the one-shot focus requests; the Item/Handle parts read
/// this surface and route their keydown/focus back through it (mirroring the TagInput
/// conventions). Parts subscribe to <see cref="Changed"/> to re-render when the root mutates
/// grab/roving state without a full value change. Pointer drag visuals
/// (<c>data-dragging</c>/<c>data-drop-target</c>) are painted by the engine directly on the
/// DOM and are never rendered from C#, so a re-render never fights them (the "passthrough").
/// </summary>
public sealed class SortableContext
{
    private readonly Func<string, KeyboardEventArgs, Task> _itemKeyDown;
    private readonly Dictionary<string, string> _labels = new();
    private readonly HashSet<string> _disabledKeys = new();

    public SortableContext(Func<string, KeyboardEventArgs, Task> itemKeyDown)
    {
        _itemKeyDown = itemKeyDown;
    }

    /// <summary>The current order, as a list of item keys.</summary>
    public IReadOnlyList<string> Keys { get; internal set; } = Array.Empty<string>();

    public bool Disabled { get; internal set; }

    public SortableOrientation Orientation { get; internal set; }

    /// <summary>The roving-tabindex active key (the one item that is tabbable), or null before first focus.</summary>
    public string? ActiveKey { get; internal set; }

    /// <summary>The key currently grabbed for keyboard reordering, or null when not grabbing.</summary>
    public string? GrabbedKey { get; internal set; }

    internal bool HasHandle { get; private set; }

    /// <summary>Called by a <c>NaviusSortableItemHandle</c> on init so the root scopes drag to the handle selector.</summary>
    internal void RegisterHandle() => HasHandle = true;

    private string? _pendingFocusKey;

    /// <summary>Queue DOM focus onto the item with <paramref name="key"/> after the next render.</summary>
    internal void RequestFocus(string key) => _pendingFocusKey = key;

    /// <summary>The item with <paramref name="key"/> claims a queued focus request (and clears it).</summary>
    public bool ConsumeFocus(string key)
    {
        if (_pendingFocusKey != key)
        {
            return false;
        }

        _pendingFocusKey = null;
        return true;
    }

    public int IndexOf(string key)
    {
        for (var i = 0; i < Keys.Count; i++)
        {
            if (Keys[i] == key)
            {
                return i;
            }
        }

        return -1;
    }

    public bool IsGrabbed(string key) => GrabbedKey == key;

    public bool IsActive(string key) => ActiveKey == key;

    /// <summary>Record a row's per-item disabled state (used to skip it during roving navigation).</summary>
    internal void SetItemDisabled(string key, bool disabled)
    {
        if (disabled)
        {
            _disabledKeys.Add(key);
        }
        else
        {
            _disabledKeys.Remove(key);
        }
    }

    /// <summary>Whether the row with <paramref name="key"/> is unreachable (the whole list or the row itself is disabled).</summary>
    public bool IsKeyDisabled(string key) => Disabled || _disabledKeys.Contains(key);

    private string? FirstEnabledKey()
    {
        foreach (var key in Keys)
        {
            if (!IsKeyDisabled(key))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>
    /// The single roving tab stop: the active key when it is enabled, otherwise the first
    /// enabled row. Derived (not stored) so a disabled active/seed key never strands Tab with
    /// no reachable item.
    /// </summary>
    public bool IsTabbable(string key)
    {
        if (IsKeyDisabled(key))
        {
            return false;
        }

        if (ActiveKey is not null && !IsKeyDisabled(ActiveKey))
        {
            return ActiveKey == key;
        }

        return FirstEnabledKey() == key;
    }

    /// <summary>Store the accessible label for a key (used by the root's live-region announcements).</summary>
    internal void SetLabel(string key, string label) => _labels[key] = label;

    internal void RemoveLabel(string key) => _labels.Remove(key);

    internal string LabelFor(string key) => _labels.TryGetValue(key, out var label) ? label : key;

    /// <summary>Route an item's keydown to the root reducer (grab / move / drop / cancel single-source).</summary>
    public Task HandleKeyDownAsync(string key, KeyboardEventArgs e) => _itemKeyDown(key, e);

    /// <summary>An item reports it received focus; roving tabindex follows (no-op when already active).</summary>
    public void NotifyFocused(string key)
    {
        if (ActiveKey == key)
        {
            return;
        }

        // Don't let stray focus steal the active key mid-grab; the grabbed item stays active.
        if (GrabbedKey is not null)
        {
            return;
        }

        ActiveKey = key;
        RaiseChanged();
    }

    public event Action? Changed;

    internal void RaiseChanged() => Changed?.Invoke();
}
