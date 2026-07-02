namespace Navius.Primitives.Components.Checkbox;

/// <summary>
/// Cascaded from <see cref="NaviusCheckboxGroup"/> to its child checkboxes. Holds
/// the group's checked-value set (by checkbox <c>Name</c>) and the toggle plumbing
/// so children derive their checked state from membership and route changes back
/// through the group. A <c>parent</c> checkbox reads <see cref="ParentState"/> to
/// roll up to checked / unchecked / indeterminate over <see cref="AllValues"/>.
/// </summary>
public sealed class CheckboxGroupContext
{
    private readonly Func<IReadOnlyList<string>> _value;
    private readonly Func<IReadOnlyList<string>?> _allValues;
    private readonly Func<string, bool, Task> _setMember;
    private readonly Func<bool, Task> _setAll;

    public CheckboxGroupContext(
        Func<IReadOnlyList<string>> value,
        Func<IReadOnlyList<string>?> allValues,
        Func<string, bool, Task> setMember,
        Func<bool, Task> setAll,
        bool disabled)
    {
        _value = value;
        _allValues = allValues;
        _setMember = setMember;
        _setAll = setAll;
        Disabled = disabled;
    }

    /// <summary>Whether the whole group is disabled.</summary>
    public bool Disabled { get; }

    /// <summary>The names of the currently-checked child checkboxes.</summary>
    public IReadOnlyList<string> Value => _value();

    /// <summary>The names of every child (set on the group for a parent checkbox).</summary>
    public IReadOnlyList<string>? AllValues => _allValues();

    /// <summary>Whether the child named <paramref name="name"/> is currently checked.</summary>
    public bool IsChecked(string name) => Value.Contains(name);

    /// <summary>
    /// Roll-up state for a parent checkbox over <see cref="AllValues"/>: <c>true</c>
    /// when all are checked, <c>false</c> when none, <c>null</c> (indeterminate) when some.
    /// </summary>
    public bool? ParentState
    {
        get
        {
            var all = AllValues;
            if (all is null || all.Count == 0)
            {
                return false;
            }

            var checkedCount = all.Count(v => Value.Contains(v));
            if (checkedCount == 0)
            {
                return false;
            }

            return checkedCount == all.Count ? true : (bool?)null;
        }
    }

    /// <summary>Add/remove a child by name (routes through the group's value change).</summary>
    public Task SetMemberAsync(string name, bool isChecked) => _setMember(name, isChecked);

    /// <summary>Check or uncheck every value in <see cref="AllValues"/> (parent checkbox click).</summary>
    public Task SetAllAsync(bool isChecked) => _setAll(isChecked);

    /// <summary>Raised when the group value changes so children + the parent re-render.</summary>
    public event Action? Changed;

    internal void NotifyChanged() => Changed?.Invoke();
}
