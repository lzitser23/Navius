using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Select;

/// <summary>
/// Cascaded by <c>NaviusSelectViewport</c> so the scroll buttons can observe the
/// viewport's scrollability and nudge it. The viewport reports its scroll metrics
/// from an <c>onscroll</c> handler; the buttons mount only when scrollable in
/// their direction (the spec's ScrollUpButton/ScrollDownButton behaviour).
/// </summary>
public sealed class SelectViewportContext
{
    /// <summary>The scrollable viewport element (for the buttons to scroll).</summary>
    public ElementReference Element { get; set; }
    public bool HasElement { get; set; }

    /// <summary>True when the viewport is scrolled down from the top (up-button shows).</summary>
    public bool CanScrollUp { get; private set; }

    /// <summary>True when there is more content below the fold (down-button shows).</summary>
    public bool CanScrollDown { get; private set; }

    public event Action? Changed;

    public void UpdateScrollState(double scrollTop, double scrollHeight, double clientHeight)
    {
        // 1px epsilon to avoid flicker from sub-pixel rounding.
        var up = scrollTop > 1;
        var down = scrollTop + clientHeight < scrollHeight - 1;
        if (up == CanScrollUp && down == CanScrollDown) return;
        CanScrollUp = up;
        CanScrollDown = down;
        Changed?.Invoke();
    }
}
