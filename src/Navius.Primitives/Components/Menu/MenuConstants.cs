namespace Navius.Primitives.Components.Menu;

/// <summary>Shared constants for the Menu parts.</summary>
internal static class MenuConstants
{
    /// <summary>
    /// All enabled menu-item roles (plain item, checkbox item, radio item, sub-trigger).
    /// Used by SubContent, whose roving container is the sub-content element itself.
    /// </summary>
    public const string ItemRoles =
        "[role=\"menuitem\"]:not([data-disabled])," +
        "[role=\"menuitemcheckbox\"]:not([data-disabled])," +
        "[role=\"menuitemradio\"]:not([data-disabled])";

    /// <summary>
    /// Roving-focus selector for a root Content. Same roles as <see cref="ItemRoles"/>
    /// but excludes anything inside a nested SubContent so the root menu's roving stays
    /// on its own items (an inline SubContent is a DOM descendant of its parent Content;
    /// the submenu runs its own roving). Matches the spec's per-menu focus boundary.
    /// </summary>
    public const string RovingSelector =
        "[role=\"menuitem\"]:not([data-disabled]):not([data-navius-menu-sub-content] *)," +
        "[role=\"menuitemcheckbox\"]:not([data-disabled]):not([data-navius-menu-sub-content] *)," +
        "[role=\"menuitemradio\"]:not([data-disabled]):not([data-navius-menu-sub-content] *)";
}
