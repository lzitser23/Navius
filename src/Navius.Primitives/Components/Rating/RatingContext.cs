using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Rating;

/// <summary>
/// Shared state for a rating (an APG radio group of stars). The root owns the
/// authoritative <see cref="decimal"/> value plus a transient hover-preview value
/// and pushes both here; items register their element + disabled state so the
/// root's keyboard model (arrows / Home / End / digits) can move focus onto the
/// star that holds the new value. Items read <see cref="Effective"/> to paint
/// their own full/half/empty fill without reaching back into the root.
/// </summary>
public sealed class RatingContext
{
    private readonly Func<decimal?, Task> _select;
    private readonly Func<decimal?, Task> _setHover;
    private readonly List<(Func<ElementReference> El, Func<bool> Disabled)> _items = new();

    public RatingContext(Func<decimal?, Task> select, Func<decimal?, Task> setHover)
    {
        _select = select;
        _setHover = setHover;
    }

    /// <summary>Number of visual stars (each star is one <c>role="radio"</c>).</summary>
    public int Max { get; private set; } = 5;

    /// <summary>Whether half-star values (0.5, 1.5, …) are reachable via pointer + keyboard.</summary>
    public bool AllowHalf { get; private set; }

    /// <summary>Whether re-selecting the current value (or arrowing below the lowest) clears to unrated.</summary>
    public bool AllowClear { get; private set; } = true;

    public bool ReadOnly { get; private set; }

    public bool Disabled { get; private set; }

    public bool Required { get; private set; }

    /// <summary>Optional form field name, mirrored onto the hidden bubble input.</summary>
    public string? Name { get; private set; }

    /// <summary>Localizable accessible-name factory for a star value ("3.5 stars").</summary>
    public Func<decimal, string>? Label { get; private set; }

    /// <summary>The committed value (null = unrated).</summary>
    public decimal? Value { get; private set; }

    /// <summary>The transient hover-preview value (null = not hovering).</summary>
    public decimal? HoverValue { get; private set; }

    public event Func<Task>? Changed;

    public void Configure(int max, bool allowHalf, bool allowClear, bool readOnly, bool disabled, bool required, string? name, Func<decimal, string>? label)
    {
        Max = max < 1 ? 1 : max;
        AllowHalf = allowHalf;
        AllowClear = allowClear;
        ReadOnly = readOnly;
        Disabled = disabled;
        Required = required;
        Name = name;
        Label = label;
    }

    /// <summary>Register an item in document order; returns its 1-based star index.</summary>
    public int RegisterItem(Func<ElementReference> el, Func<bool> disabled)
    {
        _items.Add((el, disabled));
        return _items.Count;
    }

    internal async Task SetValueInternalAsync(decimal? value)
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

    internal async Task SetHoverInternalAsync(decimal? value)
    {
        if (HoverValue == value)
        {
            return;
        }

        HoverValue = value;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public Task SelectAsync(decimal? value) => _select(value);

    public Task SetHoverAsync(decimal? value) => _setHover(value);

    /// <summary>The per-arrow increment (0.5 with <see cref="AllowHalf"/>, else 1).</summary>
    public decimal Step => AllowHalf ? 0.5m : 1m;

    /// <summary>The value that drives the fill: the hover preview when present, else the committed value.</summary>
    public decimal Effective => HoverValue ?? Value ?? 0m;

    public string LabelFor(decimal value) => Label?.Invoke(value) ?? DefaultLabel(value);

    private static string DefaultLabel(decimal value)
        => value == 1m
            ? "1 star"
            : $"{value.ToString(System.Globalization.CultureInfo.InvariantCulture)} stars";

    /// <summary>Focus the star at the 1-based <paramref name="index"/> (used after a keyboard edit).</summary>
    public async Task FocusIndexAsync(int index)
    {
        if (index >= 1 && index <= _items.Count)
        {
            try { await _items[index - 1].El().FocusAsync(); }
            catch { /* element not ready */ }
        }
    }
}
