namespace Navius.Primitives.Components.Toast;

/// <summary>
/// A single toast in the <see cref="ToastManager"/> store. A class (not a record) because
/// the transient stack fields (<see cref="Open"/>, <see cref="Limited"/>,
/// <see cref="UpdateKey"/>) mutate in place while the toast lives. Mirrors Base UI's
/// <c>ToastObject</c>. Replaces the old <c>ToastItem</c> record.
/// </summary>
public sealed class ToastObject
{
    /// <summary>Stable id, used as the render <c>@key</c> and for <c>Close</c>/<c>Update</c>.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Title text; rendered by <c>NaviusToastTitle</c> when it has no ChildContent.</summary>
    public string? Title { get; set; }

    /// <summary>Description text; rendered by <c>NaviusToastDescription</c> when it has no ChildContent.</summary>
    public string? Description { get; set; }

    /// <summary>Visual/semantic type: <c>success | error | loading | null</c>. Drives <c>data-type</c>. Orthogonal to <see cref="Priority"/>.</summary>
    public string? Type { get; set; }

    /// <summary>Announcement urgency: <c>low</c> (role=status, polite) or <c>high</c> (role=alert, assertive).</summary>
    public string Priority { get; set; } = "low";

    /// <summary>Auto-close in ms; <c>null</c> → the Provider default; <c>0</c> → sticky (no auto-close).</summary>
    public int? Timeout { get; set; }

    /// <summary>Arbitrary consumer payload.</summary>
    public object? Data { get; set; }

    /// <summary>Optional action button (label + accessible alt text + handler).</summary>
    public ToastActionProps? Action { get; set; }

    /// <summary>Invoked once the toast has been removed (after its exit animation).</summary>
    public Func<Task>? OnClose { get; set; }

    // ---- transient (the Manager / Root own these; not consumer-set) --------------------

    /// <summary>True while live; flipped false by <see cref="ToastManager.Close"/> to start the exit animation.</summary>
    public bool Open { get; set; } = true;

    /// <summary>Computed: this toast is queued beyond <see cref="ToastManager.Limit"/> among the open toasts.</summary>
    public bool Limited { get; internal set; }

    /// <summary>Bumped by <see cref="ToastManager.Update"/> so the Root can replay the enter animation.</summary>
    public int UpdateKey { get; internal set; }
}

/// <summary>An action button on a manager-driven toast: a label, required alt text, and an optional handler.</summary>
public sealed record ToastActionProps(string Label, string AltText, Func<Task>? OnClick = null);

/// <summary>Options for <see cref="ToastManager.Add"/> / <see cref="ToastManager.Update"/>.</summary>
public sealed record ToastOptions(
    string? Title = null,
    string? Description = null,
    string? Type = null,
    string Priority = "low",
    int? Timeout = null,
    object? Data = null,
    ToastActionProps? Action = null,
    Func<Task>? OnClose = null);

/// <summary>The three status strings shown across a <see cref="ToastManager.Promise{T}"/> lifecycle.</summary>
public sealed record PromiseMessages(string Loading, string Success, string Error);
