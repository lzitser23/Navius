using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Navius.Primitives.Components.Tree;

/// <summary>
/// One registered <c>role="treeitem"</c>. Items register their live element, disabled
/// state, level and parent (in mount order, which is document order) so the context can
/// compute the visible-node order, roving focus, aria-setsize/posinset and keyboard nav
/// without any per-item bookkeeping in the parts.
/// </summary>
internal sealed class TreeItemReg
{
    public object Value = default!;
    public object? Parent;
    public Func<ElementReference> El = default!;
    public Func<bool> Disabled = default!;
    public Func<int> Level = default!;
    public Func<bool> Expandable = default!;
    public Func<string> Label = default!;
}

/// <summary>
/// Shared state for a <see cref="NaviusTree{TValue}"/> (the full WAI-APG tree contract).
/// The root owns the authoritative selection + expansion sets and pushes them here; items
/// register their element/level/parent so this context can drive roving focus, keyboard
/// navigation (arrows with expand/collapse semantics, Home/End, typeahead, asterisk,
/// multi-select shortcuts) and the aria-level/setsize/posinset math. Values are boxed to
/// <see cref="object"/> so the parts stay non-generic; equality flows through the default
/// comparer (correct for strings and boxed value types alike).
/// </summary>
public sealed class TreeContext
{
    private readonly Func<IReadOnlyCollection<object>, Task> _onSelectionChange;
    private readonly Func<IReadOnlyCollection<object>, Task> _onExpandedChange;

    private readonly HashSet<object> _selected = new();
    private readonly HashSet<object> _expanded = new();
    private readonly List<TreeItemReg> _items = new();

    private object? _active;   // roving-tabindex / activedescendant target
    private object? _anchor;   // multi-select range anchor

    private string _typeBuffer = "";
    private DateTime _typeAt = DateTime.MinValue;

    private const int TypeaheadResetMs = 500;

    public TreeContext(
        Func<IReadOnlyCollection<object>, Task> onSelectionChange,
        Func<IReadOnlyCollection<object>, Task> onExpandedChange)
    {
        _onSelectionChange = onSelectionChange;
        _onExpandedChange = onExpandedChange;
    }

    /// <summary>"none" | "single" | "multiple".</summary>
    public string SelectionMode { get; private set; } = "single";

    /// <summary>"roving" (default, per-item tabindex) | "activedescendant" (container-owned focus).</summary>
    public string FocusMode { get; private set; } = "roving";

    public bool RootDisabled { get; private set; }

    public string Orientation { get; private set; } = "vertical";

    public string Dir { get; private set; } = "ltr";

    public string BaseId { get; } = $"navius-tree-{Guid.NewGuid():N}";

    /// <summary>Fired after selection/expansion/active changes so subscribed parts re-render.</summary>
    public event Func<Task>? Changed;

    public bool IsMulti => SelectionMode == "multiple";
    public bool IsSingle => SelectionMode == "single";
    public bool SelectionEnabled => SelectionMode is "single" or "multiple";
    public bool IsActiveDescendant => FocusMode == "activedescendant";

    // ---- root configuration + source-of-truth sync ---------------------------

    internal void UpdateRoot(string selectionMode, string focusMode, bool disabled, string orientation, string dir)
    {
        SelectionMode = selectionMode;
        FocusMode = focusMode;
        RootDisabled = disabled;
        Orientation = orientation;
        Dir = dir;
    }

    internal async Task SetSelectionInternalAsync(IEnumerable<object> values)
    {
        var next = new HashSet<object>(values);
        if (next.SetEquals(_selected))
        {
            return;
        }

        _selected.Clear();
        foreach (var v in next)
        {
            _selected.Add(v);
        }

        await RaiseChangedAsync();
    }

    internal async Task SetExpandedInternalAsync(IEnumerable<object> values)
    {
        var next = new HashSet<object>(values);
        if (next.SetEquals(_expanded))
        {
            return;
        }

        _expanded.Clear();
        foreach (var v in next)
        {
            _expanded.Add(v);
        }

        await RaiseChangedAsync();
    }

    // ---- state queries -------------------------------------------------------

    public bool IsSelected(object value) => _selected.Contains(value);

    public bool IsExpanded(object value) => _expanded.Contains(value);

    public bool IsItemDisabled(bool itemDisabled) => RootDisabled || itemDisabled;

    public bool IsTabbable(object value)
    {
        if (IsActiveDescendant)
        {
            return false; // the container owns tabindex=0 in activedescendant mode
        }

        var visible = VisibleOrder();
        if (visible.Count == 0)
        {
            return false;
        }

        return Equals(ActiveOrDefault(visible), value);
    }

    public bool IsActive(object value) => _active is not null && Equals(_active, value);

    /// <summary>
    /// Sync the roving/active node to whatever actually received DOM focus (tab-in, a click that
    /// lands on the node, or programmatic focus), so the next key acts from the visibly focused
    /// node. No-ops when already active or disabled.
    /// </summary>
    public async Task SetActiveFromFocusAsync(object value)
    {
        if (_active is not null && Equals(_active, value))
        {
            return;
        }

        if (IsDisabledValue(value))
        {
            return;
        }

        _active = value;
        await RaiseChangedAsync();
    }

    /// <summary>The DOM id used for aria-activedescendant / aria-owns wiring.</summary>
    public string ItemId(object value) => $"{BaseId}-item-{Slug(value)}";

    public string GroupId(object value) => $"{BaseId}-group-{Slug(value)}";

    /// <summary>The activedescendant id (null in roving mode or before anything is active).</summary>
    public string? ActiveDescendantId
    {
        get
        {
            if (!IsActiveDescendant)
            {
                return null;
            }

            var visible = VisibleOrder();
            if (visible.Count == 0)
            {
                return null;
            }

            return ItemId(ActiveOrDefault(visible));
        }
    }

    // ---- aria-setsize / aria-posinset ---------------------------------------

    /// <summary>Number of siblings (including this one) under the same parent, in the mounted subtree.</summary>
    public int SetSize(object? parent) => _items.Count(i => Equals(i.Parent, parent));

    /// <summary>1-based position of <paramref name="value"/> among its mounted siblings.</summary>
    public int PosInSet(object value, object? parent)
    {
        var index = _items.Where(i => Equals(i.Parent, parent)).ToList().FindIndex(i => Equals(i.Value, value));
        return index < 0 ? 1 : index + 1;
    }

    // ---- registration --------------------------------------------------------

    internal void Register(TreeItemReg reg)
    {
        if (!_items.Any(i => Equals(i.Value, reg.Value)))
        {
            _items.Add(reg);
        }
    }

    public void Unregister(object value)
    {
        _items.RemoveAll(i => Equals(i.Value, value));
        if (_active is not null && Equals(_active, value))
        {
            _active = null;
        }
    }

    // ---- pointer entry points (click) ---------------------------------------

    /// <summary>Click/Enter activate: toggle expansion on a parent, then apply selection.</summary>
    public async Task ActivateAsync(object value)
    {
        if (IsDisabledValue(value))
        {
            return;
        }

        _active = value;
        _anchor = value;

        var didExpand = false;
        if (ExpandableOf(value))
        {
            await ToggleExpandedAsync(value);
            didExpand = true;
        }

        if (IsSingle)
        {
            await ReplaceSelectionAsync(value);
        }
        else if (IsMulti)
        {
            await ToggleSelectionAsync(value);
        }

        if (!didExpand)
        {
            await RaiseChangedAsync();
        }

        // A pointer activation (click) must also move roving focus onto the node.
        await FocusValueAsync(value);
    }

    // ---- keyboard ------------------------------------------------------------

    /// <summary>
    /// The single keyboard handler for the whole tree (the root wires it on the
    /// <c>role="tree"</c> element so it works in both roving and activedescendant modes).
    /// Returns true when the key was handled (the caller may then stop propagation).
    /// </summary>
    public async Task<bool> HandleKeyDownAsync(KeyboardEventArgs e)
    {
        var visible = VisibleOrder();
        if (visible.Count == 0)
        {
            return false;
        }

        var current = ActiveOrDefault(visible);
        var key = e.Key;

        // Reading-direction-aware horizontal mapping (expand vs collapse).
        var rtl = string.Equals(Dir, "rtl", StringComparison.OrdinalIgnoreCase);
        var expandKey = rtl ? "ArrowLeft" : "ArrowRight";
        var collapseKey = rtl ? "ArrowRight" : "ArrowLeft";

        switch (key)
        {
            case "ArrowDown":
                if (IsMulti && e.ShiftKey)
                {
                    await MoveAndExtendAsync(current, visible, +1);
                }
                else
                {
                    await MoveAsync(current, visible, +1);
                }

                return true;

            case "ArrowUp":
                if (IsMulti && e.ShiftKey)
                {
                    await MoveAndExtendAsync(current, visible, -1);
                }
                else
                {
                    await MoveAsync(current, visible, -1);
                }

                return true;

            case "Home":
                if (IsMulti && e.CtrlKey && e.ShiftKey)
                {
                    await SelectRangeToEdgeAsync(current, visible, last: false);
                }
                else
                {
                    await FocusAtAsync(visible, 0);
                }

                return true;

            case "End":
                if (IsMulti && e.CtrlKey && e.ShiftKey)
                {
                    await SelectRangeToEdgeAsync(current, visible, last: true);
                }
                else
                {
                    await FocusAtAsync(visible, visible.Count - 1);
                }

                return true;

            case "Enter":
                await ActivateAsync(current);
                return true;

            case " ":
                if (IsMulti && e.ShiftKey)
                {
                    await SelectContiguousAsync(current, visible);
                }
                else
                {
                    await SelectFocusedAsync(current);
                }

                return true;

            case "*":
                await ExpandSiblingsAsync(current);
                return true;
        }

        if (key == expandKey)
        {
            await ExpandOrIntoChildAsync(current, visible);
            return true;
        }

        if (key == collapseKey)
        {
            await CollapseOrToParentAsync(current);
            return true;
        }

        // Ctrl+A / Ctrl+a: select or deselect all (multi-select only).
        if (IsMulti && (key == "a" || key == "A") && (e.CtrlKey || e.MetaKey))
        {
            await ToggleSelectAllAsync(visible);
            return true;
        }

        // Type-ahead: a single printable char (Shift allowed for capitals).
        if (key.Length == 1 && key != " " && !e.CtrlKey && !e.AltKey && !e.MetaKey)
        {
            await TypeaheadAsync(key, current, visible);
            return true;
        }

        return false;
    }

    // ---- navigation ----------------------------------------------------------

    private async Task MoveAsync(object current, IReadOnlyList<object> visible, int delta)
    {
        var index = IndexOfEnabled(visible, current);
        var next = NextEnabledIndex(visible, index, delta);
        if (next >= 0)
        {
            await FocusAtAsync(visible, next);
        }
    }

    private async Task MoveAndExtendAsync(object current, IReadOnlyList<object> visible, int delta)
    {
        var index = IndexOfEnabled(visible, current);
        var next = NextEnabledIndex(visible, index, delta);
        if (next < 0)
        {
            return;
        }

        var target = visible[next];
        _active = target;
        await ToggleSelectionAsync(target); // extend + toggle (raises Changed)
        await FocusValueAsync(target);
    }

    private async Task FocusAtAsync(IReadOnlyList<object> visible, int index)
    {
        if (index < 0 || index >= visible.Count)
        {
            return;
        }

        // Home scans forward, End scans backward, an interior (already-enabled) index resolves to
        // itself; the reversed fallback covers a disabled edge with everything beyond it disabled.
        var dir = index <= 0 ? +1 : -1;
        var target = FirstEnabledFrom(visible, index, dir) ?? FirstEnabledFrom(visible, index, -dir);
        if (target is null)
        {
            return;
        }

        _active = target;
        await RaiseChangedAsync();
        await FocusValueAsync(target);
    }

    private async Task ExpandOrIntoChildAsync(object current, IReadOnlyList<object> visible)
    {
        if (!ExpandableOf(current))
        {
            return; // leaf: nothing
        }

        if (!IsExpanded(current))
        {
            await ExpandAsync(current); // collapsed parent: expand in place
            return;
        }

        // Expanded parent: move to first (enabled) child, which is the next visible node.
        var index = visible.ToList().FindIndex(v => Equals(v, current));
        if (index >= 0 && index + 1 < visible.Count && Equals(ParentOf(visible[index + 1]), current))
        {
            var next = NextEnabledIndex(visible, index, +1);
            if (next >= 0 && Equals(ParentOf(visible[next]), current))
            {
                await FocusAtAsync(visible, next);
            }
        }
    }

    private async Task CollapseOrToParentAsync(object current)
    {
        if (ExpandableOf(current) && IsExpanded(current))
        {
            await CollapseAsync(current); // expanded parent: collapse in place
            return;
        }

        var parent = ParentOf(current);
        if (parent is not null && !IsDisabledValue(parent))
        {
            _active = parent;
            await RaiseChangedAsync();
            await FocusValueAsync(parent);
        }
    }

    private async Task ExpandSiblingsAsync(object current)
    {
        var parent = ParentOf(current);
        var siblings = _items.Where(i => Equals(i.Parent, parent) && i.Expandable()).Select(i => i.Value).ToList();
        if (siblings.Count == 0)
        {
            return;
        }

        var next = new HashSet<object>(_expanded);
        foreach (var s in siblings)
        {
            next.Add(s);
        }

        await _onExpandedChange(next);
    }

    private async Task TypeaheadAsync(string ch, object current, IReadOnlyList<object> visible)
    {
        var now = DateTime.UtcNow;
        if ((now - _typeAt).TotalMilliseconds > TypeaheadResetMs)
        {
            _typeBuffer = "";
        }

        _typeAt = now;
        _typeBuffer += ch;

        // Same char repeated: cycle through matches on the first letter instead.
        var query = _typeBuffer;
        if (_typeBuffer.Length > 1 && _typeBuffer.Distinct().Count() == 1)
        {
            query = _typeBuffer[..1];
        }

        var start = visible.ToList().FindIndex(v => Equals(v, current));
        var count = visible.Count;

        // Search from the node AFTER current, wrapping, so repeated presses advance.
        for (var step = 1; step <= count; step++)
        {
            var idx = ((start < 0 ? -1 : start) + step) % count;
            var value = visible[idx];
            if (IsDisabledValue(value))
            {
                continue;
            }

            var label = LabelOf(value);
            if (label.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                _active = value;
                await RaiseChangedAsync();
                await FocusValueAsync(value);
                return;
            }
        }
    }

    // ---- expansion mutation --------------------------------------------------

    private Task ExpandAsync(object value)
    {
        if (_expanded.Contains(value))
        {
            return Task.CompletedTask;
        }

        var next = new HashSet<object>(_expanded) { value };
        return _onExpandedChange(next);
    }

    private Task CollapseAsync(object value)
    {
        if (!_expanded.Contains(value))
        {
            return Task.CompletedTask;
        }

        var next = new HashSet<object>(_expanded);
        next.Remove(value);
        return _onExpandedChange(next);
    }

    public Task ToggleExpandedAsync(object value)
        => _expanded.Contains(value) ? CollapseAsync(value) : ExpandAsync(value);

    // ---- selection mutation --------------------------------------------------

    private async Task SelectFocusedAsync(object value)
    {
        if (!SelectionEnabled || IsDisabledValue(value))
        {
            return;
        }

        _anchor = value;
        if (IsSingle)
        {
            await ReplaceSelectionAsync(value);
        }
        else
        {
            await ToggleSelectionAsync(value);
        }
    }

    public async Task SelectAsync(object value)
    {
        if (!SelectionEnabled || IsDisabledValue(value))
        {
            return;
        }

        _active = value;
        _anchor = value;
        if (IsSingle)
        {
            await ReplaceSelectionAsync(value);
        }
        else
        {
            await ToggleSelectionAsync(value);
        }
    }

    private Task ReplaceSelectionAsync(object value)
    {
        if (IsDisabledValue(value))
        {
            return Task.CompletedTask;
        }

        return _onSelectionChange(new[] { value });
    }

    private Task ToggleSelectionAsync(object value)
    {
        if (IsDisabledValue(value))
        {
            return Task.CompletedTask;
        }

        var next = new HashSet<object>(_selected);
        if (!next.Remove(value))
        {
            next.Add(value);
        }

        return _onSelectionChange(next);
    }

    private async Task SelectContiguousAsync(object current, IReadOnlyList<object> visible)
    {
        var anchor = _anchor is not null && visible.Any(v => Equals(v, _anchor)) ? _anchor : current;
        await SelectSpanAsync(anchor, current, visible);
    }

    private async Task SelectRangeToEdgeAsync(object current, IReadOnlyList<object> visible, bool last)
    {
        var edgeIndex = last ? visible.Count - 1 : 0;
        var target = FirstEnabledFrom(visible, edgeIndex, last ? -1 : +1);
        if (target is null)
        {
            return;
        }

        await SelectSpanAsync(current, target, visible);
        _active = target;
        await FocusValueAsync(target);
    }

    private async Task SelectSpanAsync(object from, object to, IReadOnlyList<object> visible)
    {
        var i = visible.ToList().FindIndex(v => Equals(v, from));
        var j = visible.ToList().FindIndex(v => Equals(v, to));
        if (i < 0 || j < 0)
        {
            return;
        }

        var (lo, hi) = i <= j ? (i, j) : (j, i);
        var next = new HashSet<object>(_selected);
        for (var k = lo; k <= hi; k++)
        {
            if (!IsDisabledValue(visible[k]))
            {
                next.Add(visible[k]);
            }
        }

        await _onSelectionChange(next);
    }

    private async Task ToggleSelectAllAsync(IReadOnlyList<object> visible)
    {
        var selectable = visible.Where(v => !IsDisabledValue(v)).ToList();
        var allSelected = selectable.Count > 0 && selectable.All(_selected.Contains);
        await _onSelectionChange(allSelected ? Array.Empty<object>() : selectable);
    }

    // ---- shared helpers ------------------------------------------------------

    /// <summary>Pre-order DFS of the mounted subtree, descending only into expanded nodes.</summary>
    public IReadOnlyList<object> VisibleOrder()
    {
        var result = new List<object>();
        Walk(null);
        return result;

        void Walk(object? parent)
        {
            foreach (var item in _items.Where(i => Equals(i.Parent, parent)))
            {
                result.Add(item.Value);
                if (IsExpanded(item.Value))
                {
                    Walk(item.Value);
                }
            }
        }
    }

    private object ActiveOrDefault(IReadOnlyList<object> visible)
    {
        if (_active is not null && visible.Any(v => Equals(v, _active)) && !IsDisabledValue(_active))
        {
            return _active;
        }

        var selected = visible.FirstOrDefault(v => _selected.Contains(v) && !IsDisabledValue(v));
        if (selected is not null)
        {
            return selected;
        }

        return visible.FirstOrDefault(v => !IsDisabledValue(v)) ?? visible[0];
    }

    private int IndexOfEnabled(IReadOnlyList<object> visible, object value)
    {
        var idx = visible.ToList().FindIndex(v => Equals(v, value));
        return idx;
    }

    private int NextEnabledIndex(IReadOnlyList<object> visible, int from, int delta)
    {
        var i = from;
        while (true)
        {
            i += delta;
            if (i < 0 || i >= visible.Count)
            {
                return -1; // APG tree: Up/Down do not wrap
            }

            if (!IsDisabledValue(visible[i]))
            {
                return i;
            }
        }
    }

    private object? FirstEnabledFrom(IReadOnlyList<object> visible, int start, int delta)
    {
        for (var i = start; i >= 0 && i < visible.Count; i += delta)
        {
            if (!IsDisabledValue(visible[i]))
            {
                return visible[i];
            }
        }

        return null;
    }

    private async Task FocusValueAsync(object value)
    {
        if (IsActiveDescendant)
        {
            return; // container keeps focus; aria-activedescendant already updated via Changed
        }

        var reg = _items.FirstOrDefault(i => Equals(i.Value, value));
        if (reg is null)
        {
            return;
        }

        try
        {
            await reg.El().FocusAsync();
        }
        catch
        {
            // element not ready / disposed, ignore
        }
    }

    private async Task RaiseChangedAsync()
    {
        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public bool ExpandableOf(object value)
        => _items.FirstOrDefault(i => Equals(i.Value, value))?.Expandable() ?? false;

    public object? ParentOf(object value)
        => _items.FirstOrDefault(i => Equals(i.Value, value))?.Parent;

    private bool IsDisabledValue(object value)
    {
        var reg = _items.FirstOrDefault(i => Equals(i.Value, value));
        return reg is not null && IsItemDisabled(reg.Disabled());
    }

    private string LabelOf(object value)
    {
        var reg = _items.FirstOrDefault(i => Equals(i.Value, value));
        return reg?.Label() ?? value.ToString() ?? "";
    }

    private static string Slug(object value)
    {
        var s = value.ToString() ?? "";
        return new string(s.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
    }
}
