namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Cascaded by a <see cref="NaviusMenubarGroup"/> so a <see cref="NaviusMenubarLabel"/>
/// rendered inside it can register its generated id; the group then exposes that id as
/// <c>aria-labelledby</c> for an accessible group name (the spec wiring).
/// </summary>
public sealed class MenubarGroupContext
{
    private readonly Action<string> _registerLabelId;

    public MenubarGroupContext(Action<string> registerLabelId) => _registerLabelId = registerLabelId;

    /// <summary>Called by a nested label to hand the group its id for <c>aria-labelledby</c>.</summary>
    public void RegisterLabel(string id) => _registerLabelId(id);
}
