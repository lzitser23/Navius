using System.Globalization;
using Microsoft.AspNetCore.Components.Web;

namespace Navius.Primitives.Common;

/// <summary>
/// Shared state cascaded from a segmented field root (<c>NaviusDateInput</c> /
/// <c>NaviusTimeInput</c>) to its <see cref="SegmentLayoutItem"/> parts. The root owns
/// the authoritative value and rebuilds <see cref="Layout"/>; the segment parts render
/// from it and route keystrokes back through <see cref="HandleKeyAsync"/>. Presentation
/// helpers (labels, <c>aria-value*</c>, display text) live here so both the date and time
/// segment parts share one culture-aware source of truth. <see cref="PartName"/> keys the
/// discrete <c>data-navius-&lt;part&gt;-segment</c> / <c>-literal</c> test hooks.
/// </summary>
public sealed class SegmentFieldContext
{
    public SegmentFieldContext(string partName)
    {
        PartName = partName;
    }

    /// <summary>"date-input" or "time-input"; keys the discrete data-* test hooks.</summary>
    public string PartName { get; }

    /// <summary>The ordered segments + literals the root computed from culture + granularity.</summary>
    public IReadOnlyList<SegmentLayoutItem> Layout { get; internal set; } = Array.Empty<SegmentLayoutItem>();

    public CultureInfo Culture { get; internal set; } = CultureInfo.CurrentCulture;

    public bool ForceLeadingZeros { get; internal set; }

    public bool Disabled { get; internal set; }

    public bool ReadOnly { get; internal set; }

    public bool Required { get; internal set; }

    public bool Invalid { get; internal set; }

    /// <summary>Reading direction ("ltr"/"rtl"); flips ArrowLeft/ArrowRight focus travel.</summary>
    public string Dir { get; internal set; } = "ltr";

    /// <summary>The <see cref="DateTimeSegment.Order"/> of the segment that should pull DOM focus next.</summary>
    public int FocusIndex { get; internal set; } = -1;

    /// <summary>One-shot: true for exactly one render after a keyboard-driven focus move.</summary>
    public bool FocusRequested { get; internal set; }

    // Wired by the root.
    internal Func<int, KeyboardEventArgs, Task>? KeyImpl;

    /// <summary>Route a segment keystroke (identified by its <see cref="DateTimeSegment.Order"/>) to the root.</summary>
    public Task HandleKeyAsync(int order, KeyboardEventArgs e) => KeyImpl?.Invoke(order, e) ?? Task.CompletedTask;

    // --- presentation -------------------------------------------------------

    /// <summary>The <c>data-segment</c> value for a kind.</summary>
    public static string SegmentName(NaviusSegmentKind kind) => kind switch
    {
        NaviusSegmentKind.Year => "year",
        NaviusSegmentKind.Month => "month",
        NaviusSegmentKind.Day => "day",
        NaviusSegmentKind.Hour => "hour",
        NaviusSegmentKind.Minute => "minute",
        NaviusSegmentKind.Second => "second",
        NaviusSegmentKind.DayPeriod => "dayPeriod",
        _ => "",
    };

    /// <summary>The accessible label read before the segment value ("month, ", etc.).</summary>
    public static string AriaLabel(DateTimeSegment seg) => seg.Kind switch
    {
        NaviusSegmentKind.Year => "year",
        NaviusSegmentKind.Month => "month",
        NaviusSegmentKind.Day => "day",
        NaviusSegmentKind.Hour => "hour",
        NaviusSegmentKind.Minute => "minute",
        NaviusSegmentKind.Second => "second",
        NaviusSegmentKind.DayPeriod => "AM/PM",
        _ => "",
    };

    /// <summary><c>aria-valuenow</c>: the raw number, or null for an empty / day-period segment.</summary>
    public string? AriaValueNow(DateTimeSegment seg) =>
        seg.Kind == NaviusSegmentKind.DayPeriod || seg.Value is null
            ? null
            : seg.Value.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary><c>aria-valuetext</c>: "Empty" when unfilled, the month name / AM-PM otherwise.</summary>
    public string AriaValueText(DateTimeSegment seg)
    {
        if (!seg.Filled)
        {
            return "Empty";
        }

        return seg.Kind switch
        {
            NaviusSegmentKind.Month => Culture.DateTimeFormat.GetMonthName(seg.Value!.Value),
            NaviusSegmentKind.DayPeriod => seg.Value == 1 ? "PM" : "AM",
            _ => seg.Value!.Value.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>The visible text: a format-letter placeholder when empty, else the formatted value.</summary>
    public string Display(DateTimeSegment seg)
    {
        if (!seg.Filled)
        {
            return seg.Kind switch
            {
                NaviusSegmentKind.Year => "yyyy",
                NaviusSegmentKind.Month => "mm",
                NaviusSegmentKind.Day => "dd",
                NaviusSegmentKind.Hour => "hh",
                NaviusSegmentKind.Minute => "mm",
                NaviusSegmentKind.Second => "ss",
                NaviusSegmentKind.DayPeriod => "AM",
                _ => "",
            };
        }

        return seg.Kind switch
        {
            NaviusSegmentKind.DayPeriod => seg.Value == 1 ? "PM" : "AM",
            NaviusSegmentKind.Year => seg.Value!.Value.ToString(ForceLeadingZeros ? "D4" : "D1", CultureInfo.InvariantCulture),
            _ => seg.Value!.Value.ToString(ForceLeadingZeros ? "D2" : "D1", CultureInfo.InvariantCulture),
        };
    }

    public event Func<Task>? Changed;

    internal Task NotifyAsync() => Changed?.Invoke() ?? Task.CompletedTask;
}
