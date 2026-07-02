namespace Navius.Primitives.Components.Menu;

/// <summary>
/// Cascaded from <c>NaviusMenuGroup</c> to a <c>NaviusMenuLabel</c>
/// inside it. The label publishes its generated id here; the group references it via
/// <c>aria-labelledby</c> so the group is announced with the label as its heading,
/// matching the spec's Group/Label pairing.
/// </summary>
public sealed class MenuGroupContext
{
    /// <summary>Id of the label that names this group (null until a Label registers).</summary>
    public string? LabelId { get; private set; }

    /// <summary>Raised when the label id changes so the group re-renders with aria-labelledby.</summary>
    public event Action? Changed;

    /// <summary>Called by a Label to register its id as the group's accessible name.</summary>
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
