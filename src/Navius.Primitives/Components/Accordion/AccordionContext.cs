using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Accordion;

/// <summary>
/// Shared state for an accordion. Type "single" keeps at most one item open
/// (honouring <see cref="Collapsible"/>); "multiple" allows many. Ids are derived
/// from the item value so trigger ↔ content wiring needs no registration. Triggers
/// register their element + disabled state (in DOM order) so arrow-key roving focus
/// can move between them.
/// </summary>
public sealed class AccordionContext
{
    private readonly HashSet<string> _open = new();
    private readonly Func<IReadOnlyCollection<string>, Task> _onChange;
    private readonly List<(string Value, Func<ElementReference> El, Func<bool> Disabled)> _triggers = new();

    public AccordionContext(
        string type,
        bool collapsible,
        bool disabled,
        string orientation,
        string dir,
        Func<IReadOnlyCollection<string>, Task> onChange)
    {
        Type = type;
        Collapsible = collapsible;
        RootDisabled = disabled;
        Orientation = orientation;
        Dir = dir;
        _onChange = onChange;
    }

    public string Type { get; private set; }
    public bool Collapsible { get; private set; }
    public bool RootDisabled { get; private set; }
    public string Orientation { get; private set; }
    public string Dir { get; private set; }
    public string BaseId { get; } = $"navius-accordion-{Guid.NewGuid():N}";

    /// <summary>Fired after the open-set changes; the root re-renders parts and surfaces ValueChanged.</summary>
    public event Func<Task>? Changed;

    public bool IsOpen(string value) => _open.Contains(value);

    public string DataOrientation => Orientation == "horizontal" ? "horizontal" : "vertical";

    public string TriggerId(string value) => $"{BaseId}-trigger-{Slug(value)}";
    public string ContentId(string value) => $"{BaseId}-content-{Slug(value)}";

    /// <summary>Whether an item is disabled (root-disabled cascades to every item).</summary>
    public bool IsItemDisabled(bool itemDisabled) => RootDisabled || itemDisabled;

    internal void UpdateRoot(string type, bool collapsible, bool disabled, string orientation, string dir)
    {
        Type = type;
        Collapsible = collapsible;
        RootDisabled = disabled;
        Orientation = orientation;
        Dir = dir;
    }

    /// <summary>Replace the open-set from the controlled/uncontrolled source of truth without re-emitting.</summary>
    internal async Task SetOpenInternalAsync(IEnumerable<string> values)
    {
        var next = new HashSet<string>(values);
        if (next.SetEquals(_open))
        {
            return;
        }

        _open.Clear();
        foreach (var v in next)
        {
            _open.Add(v);
        }

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    /// <summary>
    /// Compute the next open-set for toggling <paramref name="value"/> and hand it to the root,
    /// which decides controlled vs uncontrolled and surfaces ValueChanged. Disabled items no-op.
    /// </summary>
    public async Task ToggleAsync(string value, bool itemDisabled)
    {
        if (IsItemDisabled(itemDisabled))
        {
            return;
        }

        IReadOnlyCollection<string> next;

        if (Type == "single")
        {
            if (_open.Contains(value))
            {
                // Collapsible=false (the spec default): the only open item cannot close itself.
                next = Collapsible ? Array.Empty<string>() : new[] { value };
            }
            else
            {
                next = new[] { value };
            }
        }
        else
        {
            var set = new HashSet<string>(_open);
            if (!set.Remove(value))
            {
                set.Add(value);
            }

            next = set;
        }

        await _onChange(next);
    }

    // ---- Roving focus registration (DOM order) ----

    public void Register(string value, Func<ElementReference> el, Func<bool> disabled)
    {
        if (!_triggers.Any(t => t.Value == value))
        {
            _triggers.Add((value, el, disabled));
        }
    }

    public void Unregister(string value) => _triggers.RemoveAll(t => t.Value == value);

    /// <summary>Zero-based DOM-order index of an item value (−1 if its trigger hasn't registered), for data-index.</summary>
    public int IndexOf(string value) => _triggers.FindIndex(t => t.Value == value);

    /// <summary>
    /// Move focus between triggers in response to an arrow / Home / End key. Honours
    /// orientation + dir, skips disabled triggers, and wraps around (the spec loops).
    /// </summary>
    public async Task HandleKeyDownAsync(string current, string key)
    {
        int dir;
        switch (key)
        {
            case "Home":
                await FocusEdgeAsync(last: false);
                return;
            case "End":
                await FocusEdgeAsync(last: true);
                return;
            case "ArrowDown" when Orientation != "horizontal":
            case "ArrowUp" when Orientation != "horizontal":
                dir = key == "ArrowDown" ? 1 : -1;
                break;
            case "ArrowRight" when Orientation == "horizontal":
            case "ArrowLeft" when Orientation == "horizontal":
                var forward = key == "ArrowRight";
                if (Dir == "rtl")
                {
                    forward = !forward;
                }

                dir = forward ? 1 : -1;
                break;
            default:
                return;
        }

        await MoveAsync(current, dir);
    }

    private async Task MoveAsync(string current, int delta)
    {
        var enabled = _triggers.Where(t => !IsItemDisabled(t.Disabled())).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        var cur = enabled.FindIndex(t => t.Value == current);
        if (cur < 0)
        {
            cur = 0;
        }

        var target = enabled[(cur + delta + enabled.Count) % enabled.Count];
        await FocusAsync(target);
    }

    private async Task FocusEdgeAsync(bool last)
    {
        var enabled = _triggers.Where(t => !IsItemDisabled(t.Disabled())).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        await FocusAsync(last ? enabled[^1] : enabled[0]);
    }

    private static async Task FocusAsync((string Value, Func<ElementReference> El, Func<bool> Disabled) t)
    {
        try { await t.El().FocusAsync(); }
        catch { /* element not ready / disposed */ }
    }

    private static string Slug(string value) =>
        new string(value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
}
