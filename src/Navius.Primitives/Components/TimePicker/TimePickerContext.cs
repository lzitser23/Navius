using Navius.Primitives.Common;

namespace Navius.Primitives.Components.TimePicker;

/// <summary>
/// Shared state cascaded from <see cref="NaviusTimePicker"/> to its embedded time input,
/// trigger, and listbox columns. The root owns the authoritative <see cref="Value"/> and
/// controlled/uncontrolled open state (via the wrapped popover); the columns edit a single
/// unit through <see cref="SetUnitAsync"/> and the input round-trips the whole value through
/// <see cref="SetValueAsync"/>.
/// </summary>
public sealed class TimePickerContext
{
    /// <summary>The current time (null = nothing selected yet).</summary>
    public TimeOnly? Value { get; internal set; }

    /// <summary>12 or 24; decides the hour column range and whether an AM/PM column shows.</summary>
    public int HourCycle { get; internal set; } = 24;

    /// <summary><c>hour</c> / <c>minute</c> / <c>second</c>: which columns render.</summary>
    public string Granularity { get; internal set; } = "minute";

    public int MinuteStep { get; internal set; } = 1;

    public int SecondStep { get; internal set; } = 1;

    public bool Disabled { get; internal set; }

    // Wired by the root.
    internal Func<TimeOnly?, Task>? SetValueImpl;

    /// <summary>Replace the whole value (used by the embedded segmented input).</summary>
    public Task SetValueAsync(TimeOnly? value) => SetValueImpl?.Invoke(value) ?? Task.CompletedTask;

    /// <summary>Set one unit (from a column), preserving the other units, and commit the composed time.</summary>
    public Task SetUnitAsync(NaviusSegmentKind unit, int value)
    {
        var basis = Value ?? new TimeOnly(0, 0);
        int hour = basis.Hour, minute = basis.Minute, second = basis.Second;

        switch (unit)
        {
            case NaviusSegmentKind.Hour:
                if (HourCycle == 12)
                {
                    var pm = basis.Hour >= 12;
                    hour = (value % 12) + (pm ? 12 : 0);
                }
                else
                {
                    hour = value;
                }

                break;
            case NaviusSegmentKind.Minute:
                minute = value;
                break;
            case NaviusSegmentKind.Second:
                second = value;
                break;
            case NaviusSegmentKind.DayPeriod:
                var wantPm = value == 1;
                hour = (basis.Hour % 12) + (wantPm ? 12 : 0);
                break;
        }

        return SetValueAsync(new TimeOnly(hour, minute, second));
    }

    /// <summary>The current value of a unit in display terms (12h hour, AM/PM index), or null.</summary>
    public int? UnitValue(NaviusSegmentKind unit)
    {
        if (Value is not { } v)
        {
            return null;
        }

        return unit switch
        {
            NaviusSegmentKind.Hour => HourCycle == 12 ? (v.Hour % 12 == 0 ? 12 : v.Hour % 12) : v.Hour,
            NaviusSegmentKind.Minute => v.Minute,
            NaviusSegmentKind.Second => v.Second,
            NaviusSegmentKind.DayPeriod => v.Hour >= 12 ? 1 : 0,
            _ => null,
        };
    }

    public event Func<Task>? Changed;

    internal Task NotifyAsync() => Changed?.Invoke() ?? Task.CompletedTask;
}
