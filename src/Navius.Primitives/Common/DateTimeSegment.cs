using Microsoft.AspNetCore.Components.Web;

namespace Navius.Primitives.Common;

/// <summary>The editable unit a segmented date/time field is composed of.</summary>
public enum NaviusSegmentKind
{
    Year,
    Month,
    Day,
    Hour,
    Minute,
    Second,
    DayPeriod,
}

/// <summary>
/// One editable segment of a segmented field (<c>NaviusDateInput</c> /
/// <c>NaviusTimeInput</c>). Mutable: the owning root seeds <see cref="Value"/> from its
/// bound value and reads it back when composing the value. Each segment is presented as
/// a <c>role="spinbutton"</c> with <see cref="Min"/>/<see cref="Max"/> bounds, arrow/page
/// step amounts, and a digit-typing buffer that drives auto-advance.
/// </summary>
public sealed class DateTimeSegment
{
    public required NaviusSegmentKind Kind { get; init; }

    /// <summary>Inclusive lower bound (<c>aria-valuemin</c>). Day recalculates per month.</summary>
    public int Min { get; set; }

    /// <summary>Inclusive upper bound (<c>aria-valuemax</c>). Day recalculates per month.</summary>
    public int Max { get; set; }

    /// <summary>Step applied on ArrowUp/ArrowDown (minute/second honour their step param).</summary>
    public int ArrowStep { get; init; } = 1;

    /// <summary>Larger step applied on PageUp/PageDown.</summary>
    public int PageStep { get; init; } = 1;

    /// <summary>Maximum digits the segment accepts before auto-advancing (year 4, else 2).</summary>
    public int MaxDigits { get; init; } = 2;

    /// <summary>Zero-based position of this segment among the field's focusable segments.</summary>
    public int Order { get; set; }

    /// <summary>The current value; <c>null</c> means the segment is an unfilled placeholder.</summary>
    public int? Value { get; set; }

    /// <summary>Digits typed since focus/last commit; reset on step, clear or auto-advance.</summary>
    public string TypeBuffer { get; set; } = string.Empty;

    /// <summary><c>true</c> once the segment holds a value (drives <c>data-placeholder</c> absence).</summary>
    public bool Filled => Value.HasValue;
}

/// <summary>An ordered piece of a segmented field: either an editable segment or a literal separator.</summary>
public readonly struct SegmentLayoutItem
{
    private SegmentLayoutItem(DateTimeSegment? segment, string? literal)
    {
        Segment = segment;
        Literal = literal;
    }

    public DateTimeSegment? Segment { get; }

    public string? Literal { get; }

    public bool IsLiteral => Literal is not null;

    public static SegmentLayoutItem Editable(DateTimeSegment segment) => new(segment, null);

    public static SegmentLayoutItem Separator(string literal) => new(null, literal);
}

/// <summary>The action a segment keystroke resolves to, applied by the owning root.</summary>
public enum SegmentKeyResult
{
    None,
    ValueChanged,
    ValueChangedAndAdvance,
    MovePrev,
    MoveNext,
}

/// <summary>
/// The spinbutton keyboard + digit-typing model shared by <c>NaviusDateInput</c> and
/// <c>NaviusTimeInput</c>. Pure functions over a single <see cref="DateTimeSegment"/>:
/// stepping wraps at the unit boundary, digit typing auto-advances when the segment can
/// hold no further digit, Backspace/Delete clears to the placeholder. AM/PM (day period)
/// toggles on arrows and on the <c>a</c>/<c>p</c> keys.
/// </summary>
public static class SegmentMath
{
    /// <summary>Wrap <paramref name="value"/> into the inclusive <paramref name="min"/>..<paramref name="max"/> range.</summary>
    public static int Wrap(int value, int min, int max)
    {
        var span = max - min + 1;
        return (((value - min) % span) + span) % span + min;
    }

    /// <summary>
    /// Step the segment by <paramref name="delta"/>, wrapping at the unit boundary. An
    /// empty segment lands on the placeholder basis directly (react-aria behaviour: the
    /// first arrow reveals the placeholder value rather than placeholder ± step).
    /// </summary>
    public static void Step(DateTimeSegment seg, int delta, int? placeholderBasis)
    {
        seg.Value = seg.Value is { } current
            ? Wrap(current + delta, seg.Min, seg.Max)
            : Wrap(placeholderBasis ?? seg.Min, seg.Min, seg.Max);
        seg.TypeBuffer = string.Empty;
    }

    /// <summary>Feed one typed digit into the segment; returns <c>true</c> when it should auto-advance.</summary>
    public static bool Type(DateTimeSegment seg, int digit)
    {
        var candidateText = seg.TypeBuffer + digit.ToString();
        var candidate = int.TryParse(candidateText, out var parsed) ? parsed : digit;

        if (candidate > seg.Max)
        {
            candidate = digit;
            candidateText = digit.ToString();
        }

        seg.TypeBuffer = candidateText;
        seg.Value = Math.Max(seg.Min, candidate);

        var advance = candidate * 10 > seg.Max || candidateText.Length >= seg.MaxDigits;
        if (advance)
        {
            seg.TypeBuffer = string.Empty;
        }

        return advance;
    }

    /// <summary>Clear the segment back to its unfilled placeholder state.</summary>
    public static void Clear(DateTimeSegment seg)
    {
        seg.Value = null;
        seg.TypeBuffer = string.Empty;
    }

    /// <summary>
    /// Resolve a keystroke against <paramref name="seg"/>, mutating it in place, and report
    /// what the caller must do next (re-compose the value, move focus, or nothing).
    /// </summary>
    public static SegmentKeyResult HandleKey(DateTimeSegment seg, KeyboardEventArgs e, int? placeholderBasis)
    {
        if (seg.Kind == NaviusSegmentKind.DayPeriod)
        {
            switch (e.Key)
            {
                case "ArrowUp":
                case "ArrowDown":
                    // Empty reveals AM first (react-aria); a filled period toggles.
                    seg.Value = seg.Value == 0 ? 1 : 0;
                    return SegmentKeyResult.ValueChanged;
                case "a":
                case "A":
                    seg.Value = 0;
                    return SegmentKeyResult.ValueChanged;
                case "p":
                case "P":
                    seg.Value = 1;
                    return SegmentKeyResult.ValueChanged;
                case "Backspace":
                case "Delete":
                    seg.Value = null;
                    return SegmentKeyResult.ValueChanged;
                case "ArrowLeft":
                    return SegmentKeyResult.MovePrev;
                case "ArrowRight":
                    return SegmentKeyResult.MoveNext;
                default:
                    return SegmentKeyResult.None;
            }
        }

        switch (e.Key)
        {
            case "ArrowUp":
                Step(seg, seg.ArrowStep, placeholderBasis);
                return SegmentKeyResult.ValueChanged;
            case "ArrowDown":
                Step(seg, -seg.ArrowStep, placeholderBasis);
                return SegmentKeyResult.ValueChanged;
            case "PageUp":
                Step(seg, seg.PageStep, placeholderBasis);
                return SegmentKeyResult.ValueChanged;
            case "PageDown":
                Step(seg, -seg.PageStep, placeholderBasis);
                return SegmentKeyResult.ValueChanged;
            case "Home":
                seg.Value = seg.Min;
                seg.TypeBuffer = string.Empty;
                return SegmentKeyResult.ValueChanged;
            case "End":
                seg.Value = seg.Max;
                seg.TypeBuffer = string.Empty;
                return SegmentKeyResult.ValueChanged;
            case "Backspace":
            case "Delete":
                Clear(seg);
                return SegmentKeyResult.ValueChanged;
            case "ArrowLeft":
                return SegmentKeyResult.MovePrev;
            case "ArrowRight":
                return SegmentKeyResult.MoveNext;
            default:
                if (e.Key.Length == 1 && char.IsDigit(e.Key[0]))
                {
                    var advance = Type(seg, e.Key[0] - '0');
                    return advance ? SegmentKeyResult.ValueChangedAndAdvance : SegmentKeyResult.ValueChanged;
                }

                return SegmentKeyResult.None;
        }
    }
}
