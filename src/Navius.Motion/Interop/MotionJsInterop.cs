using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Navius.Motion.Interop;

/// <summary>
/// The bridge to the motion engine's JavaScript module. Lazily imports
/// <c>navius-motion.js</c> on first use and exposes the WAAPI executor, the presence
/// observer and the gesture bindings to C#. One instance per consuming component is
/// fine; the browser caches the dynamic <c>import()</c>. Durations cross the boundary
/// in milliseconds (WAAPI native); C# owns the seconds-to-milliseconds conversion.
/// </summary>
public sealed class MotionJsInterop : IAsyncDisposable
{
    private const string ModulePath = "./_content/Navius.Motion/navius-motion.js";

    private readonly Lazy<Task<IJSObjectReference>> _module;

    public MotionJsInterop(IJSRuntime js)
    {
        _module = new Lazy<Task<IJSObjectReference>>(
            () => js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    }

    /// <summary>
    /// Run a one-off WAAPI animation on <paramref name="element"/>. When
    /// <see cref="MotionAnimateOptions.Spring"/> is set the handle carries the spring
    /// parameters and supports <see cref="MotionAnimation.RetargetAsync"/> with
    /// velocity carry-over.
    /// </summary>
    public async Task<MotionAnimation> AnimateElementAsync(
        ElementReference element, MotionFrame[] keyframes, MotionAnimateOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "animateElement", element, keyframes, options);
        return new MotionAnimation(handle);
    }

    /// <summary>
    /// Attach enter/exit WAAPI animations to an element that carries the Navius
    /// discrete state attributes. The engine observes the attributes and starts the
    /// animations itself, so the presence machine's deferred unmount sees them via
    /// <c>getAnimations()</c> with no further coupling.
    /// </summary>
    public async Task<PresenceMotion> CreatePresenceMotionAsync(
        ElementReference element, PresenceMotionOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createPresenceMotion", element, options);
        return new PresenceMotion(handle);
    }

    /// <summary>
    /// Bind a press or hover micro-interaction (<paramref name="kind"/> is
    /// <c>press</c> or <c>hover</c>) that animates the element JS-side.
    /// </summary>
    public async Task<MotionGesture> CreateGestureAsync(
        ElementReference element, string kind, GestureOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createGesture", element, kind, null, options);
        return new MotionGesture(handle);
    }

    /// <summary>
    /// Overload that additionally reports coarse gesture edges to
    /// <paramref name="callback"/> via <c>OnGestureStart</c> / <c>OnGestureEnd</c>.
    /// </summary>
    public async Task<MotionGesture> CreateGestureAsync<T>(
        ElementReference element, string kind, DotNetObjectReference<T> callback,
        GestureOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createGesture", element, kind, callback, options);
        return new MotionGesture(handle);
    }

    /// <summary>
    /// Bind a micro-interaction (shake, pulse, ...) to an element, built from a preset
    /// with <see cref="MotionPrograms.Micro"/>. The returned handle plays the animation
    /// on demand (<see cref="MotionMicro.PlayAsync"/>) and, for loops, stops it
    /// (<see cref="MotionMicro.StopAsync"/>); nothing runs until you play it.
    /// </summary>
    public async Task<MotionMicro> CreateMicroAsync(
        ElementReference element, MicroOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createMicro", element, options);
        return new MotionMicro(handle);
    }

    /// <summary>
    /// Turn <paramref name="parent"/> into an auto-animating list container: one
    /// MutationObserver FLIPs every add, remove and reorder of its direct element
    /// children. The returned handle toggles it on and off
    /// (<see cref="AutoAnimateMotion.EnableAsync"/> / <see cref="AutoAnimateMotion.DisableAsync"/>);
    /// dispose it to disconnect the observer and detach any in-flight exit clones. Build
    /// options (springs bake to a <c>linear()</c> easing) with
    /// <see cref="MotionPrograms.AutoAnimate"/>.
    /// </summary>
    public async Task<AutoAnimateMotion> CreateAutoAnimateAsync(
        ElementReference parent, AutoAnimateOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createAutoAnimate", parent, options);
        return new AutoAnimateMotion(handle);
    }

    /// <summary>
    /// Observe <paramref name="element"/> and set <c>data-in-view</c> on it while it
    /// intersects the viewport (IntersectionObserver v1), so the generated
    /// <c>.motion-in-view-*</c> classes reveal it on scroll with zero per-frame JS. With
    /// <see cref="InViewOptions.Stagger"/> the same intersection fans the attribute out
    /// to the direct children (their delay var set up front) for a staggered group.
    /// </summary>
    public async Task<InViewMotion> CreateInViewAsync(ElementReference element, InViewOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createInView", element, options, null);
        return new InViewMotion(handle);
    }

    /// <summary>
    /// Overload that additionally reports coarse <c>OnInView(bool)</c> edges to
    /// <paramref name="callback"/> (enter/leave), for driving C# state off the reveal.
    /// </summary>
    public async Task<InViewMotion> CreateInViewAsync<T>(
        ElementReference element, InViewOptions options, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createInView", element, options, callback);
        return new InViewMotion(handle);
    }

    /// <summary>
    /// Set <c>--navius-motion-delay</c> on each direct element child of
    /// <paramref name="container"/> from a stagger schedule, without any in-view coupling
    /// (for groups revealed by other means). The var composes with the enter / in-view
    /// classes that already read it.
    /// </summary>
    public async Task<StaggerMotion> CreateStaggerAsync(ElementReference container, StaggerOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createStagger", container, options);
        return new StaggerMotion(handle);
    }

    /// <summary>
    /// Attach a spring-animated selection marker that moves <paramref name="indicator"/>
    /// to the active element within <paramref name="container"/>. Position rides a
    /// compositor transform; size animates so a pill/underline tracks items of differing
    /// size. Call <see cref="SelectionIndicatorMotion.UpdateAsync"/> after the active
    /// element changes. Build <paramref name="options"/> with
    /// <see cref="MotionPrograms.SelectionIndicator"/>.
    /// </summary>
    public async Task<SelectionIndicatorMotion> CreateSelectionIndicatorAsync(
        ElementReference container, ElementReference indicator, SelectionIndicatorOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createSelectionIndicator", container, indicator, options);
        return new SelectionIndicatorMotion(handle);
    }

    /// <summary>
    /// Play a compiled <see cref="MotionProgram"/> (build one with
    /// <see cref="MotionSequence"/>). Selector targets resolve against the whole document.
    /// The returned handle drives the whole timeline (play/pause/seek/stop) and awaits its
    /// completion.
    /// </summary>
    public Task<MotionSequenceHandle> RunSequenceAsync(MotionProgram program)
        => RunSequenceCoreAsync(program, root: null);

    /// <summary>
    /// Overload that scopes selector targets to <paramref name="root"/> (its
    /// <c>querySelector</c>), so several sequences on one page never collide.
    /// </summary>
    public Task<MotionSequenceHandle> RunSequenceAsync(MotionProgram program, ElementReference root)
        => RunSequenceCoreAsync(program, root);

    private async Task<MotionSequenceHandle> RunSequenceCoreAsync(MotionProgram program, ElementReference? root)
    {
        var module = await _module.Value;
        var handle = root is ElementReference scope
            ? await module.InvokeAsync<IJSObjectReference>("runProgram", program, scope)
            : await module.InvokeAsync<IJSObjectReference>("runProgram", program, null);
        return new MotionSequenceHandle(handle, program.TotalMs);
    }

    /// <summary>
    /// Begin a same-document View Transition, resolving once the browser has captured the
    /// OLD snapshot (so the caller can then perform the Blazor navigation without racing
    /// the capture). Returns <c>false</c> under reduced motion or where the API is
    /// unsupported: the caller then navigates instantly. Pair with
    /// <see cref="FinishViewTransitionAsync"/> after the new page has rendered.
    /// </summary>
    public async Task<bool> StartViewTransitionAsync(string reduceMotion = "user")
    {
        var module = await _module.Value;
        return await module.InvokeAsync<bool>("startViewTransition", new { reduceMotion });
    }

    /// <summary>Resolve the pending transition so the browser captures the new state and cross-fades.</summary>
    public async Task FinishViewTransitionAsync()
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("finishViewTransition");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Await <paramref name="frames"/> animation frames (let a fresh render lay out before snapshotting).</summary>
    public async Task WaitFramesAsync(int frames)
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("nextFrames", frames);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_module.IsValueCreated)
        {
            return;
        }

        try
        {
            var module = await _module.Value;
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (Server prerender/teardown): nothing to clean up.
        }
    }
}

/// <summary>A live WAAPI animation. Dispose to cancel it and release the JS handle.</summary>
public sealed class MotionAnimation : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal MotionAnimation(IJSObjectReference handle) => _handle = handle;

    /// <summary>
    /// Redirect a spring-tagged animation to a new target mid-flight: the engine
    /// evaluates the current position and velocity from the closed-form solver and
    /// re-bakes from there, so the motion stays continuous.
    /// </summary>
    public async Task RetargetAsync(RetargetOptions options)
    {
        try
        {
            await _handle.InvokeVoidAsync("retarget", options);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Cancel the animation (the element snaps back to its natural style).</summary>
    public async Task CancelAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("cancel");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live presence observer. Dispose to disconnect it and cancel its animations.</summary>
public sealed class PresenceMotion : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal PresenceMotion(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live gesture binding. Dispose to remove its listeners.</summary>
public sealed class MotionGesture : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal MotionGesture(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A micro-interaction binding (shake, pulse, ...). <see cref="PlayAsync"/> starts (or
/// restarts) the animation; <see cref="StopAsync"/> cancels a running loop. Dispose to
/// stop it and release the JS handle.
/// </summary>
public sealed class MotionMicro : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal MotionMicro(IJSObjectReference handle) => _handle = handle;

    /// <summary>Play the animation once (one-shot presets) or start the loop (looping presets).</summary>
    public async Task PlayAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("play");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Stop the animation (the element returns to its natural style).</summary>
    public async Task StopAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("stop");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live auto-animate binding on a list container. <see cref="EnableAsync"/> /
/// <see cref="DisableAsync"/> toggle the FLIP-on-mutation behaviour without tearing it
/// down; while disabled, mutations apply instantly. Dispose to disconnect the observer,
/// stop the position pollers and detach any in-flight exit clones (no orphaned nodes).
/// </summary>
public sealed class AutoAnimateMotion : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal AutoAnimateMotion(IJSObjectReference handle) => _handle = handle;

    /// <summary>Resume animating mutations (re-baselines the current children first).</summary>
    public async Task EnableAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("enable");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Stop animating mutations (they apply instantly until re-enabled).</summary>
    public async Task DisableAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("disable");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live in-view observer. When created with <see cref="InViewOptions.Stagger"/>,
/// <see cref="RefreshAsync"/> re-reads the child list (call after the children change).
/// Dispose to disconnect it (the element keeps its last state).
/// </summary>
public sealed class InViewMotion : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal InViewMotion(IJSObjectReference handle) => _handle = handle;

    /// <summary>
    /// Recompute the per-child stagger delays after the group's children change. New
    /// children pick up the delay var, and if the group is already in view, data-in-view
    /// too. A no-op when the observer was created without a stagger.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("refresh");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A stagger binding on a container. <see cref="RefreshAsync"/> re-reads the child list.</summary>
public sealed class StaggerMotion : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal StaggerMotion(IJSObjectReference handle) => _handle = handle;

    /// <summary>Recompute and re-apply the per-child delay vars (call after the children change).</summary>
    public async Task RefreshAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("refresh");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live selection-indicator binding. <see cref="UpdateAsync"/> animates the marker to
/// the current active element; dispose to disconnect the resize observer.
/// </summary>
public sealed class SelectionIndicatorMotion : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal SelectionIndicatorMotion(IJSObjectReference handle) => _handle = handle;

    /// <summary>Animate the marker to the active element (call after the active element changes).</summary>
    public async Task UpdateAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("update");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A handle to a running <see cref="MotionProgram"/>: drive the whole timeline with
/// <see cref="PlayAsync"/> / <see cref="PauseAsync"/> / <see cref="SeekAsync"/> /
/// <see cref="StopAsync"/> and await its completion with <see cref="WaitForFinishAsync"/>.
/// <see cref="DurationMs"/> is the resolved total length. Dispose to cancel every segment.
/// </summary>
public sealed class MotionSequenceHandle : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal MotionSequenceHandle(IJSObjectReference handle, double totalMs)
    {
        _handle = handle;
        DurationMs = totalMs;
    }

    /// <summary>The resolved total timeline length in milliseconds.</summary>
    public double DurationMs { get; }

    /// <summary>Play (or resume) the whole timeline from the current position.</summary>
    public Task PlayAsync() => InvokeAsync("play");

    /// <summary>Pause every segment at the current position.</summary>
    public Task PauseAsync() => InvokeAsync("pause");

    /// <summary>Stop and rewind the whole timeline to the start.</summary>
    public Task StopAsync() => InvokeAsync("stop");

    /// <summary>Scrub the whole timeline to <paramref name="ms"/> (clamped to [0, duration]).</summary>
    public async Task SeekAsync(double ms)
    {
        try
        {
            await _handle.InvokeVoidAsync("seek", ms);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Await the timeline completing (resolves when every segment has finished playing).</summary>
    public async Task WaitForFinishAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("finished");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task InvokeAsync(string method)
    {
        try
        {
            await _handle.InvokeVoidAsync(method);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// Options for <see cref="MotionJsInterop.CreateInViewAsync(ElementReference, InViewOptions)"/>
/// (serialized to camelCase). <see cref="Amount"/> is the visibility threshold: 0 fires
/// as soon as any pixel is visible, 1 only when fully visible. <see cref="Margin"/> is a
/// CSS rootMargin. <see cref="Once"/> keeps <c>data-in-view</c> after the first entry.
/// A non-null <see cref="Stagger"/> also fans the reveal out to the direct children.
/// </summary>
public sealed record InViewOptions(
    double Amount = 0,
    string Margin = "0px",
    bool Once = false,
    StaggerOptions? Stagger = null);

/// <summary>
/// A stagger schedule (serialized to camelCase): <see cref="Step"/> is the per-child
/// step in milliseconds, <see cref="From"/> the anchor token (<c>first</c>, <c>last</c>
/// or <c>center</c>). Build from a <see cref="StaggerFrom"/> with <see cref="Of"/>.
/// </summary>
public sealed record StaggerOptions(double Step = 50, string From = "first")
{
    /// <summary>Build from a typed anchor.</summary>
    public static StaggerOptions Of(double step, StaggerFrom from) => new(step, from.ToToken());
}

/// <summary>
/// Options for <see cref="MotionJsInterop.CreateSelectionIndicatorAsync"/> (serialized to
/// camelCase). <see cref="ActiveSelector"/> finds the active item within the container;
/// <see cref="Axis"/> is <c>x</c>, <c>y</c> or <c>both</c>; <see cref="Easing"/> is the
/// baked spring the marker moves with. Build with
/// <see cref="MotionPrograms.SelectionIndicator"/>.
/// </summary>
public sealed record SelectionIndicatorOptions(
    string ActiveSelector = "[data-active]",
    string Axis = "both",
    double DurationMs = 200,
    string Easing = "ease",
    string ReduceMotion = "user");

/// <summary>
/// One WAAPI keyframe restricted to the compositor-friendly properties the presets
/// animate (serialized to camelCase; null members are dropped JS-side).
/// </summary>
public sealed record MotionFrame(double? Opacity = null, string? Transform = null);

/// <summary>
/// Options for <see cref="MotionJsInterop.AnimateElementAsync"/> (serialized to
/// camelCase). <see cref="ReduceMotion"/> is <c>user</c> (honour the OS setting),
/// <c>always</c> or <c>never</c>; under reduced motion transform keyframes are
/// stripped so only opacity animates. Set <see cref="Spring"/> together with
/// <see cref="Property"/> and <see cref="Template"/> (a CSS value with a <c>{}</c>
/// placeholder, e.g. <c>translateY({}px)</c>) to make the animation retargetable.
/// </summary>
public sealed record MotionAnimateOptions(
    double DurationMs,
    string Easing = "ease",
    double DelayMs = 0,
    string Fill = "both",
    string Composite = "replace",
    string ReduceMotion = "user",
    SpringParams? Spring = null,
    string? Property = null,
    string? Template = null);

/// <summary>
/// Serialized spring run parameters attached to retargetable animations: the exact
/// inputs of the closed-form evaluator duplicated in navius-motion.js.
/// <see cref="Velocity"/> is in value units per second.
/// </summary>
public sealed record SpringParams(
    double Stiffness,
    double Damping,
    double Mass,
    double Velocity,
    double Origin,
    double Target)
{
    /// <summary>Capture a solver run as serializable parameters.</summary>
    public static SpringParams From(SpringSolver solver) => new(
        solver.Spring.Stiffness, solver.Spring.Damping, solver.Spring.Mass,
        solver.Spring.InitialVelocity, solver.Origin, solver.Target);
}

/// <summary>One presence phase: keyframes plus the baked timing to play them with.</summary>
public sealed record MotionPhase(MotionFrame[] Keyframes, double DurationMs, string Easing);

/// <summary>
/// Options for <see cref="MotionJsInterop.CreatePresenceMotionAsync"/> (serialized to
/// camelCase). Enter plays when <c>data-starting-style</c> appears or is removed,
/// exit when <c>data-ending-style</c> appears. Build from a preset with
/// <see cref="MotionPrograms.Presence"/>.
/// </summary>
public sealed record PresenceMotionOptions(
    MotionPhase Enter,
    MotionPhase Exit,
    string ReduceMotion = "user");

/// <summary>
/// Options for <see cref="MotionJsInterop.CreateGestureAsync(ElementReference, string, GestureOptions)"/>
/// (serialized to camelCase). <see cref="PressScale"/> applies to <c>press</c>,
/// <see cref="HoverLift"/> (pixels) to <c>hover</c>; timing is the baked spring the
/// gesture animates with.
/// </summary>
public sealed record GestureOptions(
    double DurationMs,
    string Easing,
    double PressScale = 0.97,
    double HoverLift = 2,
    string ReduceMotion = "user");

/// <summary>
/// Options for <see cref="MotionAnimation.RetargetAsync"/> (serialized to camelCase).
/// <see cref="Target"/> is the new end value in the animation's value space; a null
/// <see cref="Spring"/> keeps the current spring constants.
/// </summary>
public sealed record RetargetOptions(
    double Target,
    SpringParams? Spring = null);

/// <summary>
/// Options for <see cref="MotionJsInterop.CreateMicroAsync"/> (serialized to camelCase).
/// <see cref="Keyframes"/> are WAAPI keyframe objects (each an offset plus its animated
/// properties); <see cref="Loop"/> plays them with infinite iterations. Build from a
/// preset with <see cref="MotionPrograms.Micro"/>. Under reduced motion every non-opacity
/// keyframe property is stripped (collapse to opacity-only), so a preset with nothing
/// animatable left (e.g. a transform-only, box-shadow, or background-position preset) does
/// not play.
/// </summary>
public sealed record MicroOptions(
    IReadOnlyList<IReadOnlyDictionary<string, object>> Keyframes,
    double DurationMs,
    string Easing = "ease-in-out",
    bool Loop = false,
    string ReduceMotion = "user");

/// <summary>
/// Options for <see cref="MotionJsInterop.CreateAutoAnimateAsync"/> (serialized to
/// camelCase). <see cref="DurationMs"/> and <see cref="Easing"/> time the FLIP remain
/// (adds run at 1.5x, removes at 1x, both eased per the reference). <see cref="Easing"/>
/// accepts a baked <c>linear()</c> spring string, which is how a spring animates the
/// FLIP. Defaults match @formkit/auto-animate (250ms, ease-in-out). Under reduced motion
/// transform animation is skipped (instant) while add/remove keep their opacity fade.
/// Build with <see cref="MotionPrograms.AutoAnimate"/>.
/// </summary>
public sealed record AutoAnimateOptions(
    double DurationMs = 250,
    string Easing = "ease-in-out",
    string ReduceMotion = "user");
