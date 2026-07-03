using System.Text;

namespace Navius.Primitives.Components.MaskedInput;

/// <summary>
/// The immutable value + selection snapshot the masking pipeline transforms (the Maskito
/// element-state model). <see cref="Selection"/> is <c>(selectionStart, selectionEnd)</c>,
/// exactly like <c>HTMLInputElement.setSelectionRange</c>. Preprocessors receive the raw
/// proposed state; postprocessors receive the masked state.
/// </summary>
public sealed record ElementState(string Value, (int Start, int End) Selection);

/// <summary>The three placeholder token classes plus the fixed-literal class.</summary>
internal enum MaskTokenKind
{
    /// <summary>A mask literal (<c>/</c>, <c>(</c>, <c>-</c>, space …) auto-inserted verbatim.</summary>
    Fixed,
    /// <summary><c>0</c>: one digit <c>[0-9]</c>.</summary>
    Digit,
    /// <summary><c>A</c>: one letter.</summary>
    Letter,
    /// <summary><c>*</c>: one alphanumeric.</summary>
    Alnum,
}

/// <summary>One parsed mask token. A fixed token carries its <see cref="Literal"/>.</summary>
internal readonly record struct MaskToken(MaskTokenKind Kind, char Literal)
{
    public bool IsFixed => Kind == MaskTokenKind.Fixed;

    public bool Accepts(char c) => Kind switch
    {
        MaskTokenKind.Digit => char.IsDigit(c),
        MaskTokenKind.Letter => char.IsLetter(c),
        MaskTokenKind.Alnum => char.IsLetterOrDigit(c),
        _ => false,
    };
}

/// <summary>
/// The hand-rolled, caret-stable masking core (a C# reimplementation of the Maskito
/// element-state pipeline; we do not depend on Maskito). Pure and side-effect-free so it is
/// unit-testable in isolation. The one non-obvious part is <see cref="Format"/>'s caret:
/// it never trusts the raw caret index; it counts the editable (placeholder-filled)
/// characters left of the proposed caret and re-lands the caret after the same count of
/// editable characters in the freshly masked string, so a mid-string edit (or a Backspace
/// over a fixed literal) keeps the caret exactly where the user expects.
/// </summary>
internal static class MaskEngine
{
    /// <summary>Parse a mask pattern: <c>0</c>=digit, <c>A</c>=letter, <c>*</c>=alnum, anything else=fixed literal.</summary>
    public static IReadOnlyList<MaskToken> Parse(string mask)
    {
        var tokens = new List<MaskToken>(mask.Length);
        foreach (var c in mask)
        {
            tokens.Add(c switch
            {
                '0' => new MaskToken(MaskTokenKind.Digit, '0'),
                'A' => new MaskToken(MaskTokenKind.Letter, 'A'),
                '*' => new MaskToken(MaskTokenKind.Alnum, '*'),
                _ => new MaskToken(MaskTokenKind.Fixed, c),
            });
        }

        return tokens;
    }

    /// <summary>
    /// Walk <paramref name="raw"/> against <paramref name="tokens"/>, producing the masked value
    /// and the caret. Fixed literals are auto-inserted (or consumed when the user typed them);
    /// placeholder tokens consume one accepted char and reject/skip anything else. Empty
    /// placeholder slots render <paramref name="placeholder"/> when set. When <paramref name="lazy"/>
    /// a trailing fixed literal is not emitted until the user reaches it.
    /// </summary>
    public static (string Value, int CaretStart, int CaretEnd, string Unmasked) Format(
        IReadOnlyList<MaskToken> tokens, string raw, int caretStart, int caretEnd, bool lazy, char? placeholder)
    {
        var (value, editable, unmasked, beforeStart) = Walk(tokens, raw, caretStart, lazy, placeholder);
        // caretEnd is walked on the same raw; only the "consumed before caret" count differs.
        var beforeEnd = caretEnd == caretStart ? beforeStart : CountConsumedBefore(tokens, raw, caretEnd);
        return (value, CaretFor(value, editable, beforeStart), CaretFor(value, editable, beforeEnd), unmasked);
    }

    // Core walk. Returns the masked value, a per-output-char editable flag, the unmasked
    // (placeholder-filled) characters, and how many editable chars sit left of `caret`.
    private static (string Value, bool[] Editable, string Unmasked, int ConsumedBeforeCaret) Walk(
        IReadOnlyList<MaskToken> tokens, string raw, int caret, bool lazy, char? placeholder)
    {
        var sb = new StringBuilder();
        var editable = new List<bool>();
        var unmasked = new StringBuilder();
        var ri = 0;
        var consumedBeforeCaret = 0;

        foreach (var t in tokens)
        {
            if (t.IsFixed)
            {
                var consumed = false;
                if (ri < raw.Length && raw[ri] == t.Literal)
                {
                    ri++;
                    consumed = true;
                }

                var moreRaw = ri < raw.Length;
                if (placeholder.HasValue || !lazy || moreRaw || consumed)
                {
                    sb.Append(t.Literal);
                    editable.Add(false);
                }
                else
                {
                    break; // lazy + nothing left to place after this literal → stop.
                }
            }
            else
            {
                while (ri < raw.Length && !t.Accepts(raw[ri]))
                {
                    ri++; // drop a rejected character.
                }

                if (ri < raw.Length)
                {
                    if (ri < caret)
                    {
                        consumedBeforeCaret++;
                    }

                    sb.Append(raw[ri]);
                    unmasked.Append(raw[ri]);
                    editable.Add(true);
                    ri++;
                }
                else if (placeholder.HasValue)
                {
                    sb.Append(placeholder.Value);
                    editable.Add(false);
                }
                else
                {
                    break;
                }
            }
        }

        return (sb.ToString(), editable.ToArray(), unmasked.ToString(), consumedBeforeCaret);
    }

    private static int CountConsumedBefore(IReadOnlyList<MaskToken> tokens, string raw, int caret)
        => Walk(tokens, raw, caret, lazy: true, placeholder: null).ConsumedBeforeCaret;

    // Land the caret right after the Nth editable char (N = editable chars left of the old caret).
    private static int CaretFor(string value, bool[] editable, int consumedBefore)
    {
        if (consumedBefore <= 0)
        {
            return 0;
        }

        var seen = 0;
        for (var i = 0; i < value.Length && i < editable.Length; i++)
        {
            if (!editable[i])
            {
                continue;
            }

            seen++;
            if (seen == consumedBefore)
            {
                return i + 1;
            }
        }

        return value.Length;
    }
}
