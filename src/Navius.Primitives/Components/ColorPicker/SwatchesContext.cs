using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ColorPicker;

/// <summary>
/// Roving-focus coordinator for a preset swatch listbox. Items register in
/// document order; the container's arrow keys move focus (single tab stop) and
/// Enter/Space/click apply the swatch to the picker. Selection is derived by
/// comparing each swatch's hex to the picker's current hex.
/// </summary>
public sealed class SwatchesContext
{
    private readonly ColorPickerContext _picker;
    private readonly List<(string Value, Func<ElementReference> El)> _items = new();

    public SwatchesContext(ColorPickerContext picker)
    {
        _picker = picker;
    }

    public string CurrentHex => _picker.HexValue;

    public int Register(string value, Func<ElementReference> el)
    {
        _items.Add((value, el));
        return _items.Count - 1;
    }

    public bool IsFirst(int index) => index == 0;

    /// <summary>Whether any registered swatch equals the current color (for roving tabindex).</summary>
    public bool AnySelected()
    {
        foreach (var item in _items)
        {
            if (string.Equals(HexOf(item.Value), CurrentHex, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public Task SelectAsync(string value) => _picker.SetFromStringAsync(value);

    public async Task MoveAsync(int fromIndex, int dir)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var next = Math.Clamp(fromIndex + dir, 0, _items.Count - 1);
        await FocusAsync(next);
    }

    public async Task MoveEdgeAsync(bool last)
    {
        if (_items.Count == 0)
        {
            return;
        }

        await FocusAsync(last ? _items.Count - 1 : 0);
    }

    private async Task FocusAsync(int index)
    {
        try { await _items[index].El().FocusAsync(); }
        catch { /* not ready */ }
    }

    internal static string HexOf(string? value)
        => ColorMath.TryParse(value, out var h, out var s, out var v, out var a)
            ? ColorMath.Format("hex", h, s, v, a)
            : value ?? string.Empty;
}
