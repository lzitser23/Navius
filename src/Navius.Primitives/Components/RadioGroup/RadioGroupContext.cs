using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.RadioGroup;

/// <summary>
/// Shared state for a radio group. The root owns the authoritative value and
/// pushes it here; items register their element + disabled state so keyboard
/// navigation (arrows / Home / End) can move focus + selection together
/// (automatic activation, per WAI-ARIA APG).
/// </summary>
public sealed class RadioGroupContext
{
    private readonly Func<string, Task> _select;
    private readonly List<(string Value, Func<ElementReference> El, Func<bool> Disabled)> _items = new();

    public RadioGroupContext(Func<string, Task> select, string? name, bool disabled, bool required, string? orientation, bool loop)
    {
        _select = select;
        Name = name;
        Disabled = disabled;
        Required = required;
        Orientation = orientation;
        Loop = loop;
    }

    /// <summary>Optional form field name, mirrored onto each item's hidden input.</summary>
    public string? Name { get; }

    /// <summary>Whether the entire group is disabled.</summary>
    public bool Disabled { get; }

    /// <summary>Whether the group requires a selection (mirrored onto the native form input).</summary>
    public bool Required { get; }

    public string? Orientation { get; }

    /// <summary>Whether arrow navigation wraps around the ends (the spec loop, default true).</summary>
    public bool Loop { get; }

    public string? Value { get; private set; }

    public event Func<Task>? Changed;

    public void Register(string value, Func<ElementReference> el, Func<bool> disabled)
    {
        if (!_items.Any(i => i.Value == value))
        {
            _items.Add((value, el, disabled));
        }
    }

    public void Unregister(string value) => _items.RemoveAll(i => i.Value == value);

    internal async Task SetValueInternalAsync(string? value)
    {
        if (Value == value)
        {
            return;
        }

        Value = value;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public Task SelectAsync(string value) => _select(value);

    /// <summary>Move selection (and focus) by <paramref name="dir"/> (+1 / -1), skipping disabled, wrapping.</summary>
    public async Task MoveAsync(int dir)
    {
        var enabled = _items.Where(i => !i.Disabled()).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        var cur = enabled.FindIndex(i => i.Value == Value);
        if (cur < 0)
        {
            cur = dir > 0 ? -1 : 0;
        }

        int next;
        if (Loop)
        {
            next = (cur + dir + enabled.Count) % enabled.Count;
        }
        else
        {
            next = cur + dir;
            if (next < 0 || next >= enabled.Count)
            {
                // Clamp at the ends instead of wrapping.
                return;
            }
        }

        await FocusAndSelect(enabled[next]);
    }

    public async Task MoveToEdgeAsync(bool last)
    {
        var enabled = _items.Where(i => !i.Disabled()).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        await FocusAndSelect(last ? enabled[^1] : enabled[0]);
    }

    /// <summary>Whether this value is the first enabled item (used for roving tabindex when nothing is checked).</summary>
    public bool IsFirstEnabled(string value)
    {
        var first = _items.FirstOrDefault(i => !i.Disabled());
        return first.Value == value;
    }

    private async Task FocusAndSelect((string Value, Func<ElementReference> El, Func<bool> Disabled) target)
    {
        await SelectAsync(target.Value);
        try { await target.El().FocusAsync(); }
        catch { /* element not ready */ }
    }
}
