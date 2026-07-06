using System.Globalization;

namespace Navius.Primitives.Common;

/// <summary>
/// A date range value: an optional <see cref="Start"/> and <see cref="End"/>. Used by
/// <c>NaviusDateRangePicker</c> (the .NET value type chosen for parity spec 01). Both
/// endpoints are nullable so the range can be partially entered (start typed, end not
/// yet). Endpoints are always stored ordered (<see cref="Start"/> &lt;= <see cref="End"/>)
/// when both are present; use <see cref="Ordered"/> to normalise an arbitrary pair.
/// </summary>
public readonly record struct NaviusDateRange(DateOnly? Start, DateOnly? End)
{
    /// <summary>An empty range (both endpoints null).</summary>
    public static readonly NaviusDateRange Empty = new(null, null);

    /// <summary>True when both endpoints are set.</summary>
    public bool IsComplete => Start is not null && End is not null;

    /// <summary>True when neither endpoint is set.</summary>
    public bool IsEmpty => Start is null && End is null;

    /// <summary>Return the range with endpoints swapped if they are out of order.</summary>
    public NaviusDateRange Ordered()
    {
        if (Start is { } s && End is { } e && e < s)
        {
            return new NaviusDateRange(e, s);
        }

        return this;
    }

    /// <summary>ISO 8601 (<c>yyyy-MM-dd</c>) for <see cref="Start"/>, or empty when unset.</summary>
    public string StartIso => Start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>ISO 8601 (<c>yyyy-MM-dd</c>) for <see cref="End"/>, or empty when unset.</summary>
    public string EndIso => End?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
