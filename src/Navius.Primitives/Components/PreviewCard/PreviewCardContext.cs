using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.PreviewCard;

/// <summary>
/// Shared state for one preview card (Base UI <c>PreviewCard</c>, the renamed
/// HoverCard). The root owns the open state plus the open/close timing; the trigger
/// drives it via pointer-enter/leave and focus/blur, the popup positions against
/// <see cref="TriggerElement"/>. Implements <see cref="IAnchoredOverlayContext"/> so
/// it reuses the shared overlay machinery (<see cref="OverlayAnchoredPopupBase"/>).
/// </summary>
public sealed class PreviewCardContext : IAnchoredOverlayContext
{
    private readonly Func<Task> _openDelayed;
    private readonly Func<Task> _openNow;
    private readonly Func<Task> _closeDelayed;
    private readonly Func<Task> _closeNow;

    public PreviewCardContext(Func<Task> openDelayed, Func<Task> openNow, Func<Task> closeDelayed, Func<Task> closeNow)
    {
        _openDelayed = openDelayed;
        _openNow = openNow;
        _closeDelayed = closeDelayed;
        _closeNow = closeNow;
    }

    public bool Open { get; private set; }

    /// <summary>Preview cards are never modal — no focus trap, scroll lock, or focus move.</summary>
    public bool Modal => false;

    public string ContentId { get; } = $"navius-preview-card-{Guid.NewGuid():N}";

    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>Preview cards anchor to the trigger (no separate anchor part).</summary>
    public ElementReference PositionReference => TriggerElement;

    /// <summary>Custom portal mount-container selector; null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the popup mounted while closed (the spec <c>Portal.keepMounted</c>).</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by the Positioner part (which seeds Sticky="partial").</summary>
    public PositionOptions Options { get; private set; } = new(SideOffset: 0);

    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>The arrow element, registered by <c>NaviusPreviewCardArrow</c> for the positioner to align.</summary>
    public ElementReference ArrowElement { get; private set; }
    public bool HasArrow { get; private set; }

    public event Func<Task>? ArrowChanged;

    public async Task RegisterArrowAsync(ElementReference arrow)
    {
        ArrowElement = arrow;
        HasArrow = true;
        if (ArrowChanged is not null)
        {
            await ArrowChanged.Invoke();
        }
    }

    public async Task UnregisterArrowAsync()
    {
        HasArrow = false;
        ArrowElement = default;
        if (ArrowChanged is not null)
        {
            await ArrowChanged.Invoke();
        }
    }

    public event Func<Task>? Changed;

    internal async Task SetOpenInternalAsync(bool open)
    {
        if (Open == open)
        {
            return;
        }

        Open = open;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    /// <summary>Open after the configured open delay (hover intent).</summary>
    public Task OpenDelayed() => _openDelayed();

    /// <summary>Open immediately (e.g. keyboard focus — no delay).</summary>
    public Task OpenNow() => _openNow();

    /// <summary>Close after the configured close delay (pointer leave/blur).</summary>
    public Task CloseDelayed() => _closeDelayed();

    /// <summary>Close immediately (e.g. Escape or outside dismiss).</summary>
    public Task CloseNow() => _closeNow();

    /// <summary>Force-close (Escape / outside pointer) — maps to <see cref="CloseNow"/>.</summary>
    public Task RequestCloseAsync() => _closeNow();
}
