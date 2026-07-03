using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.MessageScroller;

/// <summary>
/// Shared state for one message scroller. Cascaded (fixed) by
/// <c>NaviusMessageScrollerProvider</c>; works anywhere inside the provider,
/// including outside the scroller frame. Exposes the hook equivalents:
/// the scroll commands (<see cref="ScrollToMessageAsync"/> /
/// <see cref="ScrollToStartAsync"/> / <see cref="ScrollToEndAsync"/>), the
/// scrollable-edge state (<see cref="ScrollableStart"/> / <see cref="ScrollableEnd"/>)
/// and the lazy visibility subscription (<see cref="SubscribeVisibilityAsync"/>),
/// which starts engine-side tracking only while at least one subscriber listens.
/// The scroll hot path stays in the engine; this context only carries the discrete
/// state parts re-render on.
/// </summary>
public sealed class MessageScrollerContext
{
    private Interop.MessageScroller? _engine;
    private MessageScrollerOptions _providerOptions = new();
    private bool _preserveScrollOnPrepend = true;
    private (string Id, MessageScrollerScrollOptions? Options)? _pendingTarget;
    private int _visibilitySubscribers;
    private Func<Task>? _visibilityChanged;

    /// <summary>Raised when the scrollable-edge state changes; parts re-render on it.</summary>
    public event Func<Task>? Changed;

    /// <summary>Whether the viewport can still scroll toward the start.</summary>
    public bool ScrollableStart { get; private set; }

    /// <summary>Whether the viewport can still scroll toward the end (the live edge).</summary>
    public bool ScrollableEnd { get; private set; }

    /// <summary>
    /// The last anchor row at or above the reading line (null before tracking runs or
    /// when no anchor has been passed). Only updates while a visibility subscriber listens.
    /// </summary>
    public string? CurrentAnchorId { get; private set; }

    /// <summary>
    /// Message ids intersecting the viewport, in document order. Only updates while a
    /// visibility subscriber listens.
    /// </summary>
    public IReadOnlyList<string> VisibleMessageIds { get; private set; } = Array.Empty<string>();

    /// <summary>The effective engine options (provider options + the viewport's prepend flag).</summary>
    public MessageScrollerOptions Options =>
        _providerOptions with { PreserveScrollOnPrepend = _preserveScrollOnPrepend };

    /// <summary>
    /// Scroll the row with <paramref name="messageId"/> into view. Queues the target when
    /// called before the viewport mounts or before any rows exist (client-resolved
    /// permalinks); returns false for an id missing from a mounted transcript.
    /// </summary>
    public async Task<bool> ScrollToMessageAsync(string messageId, MessageScrollerScrollOptions? options = null)
    {
        if (_engine is null)
        {
            _pendingTarget = (messageId, options);
            return true;
        }

        return await _engine.ScrollToMessageAsync(messageId, options);
    }

    /// <summary>Scroll to the start of the transcript. False when the viewport is not mounted.</summary>
    public Task<bool> ScrollToStartAsync(MessageScrollerScrollOptions? options = null) =>
        _engine is null ? Task.FromResult(false) : _engine.ScrollToStartAsync(options);

    /// <summary>
    /// Scroll to the live edge (re-engages follow when AutoScroll is enabled). False when
    /// the viewport is not mounted.
    /// </summary>
    public Task<bool> ScrollToEndAsync(MessageScrollerScrollOptions? options = null) =>
        _engine is null ? Task.FromResult(false) : _engine.ScrollToEndAsync(options);

    /// <summary>
    /// Subscribe to visibility updates (<see cref="CurrentAnchorId"/> +
    /// <see cref="VisibleMessageIds"/>). The first subscriber starts engine-side
    /// tracking, so an unused subscription costs nothing.
    /// </summary>
    public async Task SubscribeVisibilityAsync(Func<Task> handler)
    {
        _visibilityChanged += handler;
        if (++_visibilitySubscribers == 1 && _engine is not null)
        {
            await _engine.SetVisibilityTrackingAsync(true);
        }
    }

    /// <summary>Remove a visibility subscription; the last one stops engine-side tracking.</summary>
    public async Task UnsubscribeVisibilityAsync(Func<Task> handler)
    {
        _visibilityChanged -= handler;
        if (_visibilitySubscribers > 0 && --_visibilitySubscribers == 0 && _engine is not null)
        {
            await _engine.SetVisibilityTrackingAsync(false);
        }
    }

    internal async Task AttachEngineAsync(Interop.MessageScroller engine)
    {
        _engine = engine;
        if (_visibilitySubscribers > 0)
        {
            await engine.SetVisibilityTrackingAsync(true);
        }

        if (_pendingTarget is { } target)
        {
            _pendingTarget = null;
            await engine.ScrollToMessageAsync(target.Id, target.Options);
        }
    }

    internal void DetachEngine() => _engine = null;

    internal async Task SetProviderOptionsInternalAsync(MessageScrollerOptions options)
    {
        if (_providerOptions == options) return;
        _providerOptions = options;
        if (_engine is not null)
        {
            await _engine.UpdateAsync(Options);
        }
    }

    internal async Task SetPreserveScrollOnPrependInternalAsync(bool preserve)
    {
        if (_preserveScrollOnPrepend == preserve) return;
        _preserveScrollOnPrepend = preserve;
        if (_engine is not null)
        {
            await _engine.UpdateAsync(Options);
        }
    }

    internal async Task SetScrollableInternalAsync(bool start, bool end)
    {
        if (ScrollableStart == start && ScrollableEnd == end) return;
        ScrollableStart = start;
        ScrollableEnd = end;
        await RaiseChangedAsync();
    }

    internal async Task SetVisibilityInternalAsync(string? currentAnchorId, string[] visibleMessageIds)
    {
        CurrentAnchorId = currentAnchorId;
        VisibleMessageIds = visibleMessageIds;
        if (_visibilityChanged is not null)
        {
            await _visibilityChanged.Invoke();
        }
    }

    private Task RaiseChangedAsync() => Changed is null ? Task.CompletedTask : Changed.Invoke();
}
