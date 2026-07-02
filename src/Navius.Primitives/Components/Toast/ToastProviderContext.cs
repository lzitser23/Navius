using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Toast;

/// <summary>
/// Cascaded configuration + shared runtime state for a unified toast tree. Holds the
/// <see cref="ToastManager"/>, the Base UI Provider props (<c>timeout</c>, <c>label</c>,
/// <c>swipeDirection</c>, <c>swipeThreshold</c>), the viewport <c>Expanded</c> flag, the
/// accessibility announcer broker, and the measured-height aggregation that feeds the
/// stacking CSS vars. Read by every Viewport / Root / Content.
/// </summary>
public sealed class ToastProviderContext
{
    /// <summary>The imperative store driving the tree (injected or Provider-supplied).</summary>
    public ToastManager Manager { get; set; } = default!;

    /// <summary>Default auto-close duration (ms). Overridable per <see cref="NaviusToastRoot"/>.</summary>
    public int Timeout { get; set; } = 5000;

    /// <summary>Label prefix for the viewport's accessible name and the announcer.</summary>
    public string Label { get; set; } = "Notification";

    /// <summary>Swipe gesture direction: right | left | up | down.</summary>
    public string SwipeDirection { get; set; } = "right";

    /// <summary>Distance in px a swipe must travel to dismiss.</summary>
    public double SwipeThreshold { get; set; } = 50;

    /// <summary>CSS selector for the portal mount container (null → document.body). Set by the Portal part.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the viewport mounted while empty (Portal keepMounted). Set by the Portal part.</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Gap in px inserted between stacked toasts when the stack is expanded.</summary>
    public double Gap { get; set; } = 16;

    /// <summary>True while the viewport is hovered/focused and the stack fans out. Set by the Viewport.</summary>
    public bool Expanded { get; private set; }

    /// <summary>Raised when a stacking input changes (expanded / a reported height) so Contents restyle.</summary>
    public event Action? Changed;

    // ---- announcer broker (verbatim from the previous context) --------------------------

    /// <summary>Raised by a high-priority toast to announce assertive text.</summary>
    public event Action<string>? AssertiveRequested;

    /// <summary>Raised by a low-priority toast to announce polite text.</summary>
    public event Action<string>? PoliteRequested;

    /// <summary>The viewport element — set by the viewport, focused by the F6 hotkey handler.</summary>
    public ElementReference ViewportElement { get; set; }
    public bool HasViewport { get; set; }

    /// <summary>Push <paramref name="text"/> into the polite or assertive announcer region.</summary>
    public void Announce(string text, bool assertive)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (assertive)
        {
            AssertiveRequested?.Invoke(text);
        }
        else
        {
            PoliteRequested?.Invoke(text);
        }
    }

    /// <summary>Set the fan-out state (viewport hover/focus) and notify dependents.</summary>
    public void SetExpanded(bool value)
    {
        if (Expanded == value)
        {
            return;
        }

        Expanded = value;
        Changed?.Invoke();
    }

    // ---- height aggregation for the stacking vars (§5) ----------------------------------

    private readonly Dictionary<string, double> _heights = new();

    /// <summary>A Root reports its measured height (via <c>GetRectAsync</c>) so offsets can be summed.</summary>
    public void ReportHeight(string id, double height)
    {
        if (_heights.TryGetValue(id, out var existing) && Math.Abs(existing - height) < 0.5)
        {
            return;
        }

        _heights[id] = height;
        Changed?.Invoke();
    }

    /// <summary>Drop a removed toast's height.</summary>
    public void ForgetHeight(string id)
    {
        if (_heights.Remove(id))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Height of the frontmost (stack-index 0) visible toast; sizes the collapsed stack.</summary>
    public double FrontmostHeight
    {
        get
        {
            var visible = Manager?.VisibleOrderedNewestFirst();
            return visible is { Count: > 0 } ? HeightOf(visible[0].Id) : 0;
        }
    }

    /// <summary>Cumulative vertical offset (Σ heights + gap) of the toasts in front of stack index <paramref name="index"/>.</summary>
    public double OffsetYFor(int index)
    {
        var visible = Manager?.VisibleOrderedNewestFirst();
        if (visible is null)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < index && i < visible.Count; i++)
        {
            sum += HeightOf(visible[i].Id) + Gap;
        }
        return sum;
    }

    private double HeightOf(string id) => _heights.TryGetValue(id, out var h) ? h : 0;
}
