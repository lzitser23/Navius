using System.Threading;
using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ScrollArea;

/// <summary>
/// Shared state for one scroll area, cascaded from <see cref="NaviusScrollArea"/> to
/// its parts. The viewport pushes raw scroll metrics here (from the engine's
/// <c>observeScrollArea</c>) and the root computes derived geometry — thumb size and
/// offset per axis, whether each axis overflows, and (for hover/scroll
/// <c>type</c>) whether each scrollbar should currently be visible. Parts read this
/// to size/position the thumb and to drive <c>data-hovering</c>/<c>data-scrolling</c>.
/// </summary>
public sealed class ScrollAreaContext : IDisposable
{
    /// <summary>Raw scroll metrics for both axes, as reported by the engine.</summary>
    public sealed record ScrollMetrics(
        double ScrollTop,
        double ScrollHeight,
        double ClientHeight,
        double ScrollLeft,
        double ScrollWidth,
        double ClientWidth);

    public ScrollAreaContext(string type, int scrollHideDelay = 600, string dir = "ltr")
    {
        Type = type;
        ScrollHideDelay = scrollHideDelay;
        Dir = dir;
    }

    /// <summary><c>auto</c> | <c>always</c> | <c>hover</c> | <c>scroll</c>.</summary>
    public string Type { get; }

    /// <summary>
    /// Delay in milliseconds before the scrollbar hides again after the most recent
    /// scroll / hover-leave, for <c>type="hover"</c> and <c>type="scroll"</c>
    /// (the spec <c>scrollHideDelay</c>, default 600).
    /// </summary>
    public int ScrollHideDelay { get; }

    /// <summary>Resolved reading direction (<c>ltr</c> | <c>rtl</c>) for horizontal thumb math.</summary>
    public string Dir { get; }

    /// <summary>Last reported metrics; defaults to a non-overflowing zero state.</summary>
    public ScrollMetrics Metrics { get; private set; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>The scrollable viewport element — set by the viewport, read for drag math.</summary>
    public ElementReference ViewportElement { get; set; }
    public bool HasViewport { get; set; }

    /// <summary>True while the user is hovering the root (drives <c>type="hover"</c>).</summary>
    public bool Hovering { get; private set; }

    /// <summary>True for a short window after the most recent scroll (drives <c>type="scroll"</c>).</summary>
    public bool Scrolling { get; private set; }

    public event Func<Task>? Changed;

    /// <summary>Live pointer-inside flag (immediate), distinct from the delayed <see cref="Hovering"/>.</summary>
    private bool _pointerInside;

    /// <summary>Single shared timer that defers the hover/scroll hide by <see cref="ScrollHideDelay"/>.</summary>
    private Timer? _hideTimer;

    public bool HasVerticalOverflow => Metrics.ScrollHeight - Metrics.ClientHeight > 1;

    public bool HasHorizontalOverflow => Metrics.ScrollWidth - Metrics.ClientWidth > 1;

    public bool HasOverflow(string orientation) =>
        orientation == "horizontal" ? HasHorizontalOverflow : HasVerticalOverflow;

    /// <summary>
    /// Whether the scrollbar for <paramref name="orientation"/> should be visible right
    /// now, given the <see cref="Type"/> and current overflow / interaction state.
    /// </summary>
    public bool IsScrollbarVisible(string orientation)
    {
        // type="always" keeps the scrollbar present even with no overflow; every other
        // type gates on the axis actually overflowing.
        if (Type == "always")
        {
            return true;
        }

        if (!HasOverflow(orientation))
        {
            return false;
        }

        return Type switch
        {
            "auto" => true,
            "hover" => Hovering || Scrolling,
            "scroll" => Scrolling,
            _ => true,
        };
    }

    /// <summary>
    /// Thumb size as a fraction (0–1) of the track for <paramref name="orientation"/>:
    /// <c>clientLength / scrollLength</c>, clamped so a tiny thumb stays grabbable.
    /// </summary>
    public double ThumbSizeRatio(string orientation)
    {
        var (client, scroll) = orientation == "horizontal"
            ? (Metrics.ClientWidth, Metrics.ScrollWidth)
            : (Metrics.ClientHeight, Metrics.ScrollHeight);

        if (scroll <= 0)
        {
            return 1;
        }

        var ratio = client / scroll;
        return Math.Clamp(ratio, 0d, 1d);
    }

    /// <summary>
    /// Thumb offset as a fraction (0–1) of the *remaining* track travel for
    /// <paramref name="orientation"/>: <c>scrollOffset / (scrollLength - clientLength)</c>.
    /// Multiply by <c>(1 - sizeRatio)</c> to get the top/left percentage.
    /// </summary>
    public double ThumbOffsetRatio(string orientation)
    {
        var (offset, scroll, client) = orientation == "horizontal"
            ? (Metrics.ScrollLeft, Metrics.ScrollWidth, Metrics.ClientWidth)
            : (Metrics.ScrollTop, Metrics.ScrollHeight, Metrics.ClientHeight);

        var max = scroll - client;
        if (max <= 0)
        {
            return 0;
        }

        // RTL horizontal: per the modern (negative) scrollLeft model the scroll origin
        // is at the right edge and scrollLeft runs 0..-max moving leftward. Normalise to
        // a 0..max travel-from-start magnitude so the thumb offset is mirrored correctly
        // and matches the left:offset% layout (start = right edge).
        if (orientation == "horizontal" && Dir == "rtl")
        {
            offset = -offset;
        }

        return Math.Clamp(offset / max, 0d, 1d);
    }

    /// <summary>True when both axes overflow — the corner fills the gap between scrollbars.</summary>
    public bool ShowCorner => HasVerticalOverflow && HasHorizontalOverflow;

    /// <summary>Called by the viewport when the engine reports fresh metrics.</summary>
    internal async Task SetMetricsAsync(ScrollMetrics metrics)
    {
        Metrics = metrics;
        await RaiseAsync();
    }

    internal async Task SetHoveringAsync(bool hovering)
    {
        _pointerInside = hovering;

        if (hovering)
        {
            // Entering cancels any pending hide and shows immediately.
            _hideTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            if (Hovering)
            {
                return;
            }

            Hovering = true;
            await RaiseAsync();
            return;
        }

        // Pointer-leave: keep the scrollbar visible for ScrollHideDelay before hiding
        // (the spec delays the hover-out transition rather than flipping instantly).
        if (!Hovering)
        {
            return;
        }

        ScheduleHide();
    }

    internal async Task SetScrollingAsync(bool scrolling)
    {
        if (Scrolling == scrolling)
        {
            return;
        }

        Scrolling = scrolling;
        await RaiseAsync();
    }

    /// <summary>
    /// Called on every native scroll (engine <c>OnScrollActivity</c>): reveals the
    /// scrollbar immediately and (re)arms the hide timer so it fades
    /// <see cref="ScrollHideDelay"/> ms after scrolling stops — the spec
    /// <c>type="scroll"</c> / <c>type="hover"</c> just-in-time reveal model.
    /// </summary>
    internal async Task NotifyScrollActivityAsync()
    {
        if (!Scrolling)
        {
            Scrolling = true;
            await RaiseAsync();
        }

        ScheduleHide();
    }

    /// <summary>(Re)arm the single hide timer to clear scrolling + delayed hover after the delay.</summary>
    private void ScheduleHide()
    {
        _hideTimer ??= new Timer(_ => _ = OnHideElapsedAsync());
        _hideTimer.Change(ScrollHideDelay, Timeout.Infinite);
    }

    private async Task OnHideElapsedAsync()
    {
        var changed = false;

        if (Scrolling)
        {
            Scrolling = false;
            changed = true;
        }

        // A deferred pointer-leave also resolves here so the bar fades together.
        if (Hovering && !_pointerInside)
        {
            Hovering = false;
            changed = true;
        }

        if (changed)
        {
            await RaiseAsync();
        }
    }

    private Task RaiseAsync() => Changed is null ? Task.CompletedTask : Changed.Invoke();

    public void Dispose() => _hideTimer?.Dispose();
}
