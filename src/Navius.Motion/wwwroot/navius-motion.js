// Navius Motion engine: the executor half of the C#-compiled animation system.
//
// C# (Navius.Motion) owns the physics: springs are solved closed-form and baked
// into CSS linear() easings plus real durations before they cross the interop
// boundary once. This module only executes: thin WAAPI calls, presence
// observation of the Navius discrete state attributes, and gesture bindings.
// No rAF loops, no eval, CSP-safe. Durations arriving here are milliseconds.
//
// Deliberate exception: springPosition/springVelocity duplicate the C#
// SpringSolver closed-form evaluator (all three damping regimes) so retarget()
// can re-bake an interrupted animation from the live position AND velocity
// without an interop round trip. The two implementations must stay in
// agreement; tests/Navius.Motion.Tests runs this file under node and compares
// them (see JsAgreementTests).
//
// Validate after every edit (plain `node --check` misses ESM-only errors):
//   node --input-type=module --check < src/Navius.Motion/wwwroot/navius-motion.js

// --- Reduced motion ----------------------------------------------------------
// The guard collapses every non-opacity keyframe property to opacity-only (the
// tailwindcss-motion behaviour). Every factory takes a reduceMotion option:
// 'user' honours the OS setting, 'always' forces reduction, 'never' opts out.

function prefersReducedMotion() {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

function shouldReduceMotion(mode) {
  if (mode === 'always') return true;
  if (mode === 'never') return false;
  return prefersReducedMotion();
}

// Drop null/undefined properties (C# serializes optional keyframe members as
// null) so WAAPI never sees them.
function sanitizeFrame(frame) {
  const clean = {};
  for (const key of Object.keys(frame || {})) {
    const value = frame[key];
    if (value !== null && value !== undefined) clean[key] = value;
  }
  return clean;
}

function collapseToOpacity(frames) {
  // Keep only opacity plus the WAAPI control keys (the same set hasAnimatableProps
  // ignores); strip every other animatable property (transform, box-shadow,
  // background-position, ...) so any preset collapses to an opacity-only beat under
  // reduced motion, matching the CSS tier's `animation: none` for non-opacity presets.
  return frames.map((frame) => {
    const clean = {};
    for (const key of Object.keys(frame)) {
      if (key === 'opacity' || key === 'offset' || key === 'easing' || key === 'composite') {
        clean[key] = frame[key];
      }
    }
    return clean;
  });
}

function hasAnimatableProps(frames) {
  return frames.some((frame) =>
    Object.keys(frame).some((key) => key !== 'offset' && key !== 'easing' && key !== 'composite')
  );
}

// --- Closed-form spring evaluator --------------------------------------------
// Duplicate of C# SpringSolver (see the header note). p is the serialized run:
// { stiffness, damping, mass, velocity, origin, target }, t in SECONDS,
// velocity in value units per second. The sinh/cosh argument is capped at 300
// so the overdamped envelope stays finite.

export function springPosition(p, t) {
  const zeta = p.damping / (2 * Math.sqrt(p.stiffness * p.mass));
  const w0 = Math.sqrt(p.stiffness / p.mass);
  const delta = p.target - p.origin;
  const v0 = p.velocity || 0;
  const zw = zeta * w0;
  if (zeta < 1) {
    const wd = w0 * Math.sqrt(1 - zeta * zeta);
    const envelope = Math.exp(-zw * t);
    const a = (zw * delta - v0) / wd;
    return p.target - envelope * (a * Math.sin(wd * t) + delta * Math.cos(wd * t));
  }
  if (zeta === 1) {
    const envelope = Math.exp(-w0 * t);
    const b = w0 * delta - v0;
    return p.target - envelope * (delta + b * t);
  }
  const wd = w0 * Math.sqrt(zeta * zeta - 1);
  const envelope = Math.exp(-zw * t);
  const freqForT = Math.min(wd * t, 300);
  const c = zw * delta - v0;
  return p.target - (envelope * (c * Math.sinh(freqForT) + wd * delta * Math.cosh(freqForT))) / wd;
}

export function springVelocity(p, t) {
  const zeta = p.damping / (2 * Math.sqrt(p.stiffness * p.mass));
  const w0 = Math.sqrt(p.stiffness / p.mass);
  const delta = p.target - p.origin;
  const v0 = p.velocity || 0;
  const zw = zeta * w0;
  if (zeta < 1) {
    const wd = w0 * Math.sqrt(1 - zeta * zeta);
    const envelope = Math.exp(-zw * t);
    const a = (zw * delta - v0) / wd;
    return (
      envelope *
      ((zw * a + delta * wd) * Math.sin(wd * t) + (zw * delta - a * wd) * Math.cos(wd * t))
    );
  }
  if (zeta === 1) {
    const envelope = Math.exp(-w0 * t);
    const b = w0 * delta - v0;
    return envelope * (w0 * delta + w0 * b * t - b);
  }
  const wd = w0 * Math.sqrt(zeta * zeta - 1);
  const envelope = Math.exp(-zw * t);
  const freqForT = Math.min(wd * t, 300);
  const c = zw * delta - v0;
  return (
    envelope *
    (((zw * c - wd * wd * delta) / wd) * Math.sinh(freqForT) + (zw * delta - c) * Math.cosh(freqForT))
  );
}

// Motion's granular rest thresholds: value ranges under 5 (opacity, scale) rest
// much tighter than pixel ranges. Velocity threshold is units per second.
function restThresholds(absDelta) {
  return absDelta < 5 ? { speed: 0.01, delta: 0.005 } : { speed: 2, delta: 0.5 };
}

function settleDuration(p) {
  const thresholds = restThresholds(Math.abs(p.target - p.origin));
  for (let i = 0; ; i++) {
    const t = i * 0.01;
    if (t >= 20) return 20;
    if (
      Math.abs(springVelocity(p, t)) <= thresholds.speed &&
      Math.abs(p.target - springPosition(p, t)) <= thresholds.delta
    ) {
      return t;
    }
  }
}

// Bake a spring run to a linear() easing + duration, mirroring the C#
// LinearEasingBaker recipe: 10ms resolution (constant points per second),
// minimum 2 points, 4-decimal rounding, final point snapped to 1. Only the
// retarget path uses this JS bake; first-run bakes always come from C#.
export function bakeSpringEasing(p, resolutionSeconds = 0.01) {
  const delta = p.target - p.origin;
  if (delta === 0) throw new Error('Cannot bake a spring whose origin equals its target.');
  const duration = settleDuration(p);
  const numPoints = Math.max(Math.round(duration / resolutionSeconds), 2);
  const points = [];
  for (let i = 0; i < numPoints; i++) {
    const t = (duration * i) / (numPoints - 1);
    points.push(Math.round(((springPosition(p, t) - p.origin) / delta) * 10000) / 10000);
  }
  points[numPoints - 1] = 1;
  return {
    easing: `linear(${points.join(', ')})`,
    durationMs: Math.round(duration * 1000),
    points,
  };
}

// --- animateElement -----------------------------------------------------------
// Thin WAAPI wrapper. options: { durationMs, easing, delayMs, fill, composite,
// reduceMotion, spring, property, template }. When `spring` (a serialized run,
// see the evaluator above) is present together with `property` ('opacity' or
// 'transform') and `template` (a CSS value with a {} placeholder, e.g.
// 'translateY({}px)'), the returned handle supports retarget() with velocity
// carry-over. Returns a handle; C# calls cancel()/retarget()/destroy().

function formatValue(template, value) {
  return template.replace('{}', String(value));
}

export function animateElement(element, keyframes, options) {
  const opts = Object.assign(
    {
      durationMs: 300,
      easing: 'ease',
      delayMs: 0,
      fill: 'both',
      composite: 'replace',
      reduceMotion: 'user',
      spring: null,
      property: 'opacity',
      template: '{}',
    },
    options || {}
  );

  let frames = (keyframes || []).map(sanitizeFrame);
  const reduce = shouldReduceMotion(opts.reduceMotion);
  if (reduce) frames = collapseToOpacity(frames);

  let animation = null;
  if (hasAnimatableProps(frames) && !(reduce && opts.property === 'transform' && opts.spring)) {
    animation = element.animate(frames, {
      duration: opts.durationMs,
      easing: opts.easing,
      delay: opts.delayMs,
      fill: opts.fill,
      composite: opts.composite,
    });
    animation.finished.catch(() => {});
  } else if (reduce && opts.spring) {
    // Reduced motion on a spring-tagged transform: jump straight to the target.
    element.style.setProperty(opts.property, formatValue(opts.template, opts.spring.target));
  }

  const handle = {
    element,
    animation,
    program: opts.spring
      ? { spring: opts.spring, property: opts.property, template: opts.template, delayMs: opts.delayMs, reduceMotion: opts.reduceMotion }
      : null,
    cancel() {
      if (handle.animation) handle.animation.cancel();
    },
    destroy() {
      handle.cancel();
    },
    retarget(next) {
      return retarget(handle, next);
    },
  };
  return handle;
}

// --- retarget -----------------------------------------------------------------
// The interruption path: evaluate the live position and velocity from the
// handle's serialized spring at the animation's elapsed time, then re-bake a
// fresh linear() run from there to the new target. Because the original easing
// was sampled from the same closed form, the evaluated position matches what
// is on screen to within the bake's quantization (10ms sampling, 4 decimals).
// next: { target, spring? } where spring optionally swaps the constants
// (stiffness/damping/mass); position and velocity always carry over.

export function retarget(handle, next) {
  if (!handle || !handle.program) {
    throw new Error('retarget() requires an animation created with spring options.');
  }
  const program = handle.program;
  const previous = program.spring;

  let t = 0;
  if (handle.animation && handle.animation.currentTime !== null) {
    t = Math.max(0, Number(handle.animation.currentTime) - (program.delayMs || 0)) / 1000;
  }
  const position = springPosition(previous, t);
  const velocity = springVelocity(previous, t);

  const spring = Object.assign({}, previous, (next && next.spring) || {}, {
    velocity,
    origin: position,
    target: next.target,
  });

  if (handle.animation) handle.animation.cancel();
  program.spring = spring;
  program.delayMs = 0;

  if (
    Math.abs(spring.target - spring.origin) < 1e-6 ||
    (shouldReduceMotion(program.reduceMotion) && program.property === 'transform')
  ) {
    // Nothing to travel (or reduced motion on a transform): land instantly.
    handle.element.style.setProperty(program.property, formatValue(program.template, spring.target));
    handle.animation = null;
    return handle;
  }

  const baked = bakeSpringEasing(spring);
  handle.animation = handle.element.animate(
    [
      { [program.property]: formatValue(program.template, spring.origin) },
      { [program.property]: formatValue(program.template, spring.target) },
    ],
    { duration: baked.durationMs, easing: baked.easing, fill: 'both' }
  );
  handle.animation.finished.catch(() => {});
  return handle;
}

// --- Presence motion ----------------------------------------------------------
// Watches the Navius/Base UI discrete state attributes on one element and runs
// enter/exit WAAPI animations for them. Cooperation with the existing presence
// machine needs no coupling: the machine's waitForAnimations defers unmount by
// awaiting element.getAnimations() one frame after data-ending-style renders,
// and MutationObserver callbacks are microtasks, so the exit animation started
// here is always visible to it in time.
//
// Triggers:
//   data-starting-style added   -> enter (keep-mounted flip; starts pre-paint)
//   data-starting-style removed -> enter (standard machine; skipped when the
//                                  added branch or the initial kick already
//                                  started this cycle's enter)
//   data-ending-style added     -> exit (from a live computed snapshot, so an
//                                  interrupted enter hands over continuously)
//   data-closed added with no data-ending-style -> unanimated close: cancel
//
// options: { enter: { keyframes, durationMs, easing }, exit: {...},
//   reduceMotion }. Enter keyframes are [hidden, visible]; exit keyframes are
//   the hidden frame only (the live snapshot supplies the from frame). Enter
//   fills backwards (the element's natural style IS the visible state), exit
//   fills both so keep-mounted elements hold the hidden state while closed.

export function createPresenceMotion(element, options) {
  const opts = Object.assign({ enter: null, exit: null, reduceMotion: 'user' }, options || {});
  let current = null;
  let phase = null;

  function snapshot() {
    const computed = getComputedStyle(element);
    return { opacity: computed.opacity, transform: computed.transform };
  }

  function play(kind) {
    const program = kind === 'enter' ? opts.enter : opts.exit;
    if (!program) return;

    const interrupted = current !== null && current.playState === 'running';
    let frames = (program.keyframes || []).map(sanitizeFrame);
    if (kind === 'enter') {
      if (interrupted) frames = [snapshot(), frames[frames.length - 1]];
    } else {
      frames = [snapshot(), ...frames];
    }
    if (shouldReduceMotion(opts.reduceMotion)) frames = collapseToOpacity(frames);

    if (current) current.cancel();
    current = null;
    phase = null;
    if (!hasAnimatableProps(frames)) return;

    const animation = element.animate(frames, {
      duration: program.durationMs,
      easing: program.easing,
      fill: kind === 'enter' ? 'backwards' : 'both',
    });
    animation.finished.catch(() => {});
    current = animation;
    phase = kind;
  }

  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      const attr = mutation.attributeName;
      const has = element.hasAttribute(attr);
      if (attr === 'data-ending-style' && has) {
        play('exit');
      } else if (attr === 'data-starting-style') {
        if (has) {
          play('enter');
        } else if (phase !== 'enter' || current === null || current.playState !== 'running') {
          play('enter');
        }
      } else if (attr === 'data-closed' && has && !element.hasAttribute('data-ending-style')) {
        if (current) current.cancel();
        current = null;
        phase = null;
      }
    }
  });
  observer.observe(element, {
    attributes: true,
    attributeFilter: ['data-open', 'data-closed', 'data-starting-style', 'data-ending-style'],
  });

  // Initial kick: when attached during the starting-style frame (the standard
  // engage timing), hold the hidden state now instead of waiting for the
  // attribute to flip, so the first paint cannot flash the visible state.
  if (element.hasAttribute('data-starting-style')) {
    play('enter');
  }

  return {
    destroy() {
      observer.disconnect();
      if (current) current.cancel();
      current = null;
      phase = null;
    },
  };
}

// --- Gestures -----------------------------------------------------------------
// Coarse press/hover micro-interactions, animated JS-side; only start/end
// callbacks cross to C# (when a dotNetRef is supplied): OnGestureStart(kind),
// OnGestureEnd(success). Press mirrors Motion's semantics: primary button only,
// keyboard Enter parity, blur cancels. Hover filters emulated touch hovers.
// Gestures assume the element carries no author transform (same constraint as
// the CSS tier's :active/:hover rules). Under reduced motion the animations are
// skipped but callbacks still fire (behaviour is not decoration).

export function createGesture(element, kind, dotNetRef, options) {
  const opts = Object.assign(
    { durationMs: 200, easing: 'ease', pressScale: 0.97, hoverLift: 2, reduceMotion: 'user' },
    options || {}
  );

  const notify = (method, payload) => {
    if (dotNetRef) {
      Promise.resolve(dotNetRef.invokeMethodAsync(method, payload)).catch(() => {});
    }
  };

  // One active transform tween at a time. Each transition snapshots the live
  // computed transform as an explicit from frame, then cancels the previous
  // tween, so press/hover changes flow smoothly from wherever the element
  // currently sits. fill:'forwards' holds the target: the pressed/hovered pose
  // while engaged, the neutral pose after release. A single-keyframe animation
  // would bind its implicit from frame to the underlying value, which snaps the
  // moment the previous tween is cancelled, so the from frame is captured
  // explicitly instead.
  let current = null;

  function run(toFrame) {
    if (shouldReduceMotion(opts.reduceMotion)) return;
    const from = getComputedStyle(element).transform;
    if (current) current.cancel();
    const animation = element.animate([{ transform: from }, toFrame], {
      duration: opts.durationMs,
      easing: opts.easing,
      fill: 'forwards',
    });
    animation.finished.catch(() => {});
    current = animation;
  }

  function cancelAll() {
    if (current) current.cancel();
    current = null;
  }

  let pressed = false;
  let pressedByKey = false;

  function pressStart() {
    run({ transform: `scale(${opts.pressScale})` });
    notify('OnGestureStart', 'press');
  }

  function pressEnd(success) {
    run({ transform: 'none' });
    notify('OnGestureEnd', success);
  }

  function onPointerDown(e) {
    if (e.button !== 0 || !e.isPrimary || pressed) return;
    pressed = true;
    pressStart();
    window.addEventListener('pointerup', onPointerUp);
    window.addEventListener('pointercancel', onPointerCancel);
  }

  function onPointerUp(e) {
    if (!pressed) return;
    pressed = false;
    removeWindowListeners();
    pressEnd(element.contains(e.target));
  }

  function onPointerCancel() {
    if (!pressed) return;
    pressed = false;
    removeWindowListeners();
    pressEnd(false);
  }

  function removeWindowListeners() {
    window.removeEventListener('pointerup', onPointerUp);
    window.removeEventListener('pointercancel', onPointerCancel);
  }

  function onKeyDown(e) {
    if (e.key !== 'Enter' || e.repeat || pressedByKey) return;
    pressedByKey = true;
    pressStart();
  }

  function onKeyUp(e) {
    if (e.key !== 'Enter' || !pressedByKey) return;
    pressedByKey = false;
    pressEnd(true);
  }

  function onBlur() {
    if (!pressedByKey) return;
    pressedByKey = false;
    pressEnd(false);
  }

  function onPointerEnter(e) {
    if (e.pointerType === 'touch') return;
    run({ transform: `translateY(${-opts.hoverLift}px)` });
    notify('OnGestureStart', 'hover');
  }

  function onPointerLeave(e) {
    if (e.pointerType === 'touch') return;
    run({ transform: 'none' });
    notify('OnGestureEnd', true);
  }

  if (kind === 'press') {
    element.addEventListener('pointerdown', onPointerDown);
    element.addEventListener('keydown', onKeyDown);
    element.addEventListener('keyup', onKeyUp);
    element.addEventListener('blur', onBlur);
  } else if (kind === 'hover') {
    element.addEventListener('pointerenter', onPointerEnter);
    element.addEventListener('pointerleave', onPointerLeave);
  } else {
    throw new Error(`Unknown gesture kind: ${kind}`);
  }

  return {
    destroy() {
      if (kind === 'press') {
        element.removeEventListener('pointerdown', onPointerDown);
        element.removeEventListener('keydown', onKeyDown);
        element.removeEventListener('keyup', onKeyUp);
        element.removeEventListener('blur', onBlur);
        removeWindowListeners();
      } else {
        element.removeEventListener('pointerenter', onPointerEnter);
        element.removeEventListener('pointerleave', onPointerLeave);
      }
      cancelAll();
    },
  };
}

// --- Micro animations ---------------------------------------------------------
// Attention/ambient keyframe presets (shake, pulse, ...) played JS-side so they can
// be triggered on demand and, for loops, started/stopped programmatically. Keyframes
// and timing are authored once in C# (Navius.Motion.MicroPresets) and cross the
// boundary as a serialized program, exactly like the presence/gesture tiers. options:
// { keyframes: [{ offset, transform, opacity, ... }], durationMs, easing, loop,
// reduceMotion }. Under reduced motion every non-opacity keyframe property is stripped
// (collapse to opacity-only) via the shared guard, so a preset left with nothing
// animatable (e.g. shake, or a box-shadow/background-position sweep) simply does not
// play. Returns a handle; C# calls play()/stop()/destroy().

export function createMicro(element, options) {
  const opts = Object.assign(
    { keyframes: [], durationMs: 400, easing: 'ease-in-out', loop: false, reduceMotion: 'user' },
    options || {}
  );

  let current = null;

  function stop() {
    if (current) {
      current.cancel();
      current = null;
    }
  }

  function play() {
    stop();
    let frames = (opts.keyframes || []).map(sanitizeFrame);
    if (shouldReduceMotion(opts.reduceMotion)) frames = collapseToOpacity(frames);
    if (!hasAnimatableProps(frames)) return;
    const animation = element.animate(frames, {
      duration: opts.durationMs,
      easing: opts.easing,
      iterations: opts.loop ? Infinity : 1,
      fill: 'none',
    });
    animation.finished.catch(() => {});
    current = animation;
  }

  return {
    play,
    stop,
    destroy() {
      stop();
    },
  };
}
