namespace Navius.Primitives.Common;

/// <summary>
/// Cancelable arguments raised when the Escape key is pressed while a part is
/// open. Call <see cref="PreventDefault"/> to keep the part open. Maps the spec's
/// <c>onEscapeKeyDown</c>.
/// </summary>
public class NaviusEscapeKeyDownEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the default dismissal is skipped.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}

/// <summary>
/// Cancelable arguments raised when a pointer-down occurs outside a part's
/// content. Call <see cref="PreventDefault"/> to prevent the default dismissal.
/// Maps the spec's <c>onPointerDownOutside</c>.
/// </summary>
public class NaviusPointerDownOutsideEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the default dismissal is skipped.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}

/// <summary>
/// Cancelable arguments raised when focus moves outside a part's content. Call
/// <see cref="PreventDefault"/> to prevent the default dismissal. Maps the spec's
/// <c>onFocusOutside</c>.
/// </summary>
public class NaviusFocusOutsideEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the default dismissal is skipped.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}

/// <summary>
/// Cancelable arguments raised when an interaction (pointer or focus) occurs
/// outside a part's content. Call <see cref="PreventDefault"/> to prevent the
/// default dismissal. Maps the spec's <c>onInteractOutside</c>.
/// </summary>
public class NaviusInteractOutsideEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the default dismissal is skipped.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}

/// <summary>
/// Cancelable arguments raised when content opens and focus is about to move into
/// it. Call <see cref="PreventDefault"/> to keep focus where it is. Maps the spec's
/// <c>onOpenAutoFocus</c>.
/// </summary>
public class NaviusOpenAutoFocusEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the default auto-focus is skipped.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}

/// <summary>
/// Cancelable arguments raised when content closes and focus is about to return
/// to the trigger. Call <see cref="PreventDefault"/> to manage focus yourself.
/// Maps the spec's <c>onCloseAutoFocus</c>.
/// </summary>
public class NaviusCloseAutoFocusEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the default auto-focus is skipped.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}

/// <summary>
/// Cancelable arguments raised when a menu item is selected. Call
/// <see cref="PreventDefault"/> to keep the menu open after selection. Maps
/// the spec's menu item <c>onSelect</c>.
/// </summary>
public class NaviusSelectEventArgs
{
    /// <summary>True once <see cref="PreventDefault"/> has been called.</summary>
    public bool DefaultPrevented { get; private set; }

    /// <summary>Marks the event as handled so the menu is not auto-closed.</summary>
    public void PreventDefault() => DefaultPrevented = true;
}
