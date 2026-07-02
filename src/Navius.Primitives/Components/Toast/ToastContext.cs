namespace Navius.Primitives.Components.Toast;

/// <summary>
/// Per-<see cref="NaviusToastRoot"/> state, cascaded to its Title / Description / Action /
/// Close / Content parts. Carries the aria id wiring + the request-close channel (as
/// before) plus the manager-driven fields the unified Root now publishes: the source
/// <see cref="Toast"/>, the visual <see cref="Type"/> / <see cref="Priority"/>, the
/// stacking <see cref="Index"/> / <see cref="Height"/>, and the live
/// <see cref="Expanded"/> / <see cref="Limited"/> / <see cref="Swiping"/> flags.
/// </summary>
public sealed class ToastContext
{
    private readonly Func<Task> _requestClose;

    public ToastContext(Func<Task> requestClose) => _requestClose = requestClose;

    public string TitleId { get; } = $"navius-toast-title-{Guid.NewGuid():N}";

    public string DescriptionId { get; } = $"navius-toast-description-{Guid.NewGuid():N}";

    private bool _hasTitle;
    private bool _hasDescription;

    /// <summary>Raised when title/description presence changes so the Root can re-wire aria-* attributes.</summary>
    public event Action? Changed;

    /// <summary>Set true once a Title part has rendered.</summary>
    public bool HasTitle
    {
        get => _hasTitle;
        set { if (_hasTitle != value) { _hasTitle = value; Changed?.Invoke(); } }
    }

    /// <summary>Set true once a Description part has rendered.</summary>
    public bool HasDescription
    {
        get => _hasDescription;
        set { if (_hasDescription != value) { _hasDescription = value; Changed?.Invoke(); } }
    }

    // ---- manager-driven + stacking state (published by the Root each render) -------------

    /// <summary>The source toast when manager-driven; null for a manual toast.</summary>
    public ToastObject? Toast { get; set; }

    /// <summary>Visual/semantic type (success | error | loading | null) → <c>data-type</c>.</summary>
    public string? Type { get; set; }

    /// <summary>Announcement urgency (low | high) → role status/alert.</summary>
    public string Priority { get; set; } = "low";

    /// <summary>Stack position among the visible toasts (0 = frontmost).</summary>
    public int Index { get; set; }

    /// <summary>True when not the frontmost toast (hidden behind in the collapsed stack).</summary>
    public bool Behind => Index > 0;

    /// <summary>Mirror of the viewport's expanded state, for content-level styling.</summary>
    public bool Expanded { get; set; }

    /// <summary>True when this toast is queued beyond the Provider's limit.</summary>
    public bool Limited { get; set; }

    /// <summary>True while a swipe gesture is in progress.</summary>
    public bool Swiping { get; set; }

    /// <summary>The effective swipe direction for this toast.</summary>
    public string SwipeDirection { get; set; } = "right";

    /// <summary>This toast's measured height in px (via <c>GetRectAsync</c>).</summary>
    public double Height { get; set; }

    /// <summary>Closes the toast (routes to <c>Manager.Close(id)</c> when manager-driven). Used by Close/Action parts.</summary>
    public Task RequestCloseAsync() => _requestClose();
}
