using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.OneTimePasswordField;

/// <summary>
/// Shared state for a one-time-password field. The root owns the authoritative
/// per-character buffer and pushes it here; the rendered <c>&lt;input&gt;</c> cells read
/// their slot's character and report edits / navigation requests back to the root.
/// </summary>
/// <remarks>
/// Mirrors the spec's <c>OneTimePasswordField</c> anatomy: a <c>role="group"</c> root over
/// <see cref="Length"/> single-character inputs whose concatenation is the field value.
/// Focus management (advance / retreat / arrows / delete / paste distribution) is driven
/// from the root via <see cref="ElementReference.FocusAsync()"/> — no engine handle is
/// required. The buffer is positional: <see cref="CharAt"/> reads a slot directly so a
/// cell's <c>data-index</c> stays aligned with its glyph even when interior cells are empty.
/// </remarks>
public sealed class OneTimePasswordFieldContext
{
    private readonly Func<int, string, Task> _setChar;
    private readonly Func<int, Task> _focusIndex;
    private readonly Func<int, string, Task> _pasteFrom;
    private readonly Action<int, ElementReference> _registerCell;
    private readonly Func<int, KeyKind, bool, Task> _key;
    private readonly Func<Task> _submit;
    private char?[] _chars;

    public OneTimePasswordFieldContext(
        int length,
        bool disabled,
        bool readOnly,
        string inputMode,
        string type,
        string orientation,
        string? placeholder,
        Func<int, string, Task> setChar,
        Func<int, Task> focusIndex,
        Func<int, string, Task> pasteFrom,
        Action<int, ElementReference> registerCell,
        Func<int, KeyKind, bool, Task> key,
        Func<Task> submit)
    {
        Length = length;
        Disabled = disabled;
        ReadOnly = readOnly;
        InputMode = inputMode;
        Type = type;
        Orientation = orientation;
        Placeholder = placeholder;
        _setChar = setChar;
        _focusIndex = focusIndex;
        _pasteFrom = pasteFrom;
        _registerCell = registerCell;
        _key = key;
        _submit = submit;
        _chars = new char?[length];
    }

    /// <summary>Number of single-character input cells.</summary>
    public int Length { get; private set; }

    /// <summary>When <c>true</c> every cell is disabled.</summary>
    public bool Disabled { get; private set; }

    /// <summary>When <c>true</c> every cell is read-only.</summary>
    public bool ReadOnly { get; private set; }

    /// <summary><c>inputmode</c> applied to each cell (e.g. <c>"numeric"</c>).</summary>
    public string InputMode { get; private set; }

    /// <summary>Rendered cell type: <c>"text"</c> or <c>"password"</c> (mirrors the spec <c>type</c>).</summary>
    public string Type { get; private set; }

    /// <summary>Navigation orientation: <c>"vertical"</c> or <c>"horizontal"</c> (mirrors the spec).</summary>
    public string Orientation { get; private set; }

    /// <summary>Placeholder shown in empty cells (mirrors the spec <c>placeholder</c>).</summary>
    public string? Placeholder { get; private set; }

    /// <summary>The current aggregate value (dense concatenation of filled cells, no interior gaps).</summary>
    public string Value => new string(_chars.Where(c => c is not null).Select(c => c!.Value).ToArray());

    public event Func<Task>? Changed;

    /// <summary>The character currently shown in cell <paramref name="index"/>, or empty.</summary>
    public string CharAt(int index) =>
        index >= 0 && index < _chars.Length && _chars[index] is { } c ? c.ToString() : string.Empty;

    /// <summary>Cell entry point: write <paramref name="raw"/> into slot <paramref name="index"/>.</summary>
    public Task SetCharAsync(int index, string raw) => _setChar(index, raw);

    /// <summary>Cell entry point: move focus to slot <paramref name="index"/> (clamped by the root).</summary>
    public Task FocusAsync(int index) => _focusIndex(index);

    /// <summary>Cell entry point: distribute pasted <paramref name="text"/> (replaces the whole field).</summary>
    public Task PasteAsync(int index, string text) => _pasteFrom(index, text);

    /// <summary>Cell entry point: register a cell's element so the root can move focus to it.</summary>
    public void RegisterCell(int index, ElementReference element) => _registerCell(index, element);

    /// <summary>Cell entry point: a navigation/edit key was pressed in slot <paramref name="index"/>.</summary>
    public Task KeyAsync(int index, KeyKind kind, bool modifier) => _key(index, kind, modifier);

    /// <summary>Cell entry point: request submission of the owning form (Enter).</summary>
    public Task SubmitAsync() => _submit();

    /// <summary>
    /// Replace the authoritative buffer from an aggregate string and re-render cells if it changed.
    /// The aggregate maps onto slots 0..n-1; a space is the sentinel for an interior gap (no OTP
    /// validation class admits a space), so positions are preserved across the controlled-value
    /// round-trip. Characters beyond <see cref="Length"/> are dropped.
    /// </summary>
    internal async Task SetValueInternalAsync(string? value)
    {
        var next = new char?[Length];
        var src = value ?? string.Empty;
        for (var i = 0; i < Length && i < src.Length; i++)
        {
            next[i] = src[i] == ' ' ? null : src[i];
        }

        if (next.AsSpan().SequenceEqual(_chars))
        {
            return;
        }

        _chars = next;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    /// <summary>Re-shape the buffer when <see cref="Length"/> or flags change, preserving overlap.</summary>
    internal void Reconfigure(
        int length, bool disabled, bool readOnly, string inputMode,
        string type, string orientation, string? placeholder)
    {
        Disabled = disabled;
        ReadOnly = readOnly;
        InputMode = inputMode;
        Type = type;
        Orientation = orientation;
        Placeholder = placeholder;

        if (length == Length)
        {
            return;
        }

        var next = new char?[length];
        for (var i = 0; i < length && i < _chars.Length; i++)
        {
            next[i] = _chars[i];
        }

        Length = length;
        _chars = next;
    }
}

/// <summary>The kind of edit/navigation key reported by a cell to the root.</summary>
public enum KeyKind
{
    /// <summary>Move focus to the previous cell.</summary>
    Prev,

    /// <summary>Move focus to the next cell.</summary>
    Next,

    /// <summary>Move focus to the first cell.</summary>
    First,

    /// <summary>Move focus to the last cell.</summary>
    Last,

    /// <summary>Backspace: clear the focused (or previous) char and retreat.</summary>
    Backspace,

    /// <summary>Delete: clear the focused char and shift the remainder back.</summary>
    Delete,
}
