using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.NavigationMenu;

/// <summary>
/// A thin per-instance <see cref="IAnchoredOverlayContext"/> the
/// <see cref="NaviusNavigationMenuPopup"/> hands to the shared overlay machinery.
///
/// It resolves the popup's open/anchor surface for one of two layouts:
/// <list type="bullet">
///   <item><b>Standalone</b> (<paramref name="itemValue"/> non-null): the popup belongs to a
///   single Item — it is open iff that item is active and anchors to that item's trigger.</item>
///   <item><b>Shared</b> (<paramref name="itemValue"/> null): the single root-level popup — open
///   iff any item is active and anchors to the ACTIVE trigger (the moving anchor).</item>
/// </list>
/// Everything else (arrow, placement options, portal, close) delegates to the backing
/// <see cref="NavigationMenuContext"/>. <see cref="Changed"/>/<see cref="ArrowChanged"/> forward
/// the backing context's events so the presence machine keys off the same signal.
/// </summary>
internal sealed class NavigationMenuPopupContext : IAnchoredOverlayContext
{
    private readonly NavigationMenuContext _ctx;
    private readonly string? _itemValue;

    public NavigationMenuPopupContext(NavigationMenuContext ctx, string? itemValue)
    {
        _ctx = ctx;
        _itemValue = itemValue;
    }

    /// <summary>The value this popup tracks: its item (standalone) or the active value (shared).</summary>
    public string? ResolvedValue => _itemValue ?? _ctx.Value;

    public bool Open => _itemValue is null ? _ctx.Open : _ctx.IsOpen(_itemValue);

    public bool Modal => false;

    public string ContentId => _itemValue is null ? _ctx.ContentId : _ctx.PopupIdFor(_itemValue);

    public ElementReference TriggerElement =>
        ResolvedValue is not null && _ctx.TryGetTrigger(ResolvedValue, out var e) ? e : default;

    public bool HasTrigger =>
        ResolvedValue is not null && _ctx.TryGetTrigger(ResolvedValue, out _);

    public ElementReference PositionReference => TriggerElement;

    public event Func<Task>? Changed
    {
        add => _ctx.Changed += value;
        remove => _ctx.Changed -= value;
    }

    public Task RequestCloseAsync() => _ctx.RequestCloseAsync();

    // ---- Anchored surface (delegated to the backing context) ------------------

    public ElementReference ArrowElement => _ctx.ArrowElement;
    public bool HasArrow => _ctx.HasArrow;

    public event Func<Task>? ArrowChanged
    {
        add => _ctx.ArrowChanged += value;
        remove => _ctx.ArrowChanged -= value;
    }

    public PositionOptions Options => _ctx.Options;

    public IDictionary<string, object>? PositionerAttributes => _ctx.PositionerAttributes;

    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes) =>
        _ctx.SetPositioner(options, attributes);
}
