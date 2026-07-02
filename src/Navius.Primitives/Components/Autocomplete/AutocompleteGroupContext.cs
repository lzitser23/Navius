namespace Navius.Primitives.Components.Autocomplete;

/// <summary>
/// Cascaded from <c>NaviusAutocompleteGroup</c> to a <c>NaviusAutocompleteGroupLabel</c>
/// inside it. The label publishes its generated id here; the group references it via
/// <c>aria-labelledby</c> so the group is announced with the label as its heading.
/// </summary>
public sealed class AutocompleteGroupContext
{
    /// <summary>Id of the label that names this group (null until a Label registers).</summary>
    public string? LabelId { get; private set; }

    /// <summary>Raised when the label id changes so the group re-renders with aria-labelledby.</summary>
    public event Action? Changed;

    public void SetLabelId(string id)
    {
        if (LabelId == id)
        {
            return;
        }

        LabelId = id;
        Changed?.Invoke();
    }
}
