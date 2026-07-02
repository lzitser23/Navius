namespace Navius.Primitives.Components.ContextMenu;

/// <summary>
/// Wires a <see cref="NaviusContextMenuLabel"/> inside a <see cref="NaviusContextMenuGroup"/>
/// to the group's <c>aria-labelledby</c>: the label registers its generated id here and the
/// group renders it. Mirrors the spec, where a Label inside a Group labels the group for SRs.
/// </summary>
public sealed class ContextMenuGroupContext
{
    /// <summary>The id of the label that names this group, or null when no Label is present.</summary>
    public string? LabelId { get; private set; }

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
