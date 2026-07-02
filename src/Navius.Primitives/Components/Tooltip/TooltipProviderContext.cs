using System.Diagnostics;

namespace Navius.Primitives.Components.Tooltip;

/// <summary>
/// Shared, cross-tooltip state cascaded from <see cref="NaviusTooltipProvider"/>.
/// Supplies the default delay/skip-delay/hoverable settings and the shared
/// "recently open" timestamp that powers the skip-delay grace window: once any
/// tooltip in the provider has been open, moving to another within
/// <see cref="SkipDelayDuration"/> opens it instantly. Mirrors the spec's
/// <c>Tooltip.Provider</c>.
/// </summary>
public sealed class TooltipProviderContext
{
    /// <summary>Default hover-intent delay (ms) before a tooltip opens.</summary>
    public int DelayDuration { get; }

    /// <summary>
    /// Window (ms) after a tooltip closes during which the next tooltip opens
    /// instantly rather than waiting the full <see cref="DelayDuration"/>.
    /// </summary>
    public int SkipDelayDuration { get; }

    /// <summary>When true, the tooltip closes on trigger leave even over its content.</summary>
    public bool DisableHoverableContent { get; }

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long? _lastCloseMs;

    // The skip window is active immediately after a tooltip opens and remains
    // active for SkipDelayDuration after the most recent close.
    private bool _skipWindowOpen;

    public TooltipProviderContext(int delayDuration, int skipDelayDuration, bool disableHoverableContent)
    {
        DelayDuration = delayDuration;
        SkipDelayDuration = skipDelayDuration;
        DisableHoverableContent = disableHoverableContent;
    }

    /// <summary>
    /// True when a hover-open should skip the delay because another tooltip was
    /// shown recently (the spec's <c>isOpenDelayedRef</c> inverse).
    /// </summary>
    public bool ShouldSkipDelay()
    {
        if (_skipWindowOpen)
        {
            return true;
        }

        if (_lastCloseMs is { } closed)
        {
            return _stopwatch.ElapsedMilliseconds - closed < SkipDelayDuration;
        }

        return false;
    }

    /// <summary>Marks a tooltip as having opened — keeps the skip window active.</summary>
    public void NotifyOpen() => _skipWindowOpen = true;

    /// <summary>Marks a tooltip as having closed — starts the skip-delay countdown.</summary>
    public void NotifyClose()
    {
        _skipWindowOpen = false;
        _lastCloseMs = _stopwatch.ElapsedMilliseconds;
    }
}
