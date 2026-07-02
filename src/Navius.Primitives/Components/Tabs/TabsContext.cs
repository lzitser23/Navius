using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Tabs;

/// <summary>
/// Shared state for a tab group. Ids are derived deterministically from the tab
/// value (so trigger ↔ panel wiring needs no registration); triggers register
/// their element + disabled state for keyboard navigation.
/// </summary>
public sealed class TabsContext
{
    private readonly Func<string, Task> _select;
    private readonly List<(string Value, Func<ElementReference> El, Func<bool> Disabled)> _triggers = new();

    public TabsContext(Func<string, Task> select, string orientation, string activationMode, string dir)
    {
        _select = select;
        Orientation = orientation;
        ActivationMode = activationMode;
        Dir = dir;
    }

    public string BaseId { get; } = $"navius-tabs-{Guid.NewGuid():N}";
    public string Orientation { get; }
    public string ActivationMode { get; }
    public string Dir { get; }

    /// <summary>
    /// Whether arrow-key navigation wraps at the edges. Owned by the List part
    /// (the spec's <c>loop</c> prop lives on <c>Tabs.List</c>), so it is set after
    /// construction. Defaults to true to match the spec.
    /// </summary>
    public bool Loop { get; set; } = true;
    public string? Selected { get; private set; }

    /// <summary>Direction of the last selection change (left|right|up|down|none), for data-activation-direction.</summary>
    public string ActivationDirection { get; private set; } = "none";

    /// <summary>
    /// The value of the trigger that currently holds DOM focus. Drives the roving
    /// tabindex in manual activation mode (focus can lead selection). Null until a
    /// trigger is focused via the keyboard.
    /// </summary>
    public string? Focused { get; private set; }

    public event Func<Task>? Changed;

    public string TriggerId(string value) => $"{BaseId}-trigger-{Slug(value)}";
    public string PanelId(string value) => $"{BaseId}-panel-{Slug(value)}";

    /// <summary>Zero-based DOM-order index of a tab value (−1 if unknown), for data-index.</summary>
    public int IndexOf(string? value) => value is null ? -1 : _triggers.FindIndex(t => t.Value == value);

    public void Register(string value, Func<ElementReference> el, Func<bool> disabled)
    {
        if (!_triggers.Any(t => t.Value == value))
        {
            _triggers.Add((value, el, disabled));
        }
    }

    public void Unregister(string value)
    {
        _triggers.RemoveAll(t => t.Value == value);
        if (Focused == value)
        {
            Focused = null;
        }
    }

    /// <summary>
    /// The trigger that should be in the Tab sequence (tabindex=0). In automatic
    /// mode this tracks selection; in manual mode it follows the focused trigger
    /// once the user has navigated, falling back to selection.
    /// </summary>
    public string? TabStopValue =>
        ActivationMode == "manual" ? (Focused ?? Selected) : Selected;

    internal async Task SetSelectedInternalAsync(string? value)
    {
        if (Selected == value)
        {
            return;
        }

        var oldIdx = IndexOf(Selected);
        var newIdx = IndexOf(value);
        Selected = value;

        if (oldIdx < 0 || newIdx < 0 || oldIdx == newIdx)
        {
            ActivationDirection = "none";
        }
        else
        {
            var forward = newIdx > oldIdx;
            ActivationDirection = Orientation == "vertical"
                ? (forward ? "down" : "up")
                : (forward ? "right" : "left");
        }

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public Task SelectAsync(string value) => _select(value);

    /// <summary>Record that a trigger received DOM focus (drives the roving tab stop in manual mode).</summary>
    public void NotifyFocused(string value) => Focused = value;

    /// <summary>
    /// Move by <paramref name="dir"/> (+1 / -1), skipping disabled. In automatic
    /// activation mode this focuses and selects the target; in manual mode it only
    /// moves focus (selection happens on Enter/Space/click).
    /// </summary>
    public async Task MoveAsync(int dir)
    {
        var enabled = _triggers.Where(t => !t.Disabled()).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        var anchor = Focused ?? Selected;
        var cur = enabled.FindIndex(t => t.Value == anchor);
        if (cur < 0)
        {
            cur = dir > 0 ? -1 : 0;
        }

        int targetIndex;
        if (Loop)
        {
            targetIndex = (cur + dir + enabled.Count) % enabled.Count;
        }
        else
        {
            targetIndex = cur + dir;
            if (targetIndex < 0 || targetIndex >= enabled.Count)
            {
                return; // clamp at edge: no movement
            }
        }

        await ActivateOrFocus(enabled[targetIndex]);
    }

    public async Task MoveToEdgeAsync(bool last)
    {
        var enabled = _triggers.Where(t => !t.Disabled()).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        await ActivateOrFocus(last ? enabled[^1] : enabled[0]);
    }

    private async Task ActivateOrFocus((string Value, Func<ElementReference> El, Func<bool> Disabled) target)
    {
        if (ActivationMode == "manual")
        {
            await FocusOnly(target);
        }
        else
        {
            await FocusAndSelect(target);
        }
    }

    private async Task FocusAndSelect((string Value, Func<ElementReference> El, Func<bool> Disabled) target)
    {
        Focused = target.Value;
        await SelectAsync(target.Value);
        await FocusElement(target);
    }

    private async Task FocusOnly((string Value, Func<ElementReference> El, Func<bool> Disabled) target)
    {
        Focused = target.Value;
        // Re-render so the roving tabindex follows focus before we move it.
        if (Changed is not null)
        {
            await Changed.Invoke();
        }
        await FocusElement(target);
    }

    private static async Task FocusElement((string Value, Func<ElementReference> El, Func<bool> Disabled) target)
    {
        try { await target.El().FocusAsync(); }
        catch { /* element not ready */ }
    }

    private static string Slug(string value) =>
        new string(value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
}
