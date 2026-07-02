using Microsoft.AspNetCore.Components;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Overlays;

/// <summary>
/// The shared open/close surface every overlay context (Popover, Tooltip, Preview
/// Card, Dialog, Alert Dialog, Drawer) exposes to the reusable overlay parts
/// (<see cref="OverlayPresence"/>, <see cref="OverlayPopupBase"/>). The concrete
/// per-component context is cascaded; the shared bases read it through this
/// interface so they stay component-agnostic. Replaces the five duplicated
/// <c>*Part.cs</c> bases the per-component code previously hand-rolled.
/// </summary>
public interface IOverlayContext
{
    /// <summary>Whether the overlay is currently open.</summary>
    bool Open { get; }

    /// <summary>Modal mode: when true the popup traps focus and locks page scroll.</summary>
    bool Modal { get; }

    /// <summary>Stable id wired onto the Popup (<c>id</c>) and the Trigger (<c>aria-controls</c>).</summary>
    string ContentId { get; }

    /// <summary>The trigger element; the dismissable layer treats it as "inside" so its click toggles.</summary>
    ElementReference TriggerElement { get; }
    bool HasTrigger { get; }

    /// <summary>Raised when the open state changes so parts re-render.</summary>
    event Func<Task>? Changed;

    /// <summary>Request the overlay close (routed through the controlled/uncontrolled owner).</summary>
    Task RequestCloseAsync();
}

/// <summary>
/// Adds the anchored-positioning surface for floating overlays whose popup is
/// placed against a trigger/anchor (Popover, Tooltip, Preview Card). The Positioner
/// part publishes <see cref="Options"/> + <see cref="PositionerAttributes"/> into the
/// context; the Popup reads them to engage the engine and style the positioning div.
/// </summary>
public interface IAnchoredOverlayContext : IOverlayContext
{
    /// <summary>The element the popup anchors to: an explicit anchor if present, else the trigger.</summary>
    ElementReference PositionReference { get; }

    /// <summary>The arrow element (when an Arrow part is mounted), wired into the positioner.</summary>
    ElementReference ArrowElement { get; }
    bool HasArrow { get; }

    /// <summary>Raised when an arrow registers/unregisters so the popup can re-engage the positioner.</summary>
    event Func<Task>? ArrowChanged;

    /// <summary>Placement options collected by the Positioner part.</summary>
    PositionOptions Options { get; }

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    IDictionary<string, object>? PositionerAttributes { get; }

    /// <summary>Published by the Positioner part: placement options + its styling attributes.</summary>
    void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes);
}
