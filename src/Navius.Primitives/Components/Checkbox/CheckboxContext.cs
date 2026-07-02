namespace Navius.Primitives.Components.Checkbox;

/// <summary>
/// Shared state cascaded from <c>NaviusCheckbox</c> to its
/// <c>NaviusCheckboxIndicator</c>. Mirrors the spec's tri-state checkbox: a null
/// <see cref="Checked"/> represents the indeterminate state. The indicator reads
/// this to decide whether to mount and which discrete data attributes
/// (<c>data-checked</c>/<c>data-unchecked</c>/<c>data-indeterminate</c> +
/// <c>data-disabled</c>/<c>data-readonly</c>/<c>data-required</c>) to emit.
/// </summary>
public sealed class CheckboxContext
{
    /// <summary>Current check state. <c>true</c>=checked, <c>false</c>=unchecked, <c>null</c>=indeterminate.</summary>
    public bool? Checked { get; internal set; }

    /// <summary>Whether the owning checkbox is disabled.</summary>
    public bool Disabled { get; internal set; }

    /// <summary>Whether the owning checkbox is read-only.</summary>
    public bool ReadOnly { get; internal set; }

    /// <summary>Whether the owning checkbox is required.</summary>
    public bool Required { get; internal set; }

    /// <summary>True when checked or indeterminate (the spec mounts the indicator in both cases).</summary>
    public bool IsPresent => Checked != false;

    /// <summary>Raised by the root when state/disabled changes so the indicator re-renders.</summary>
    public event Action? Changed;

    internal void NotifyChanged() => Changed?.Invoke();
}
