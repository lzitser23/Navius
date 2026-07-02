using Microsoft.AspNetCore.Components;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Overlays;

/// <summary>
/// Shared base for a Base UI <c>Positioner</c> part. A flag-setter: it collects the
/// placement options (side / align / offsets / collision) + its own styling
/// attributes and publishes them into the anchored context; the Popup renders the
/// actual positioning <c>&lt;div&gt;</c> (the element the engine transforms) and
/// applies these attributes to it. Renders its ChildContent (the Popup).
///
/// Per-component Positioner razors inherit this and supply
/// <see cref="AnchoredContext"/>; the markup is just <c>@ChildContent</c>.
/// </summary>
public abstract class OverlayPositionerBase : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Preferred side: bottom | top | left | right. Null falls back to <see cref="DefaultSide"/>.</summary>
    [Parameter] public string? Side { get; set; }

    /// <summary>The side used when <see cref="Side"/> is unset (Popover bottom; Tooltip top).</summary>
    protected virtual string DefaultSide => "bottom";

    /// <summary>Alignment along the side: start | center | end. Null falls back to <see cref="DefaultAlign"/>.</summary>
    [Parameter] public string? Align { get; set; }

    /// <summary>The alignment used when <see cref="Align"/> is unset (Menu center; Menubar/Select/Context Menu start).</summary>
    protected virtual string DefaultAlign => "center";

    /// <summary>Distance in px from the anchor along the side (the spec default 0).</summary>
    [Parameter] public double SideOffset { get; set; }

    /// <summary>Offset in px along the alignment axis (the spec default 0).</summary>
    [Parameter] public double AlignOffset { get; set; }

    /// <summary>When collisions occur, flip to the opposite side (folded into <see cref="AvoidCollisions"/>).</summary>
    [Parameter] public bool Flip { get; set; } = true;

    /// <summary>Avoid collisions with the viewport boundary (the spec default true).</summary>
    [Parameter] public bool AvoidCollisions { get; set; } = true;

    /// <summary>Padding in px between the popup and the collision boundary.</summary>
    [Parameter] public double? CollisionPadding { get; set; }

    /// <summary>Sticky behaviour while scrolling: "partial" | "always". Null falls back to <see cref="DefaultSticky"/>.</summary>
    [Parameter] public string? Sticky { get; set; }

    /// <summary>The sticky mode used when <see cref="Sticky"/> is unset (Preview Card defaults to "partial").</summary>
    protected virtual string? DefaultSticky => null;

    /// <summary>Hide the popup (data-anchor-hidden) when the anchor is fully clipped/detached.</summary>
    [Parameter] public bool HideWhenDetached { get; set; }

    /// <summary>Padding in px between the arrow and the popup edges.</summary>
    [Parameter] public double ArrowPadding { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>The anchored context the placement options are published to (supplied by the razor).</summary>
    protected abstract IAnchoredOverlayContext AnchoredContext { get; }

    protected override void OnParametersSet()
    {
        AnchoredContext.SetPositioner(
            new PositionOptions(
                Side ?? DefaultSide, Align ?? DefaultAlign, SideOffset, AlignOffset, Flip,
                AvoidCollisions: AvoidCollisions,
                CollisionPadding: CollisionPadding,
                Sticky: Sticky ?? DefaultSticky,
                HideWhenDetached: HideWhenDetached,
                ArrowPadding: ArrowPadding),
            Attributes);
    }
}
