using Navius.Primitives.Common;

namespace Navius.Primitives.Components.DateRangePicker;

/// <summary>
/// Shared state cascaded from <see cref="NaviusDateRangePicker"/> to its control group, the
/// two segmented endpoint inputs, the trigger and the calendar content. The root owns the
/// authoritative <see cref="NaviusDateRange"/> value and controlled/uncontrolled open state
/// (via the wrapped popover); each endpoint input edits one side through
/// <see cref="SetStartAsync"/> / <see cref="SetEndAsync"/>.
/// </summary>
public sealed class DateRangePickerContext
{
    public NaviusDateRange Value { get; internal set; } = NaviusDateRange.Empty;

    public DateOnly? MinValue { get; internal set; }

    public DateOnly? MaxValue { get; internal set; }

    public string Granularity { get; internal set; } = "day";

    public bool Disabled { get; internal set; }

    public bool ReadOnly { get; internal set; }

    public bool Required { get; internal set; }

    public bool Invalid { get; internal set; }

    public string? StartName { get; internal set; }

    public string? EndName { get; internal set; }

    // Wired by the root.
    internal Func<NaviusDateRange, Task>? SetRangeImpl;

    /// <summary>Replace the whole range (used by the calendar content).</summary>
    public Task SetRangeAsync(NaviusDateRange range) => SetRangeImpl?.Invoke(range) ?? Task.CompletedTask;

    /// <summary>Set the start endpoint, keeping the current end.</summary>
    public Task SetStartAsync(DateOnly? start) => SetRangeAsync(Value with { Start = start });

    /// <summary>Set the end endpoint, keeping the current start.</summary>
    public Task SetEndAsync(DateOnly? end) => SetRangeAsync(Value with { End = end });

    public event Func<Task>? Changed;

    internal Task NotifyAsync() => Changed?.Invoke() ?? Task.CompletedTask;
}
