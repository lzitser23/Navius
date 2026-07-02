namespace Navius.Primitives.Components.RadioGroup;

/// <summary>
/// Per-item state cascaded from <c>NaviusRadioGroupItem</c> down to its
/// <c>NaviusRadioGroupIndicator</c> child, so the indicator can mirror the
/// item's discrete state (checked/disabled/readonly/required) without reaching
/// back into the group.
/// </summary>
public sealed class RadioGroupItemContext
{
    private readonly Func<bool> _isChecked;
    private readonly Func<bool> _isDisabled;
    private readonly Func<bool> _isReadOnly;
    private readonly Func<bool> _isRequired;

    public RadioGroupItemContext(Func<bool> isChecked, Func<bool> isDisabled, Func<bool> isReadOnly, Func<bool> isRequired)
    {
        _isChecked = isChecked;
        _isDisabled = isDisabled;
        _isReadOnly = isReadOnly;
        _isRequired = isRequired;
    }

    public bool IsChecked => _isChecked();

    public bool IsDisabled => _isDisabled();

    public bool IsReadOnly => _isReadOnly();

    public bool IsRequired => _isRequired();
}
