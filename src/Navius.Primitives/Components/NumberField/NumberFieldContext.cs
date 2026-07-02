namespace Navius.Primitives.Components.NumberField;

/// <summary>
/// Shared state cascaded from <see cref="NaviusNumberField"/> to its parts
/// (group, input, increment, decrement). The root owns the authoritative numeric
/// value and the step/clamp logic; parts read the formatted <see cref="Display"/>
/// and call back through <see cref="StepAsync"/> / <see cref="SetTextAsync"/>.
/// </summary>
public sealed class NumberFieldContext
{
    /// <summary>Current numeric value (null = empty).</summary>
    public double? Value { get; internal set; }

    public double Step { get; internal set; } = 1;

    public double LargeStep { get; internal set; } = 10;

    public double SmallStep { get; internal set; } = 0.1;

    public double? Min { get; internal set; }

    public double? Max { get; internal set; }

    public bool Disabled { get; internal set; }

    public bool ReadOnly { get; internal set; }

    public bool Required { get; internal set; }

    /// <summary>The formatted display string for the current value (empty when null).</summary>
    public string Display { get; internal set; } = string.Empty;

    public string ControlId { get; } = $"navius-numberfield-{Guid.NewGuid():N}";

    /// <summary>Whether the value can still be decremented (not disabled/readonly and above Min).</summary>
    public bool CanDecrement => !Disabled && !ReadOnly && (Min is null || (Value ?? Min.Value) > Min.Value);

    /// <summary>Whether the value can still be incremented (not disabled/readonly and below Max).</summary>
    public bool CanIncrement => !Disabled && !ReadOnly && (Max is null || (Value ?? Max.Value) < Max.Value);

    // Wired by the root.
    internal Func<double, Task>? StepImpl;
    internal Func<bool, Task>? SetBoundImpl;
    internal Func<string, bool, Task>? SetTextImpl;

    /// <summary>Step the value by an absolute <paramref name="delta"/> (e.g. +Step, -LargeStep), clamped, committed.</summary>
    public Task StepAsync(double delta) => StepImpl?.Invoke(delta) ?? Task.CompletedTask;

    /// <summary>Jump to the Max (<paramref name="max"/>=true) or Min bound (Home/End); no-op when that bound is unset.</summary>
    public Task SetToBoundAsync(bool max) => SetBoundImpl?.Invoke(max) ?? Task.CompletedTask;

    /// <summary>Parse <paramref name="text"/> into the value; <paramref name="commit"/> clamps + reformats (on blur/Enter).</summary>
    public Task SetTextAsync(string text, bool commit) => SetTextImpl?.Invoke(text, commit) ?? Task.CompletedTask;

    /// <summary>Raised when state changes so parts re-render.</summary>
    public event Func<Task>? Changed;

    internal Task NotifyAsync() => Changed is null ? Task.CompletedTask : Changed.Invoke();
}
