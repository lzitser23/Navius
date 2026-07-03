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
