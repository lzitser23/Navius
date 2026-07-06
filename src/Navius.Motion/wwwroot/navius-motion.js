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

// --- Auto-animate (FLIP on mutation) -----------------------------------------
// One MutationObserver on a list container: every add / remove / reorder of its direct
// element children animates. A faithful port of @formkit/auto-animate's core algorithm
// (childList diff -> FLIP remain, scale/opacity add, absolute-positioned exit), adapted
// to our conventions:
//   - Per-INSTANCE caches (the reference keys module-global WeakMaps by element; scoping
//     them to the factory closure makes destroy() trivial and matches our idiom).
//   - Scoped to ONE container's direct element children (the component's contract), so
//     the reference's per-element target-parent (__aa_tgt) collapses to `parent`.
//   - Removed nodes' siblings are stashed from the MutationRecord (previousSibling /
//     nextSibling capture the pre-removal position exactly) so the node can be
//     re-inserted for its exit animation.
//   - OUR differentiator: `easing` accepts a baked linear() spring (springs on the FLIP
//     remain), the same C#-baked curves as the rest of the engine.
//   - Reduced motion deviates from the reference (which disables entirely): mutations
//     apply instantly with zero transform animation, but add/remove keep their opacity
//     fade via the shared collapseToOpacity guard (consistent with our other tiers).
// SKIPPED: the plugin system (raw KeyframeEffect returns) and the __aa_new revival flag
// (a framework node-reuse case Blazor's keyed diff does not produce; exit clones are
// always detached, so no orphans regardless).
//
// options: { durationMs (250), easing ('ease-in-out', or a baked linear()), reduceMotion }.
// Returns { enable, disable, destroy }.

export function createAutoAnimate(parent, options) {
  const opts = Object.assign(
    { durationMs: 250, easing: 'ease-in-out', reduceMotion: 'user' },
    options || {}
  );

  const coords = new WeakMap();        // element -> {top,left,width,height}: the FLIP "First"
  const animations = new WeakMap();    // element -> in-flight Animation (cancelled on retrigger)
  const siblings = new WeakMap();      // element -> [prev, next] stashed before removal
  const intersections = new WeakMap(); // element -> IntersectionObserver (position freshness)
  const childResizes = new WeakMap();  // element -> ResizeObserver (position freshness)
  const debounces = new WeakMap();     // element -> child-resize debounce timeout
  const pendingDelete = new WeakSet(); // elements mid-exit: the engine owns their lifecycle
  const engineTouched = new WeakSet(); // nodes the engine itself inserts/detaches (ignored)

  let tracked = [];                    // current in-flow children we FLIP
  let isEnabled = true;
  let pollId = null;
  let rootResize = null;
  let rootResizeDebounce = null;

  const hasIO = typeof IntersectionObserver === 'function';
  const hasRO = typeof ResizeObserver === 'function';
  const reduce = () => shouldReduceMotion(opts.reduceMotion);

  // A child is safe to (re)measure only when nothing is transforming it: an in-flight
  // FLIP would poison getBoundingClientRect with the animation's translate.
  function idle(el) {
    return el.isConnected && !pendingDelete.has(el) && !animations.has(el);
  }

  function measure(el) {
    const rect = el.getBoundingClientRect();
    return { top: rect.top, left: rect.left, width: rect.width, height: rect.height };
  }

  // Direct element children currently in normal flow (parent.children is element-only,
  // so Blazor's comment-node markers are filtered for free). Exit clones are excluded.
  function liveChildren() {
    const out = [];
    for (const n of parent.children) if (!pendingDelete.has(n)) out.push(n);
    return out;
  }

  function cancelAnim(el) {
    const a = animations.get(el);
    if (a) {
      animations.delete(el);
      a.cancel();
    }
  }

  // Play a FLIP/add tween. fill:'none' so the element reverts to its natural style at
  // rest (the last keyframe already equals that style, so there is no snap).
  function play(el, frames, timing) {
    const anim = el.animate(frames.map(sanitizeFrame), Object.assign({ fill: 'none' }, timing));
    anim.finished.catch(() => {});
    animations.set(el, anim);
    anim.finished.then(
      () => {
        if (animations.get(el) === anim) {
          animations.delete(el);
          refreshCoords(el); // re-baseline First once the element is at rest again
        }
      },
      () => {}
    );
    return anim;
  }

  // getTransitionSizes: content-box compensation so width/height keyframes animate the
  // box the browser will settle at. Inert for border-box children (the common case).
  function transitionSizes(el, from, to) {
    let wFrom = from.width, wTo = to.width, hFrom = from.height, hTo = to.height;
    const style = getComputedStyle(el);
    if (style.boxSizing === 'content-box') {
      const num = (v) => parseFloat(v) || 0;
      const padX = num(style.paddingLeft) + num(style.paddingRight) + num(style.borderLeftWidth) + num(style.borderRightWidth);
      const padY = num(style.paddingTop) + num(style.paddingBottom) + num(style.borderTopWidth) + num(style.borderBottomWidth);
      wFrom -= padX; wTo -= padX; hFrom -= padY; hTo -= padY;
    }
    return [Math.round(wFrom), Math.round(wTo), Math.round(hFrom), Math.round(hTo)];
  }

  // FLIP a surviving child from its cached First to its fresh Last.
  function remain(el) {
    const oldCoords = coords.get(el);
    cancelAnim(el);              // revert any in-flight FLIP (fill:none) before measuring
    const newCoords = measure(el);
    coords.set(el, newCoords);   // this Last becomes the next mutation's First
    if (!oldCoords) return;
    const dx = oldCoords.left - newCoords.left;
    const dy = oldCoords.top - newCoords.top;
    const [wFrom, wTo, hFrom, hTo] = transitionSizes(el, oldCoords, newCoords);
    // Anchored on an axis (delta 0, e.g. a bottom/right-anchored list) needs no offset
    // correction on it; a 0 translate on that axis does exactly that.
    if (dx === 0 && dy === 0 && wFrom === wTo && hFrom === hTo) return;
    const start = { transform: `translate(${dx}px, ${dy}px)` };
    const end = { transform: 'translate(0px, 0px)' };
    if (wFrom !== wTo) { start.width = `${wFrom}px`; end.width = `${wTo}px`; }
    if (hFrom !== hTo) { start.height = `${hFrom}px`; end.height = `${hTo}px`; }
    let frames = [start, end];
    if (reduce()) frames = collapseToOpacity(frames); // strips transform/size -> instant
    if (!hasAnimatableProps(frames)) return;          // reduced motion: applied instantly
    play(el, frames, { duration: opts.durationMs, easing: opts.easing });
  }

  // Enter: opacity + scale(.98) -> 1 at 1.5x the base duration, ease-in (reference).
  function add(el) {
    coords.set(el, measure(el));
    observeChild(el);
    observePosition(el);
    let frames = [
      { transform: 'scale(0.98)', opacity: 0 },
      { transform: 'scale(0.98)', opacity: 0, offset: 0.5 },
      { transform: 'scale(1)', opacity: 1 },
    ];
    if (reduce()) frames = collapseToOpacity(frames); // keeps the opacity fade
    if (!hasAnimatableProps(frames)) return;
    play(el, frames, { duration: opts.durationMs * 1.5, easing: 'ease-in' });
  }

  // Re-insert a removed node where it was, so it can animate out in place.
  function reinsert(el) {
    engineTouched.add(el); // the resulting childList record is the engine's, not the app's
    const pair = siblings.get(el);
    const prev = pair && pair[0];
    const next = pair && pair[1];
    if (next && next.parentNode === parent) parent.insertBefore(el, next);
    else if (prev && prev.parentNode === parent) prev.after(el);
    else parent.appendChild(el);
  }

  // Pin an exiting node at its old spot, out of flow, relative to the container.
  function positionAbsolute(el, old) {
    const parentRect = parent.getBoundingClientRect();
    const style = getComputedStyle(parent);
    const borderTop = parseFloat(style.borderTopWidth) || 0;
    const borderLeft = parseFloat(style.borderLeftWidth) || 0;
    Object.assign(el.style, {
      position: 'absolute',
      top: `${old.top - parentRect.top - borderTop + parent.scrollTop}px`,
      left: `${old.left - parentRect.left - borderLeft + parent.scrollLeft}px`,
      width: `${old.width}px`,
      height: `${old.height}px`,
      margin: '0',
      boxSizing: 'border-box',
      pointerEvents: 'none',
      zIndex: '1',
    });
  }

  // Counter-scroll so removing an item near the bottom of a SCROLLABLE container does not
  // jump the remaining content. Conservative and inert for non-scrolling containers (our
  // demo lists); not exercised by the suite.
  //
  // GUARDRAIL: this writes parent.scrollTop directly. Do NOT enable AutoAnimate on a
  // container whose scrollTop is owned by another engine (e.g. a MessageScroller / chat
  // viewport that drives its own stick-to-bottom via scrollTop / scrollIntoView). Two
  // writers on one element fight and produce scroll jitter. AutoAnimate must own the
  // scroll position of any scrollable container it animates, or that container must be
  // non-scrolling. In Navius these never co-locate (AutoAnimate ships a non-scrolling
  // list; MessageScroller lives in a separate assembly), which is why the hazard is
  // avoided by construction rather than guarded at runtime.
  function adjustScroll(height) {
    if (parent.scrollHeight <= parent.clientHeight) return;
    const distanceToBottom = parent.scrollHeight - parent.scrollTop - parent.clientHeight;
    if (distanceToBottom < 1) parent.scrollTop = Math.max(0, parent.scrollTop - height);
  }

  function detach(el) {
    if (!pendingDelete.has(el)) return; // already gone
    pendingDelete.delete(el);
    animations.delete(el);
    siblings.delete(el);
    teardownObservers(el);
    el.remove(); // engineTouched still holds el, so this removal record is ignored
  }

  // Exit: re-insert, pin absolute at the old coords, scale/opacity out, then detach. The
  // finish handler runs on cancel too, so a cancelled exit still detaches -> no orphans.
  function remove(el) {
    const old = coords.get(el);
    if (!old) return;
    pendingDelete.add(el);
    reinsert(el);
    positionAbsolute(el, old);
    coords.delete(el);
    let frames = [
      { transform: 'scale(1)', opacity: 1 },
      { transform: 'scale(0.98)', opacity: 0 },
    ];
    if (reduce()) frames = collapseToOpacity(frames); // keeps the opacity fade
    if (!hasAnimatableProps(frames)) { detach(el); return; }
    adjustScroll(old.height);
    const anim = el.animate(frames.map(sanitizeFrame), {
      duration: opts.durationMs,
      easing: 'ease-out',
      fill: 'both',
    });
    anim.finished.catch(() => {});
    animations.set(el, anim);
    anim.finished.then(() => detach(el), () => detach(el));
  }

  // --- Position freshness (coords stay accurate between mutations) ---
  // Only ever refreshes coords for idle children (never mid-animation), so it cannot
  // poison a running FLIP; a no-op while animating, it re-arms the observers at rest.

  function refreshCoords(el) {
    if (idle(el)) coords.set(el, measure(el));
    if (el.isConnected && !pendingDelete.has(el)) observePosition(el);
  }

  // Per-element IntersectionObserver whose rootMargin is the negative of the element's
  // own viewport box: it fires the moment the element moves (scroll / layout shift).
  function observePosition(el) {
    const existing = intersections.get(el);
    if (existing) existing.disconnect();
    if (!hasIO || typeof window === 'undefined') return;
    const rect = el.getBoundingClientRect();
    const w = window.innerWidth, h = window.innerHeight;
    const margin =
      `${-Math.floor(rect.top)}px ${-Math.floor(w - rect.right)}px ` +
      `${-Math.floor(h - rect.bottom)}px ${-Math.floor(rect.left)}px`;
    let first = true;
    const io = new IntersectionObserver(
      () => {
        if (first) { first = false; return; } // the synchronous initial fire
        if (!idle(el)) return;                 // ignore transform-induced moves mid-FLIP
        refreshCoords(el);
      },
      { root: null, threshold: 1, rootMargin: margin }
    );
    io.observe(el);
    intersections.set(el, io);
  }

  // Per-child ResizeObserver, debounced by the animation duration (reference cadence).
  function observeChild(el) {
    if (!hasRO || childResizes.has(el)) return;
    const ro = new ResizeObserver(() => {
      clearTimeout(debounces.get(el));
      debounces.set(el, setTimeout(() => refreshCoords(el), opts.durationMs));
    });
    ro.observe(el);
    childResizes.set(el, ro);
  }

  function teardownObservers(el) {
    const io = intersections.get(el);
    if (io) { io.disconnect(); intersections.delete(el); }
    const ro = childResizes.get(el);
    if (ro) { ro.disconnect(); childResizes.delete(el); }
    clearTimeout(debounces.get(el));
    debounces.delete(el);
  }

  // --- Mutation handling ---

  const observer = new MutationObserver((records) => {
    if (!isEnabled) return;
    let touched = 0;
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (node.nodeType === 1 && !engineTouched.has(node)) touched++;
      }
      for (const node of record.removedNodes) {
        if (node.nodeType !== 1 || engineTouched.has(node)) continue;
        // The record preserves the pre-removal siblings: the "stash before removal".
        siblings.set(node, [record.previousSibling, record.nextSibling]);
        touched++;
      }
    }
    if (touched === 0) return; // comment/text-only, or engine-driven: nothing to do
    reconcile();
  });

  function reconcile() {
    const live = liveChildren();
    const liveSet = new Set(live);
    const removed = tracked.filter((el) => !liveSet.has(el) && coords.has(el) && !pendingDelete.has(el));
    const added = live.filter((el) => !coords.has(el));
    const survivors = live.filter((el) => coords.has(el));

    for (const el of survivors) remain(el); // FLIP everything that shifted, not just moved nodes
    for (const el of added) add(el);
    for (const el of removed) remove(el);

    tracked = live;
  }

  // --- Lifecycle ---

  function syncTracked() {
    const live = liveChildren();
    for (const el of live) {
      if (!coords.has(el)) {
        coords.set(el, measure(el)); // baseline: existing children do not animate on init
        observeChild(el);
        observePosition(el);
      }
    }
    tracked = live;
  }

  function startPoll() {
    stopPoll();
    // A slow, jittered poll catches scrolls the IntersectionObserver missed (reference:
    // ~2000ms staggered). One instance interval refreshes all idle children.
    const period = 2000 + Math.floor(Math.random() * 500);
    pollId = setInterval(() => {
      for (const el of tracked) refreshCoords(el);
    }, period);
  }

  function stopPoll() {
    if (pollId !== null) { clearInterval(pollId); pollId = null; }
  }

  function detachExitClones() {
    for (const node of Array.from(parent.children)) {
      if (pendingDelete.has(node)) cancelAnim(node); // cancel -> finish handler detaches
    }
  }

  function enable() {
    if (isEnabled) return;
    isEnabled = true;
    syncTracked();
    startPoll();
  }

  function disable() {
    if (!isEnabled) return;
    isEnabled = false;
    for (const el of tracked) cancelAnim(el);
    detachExitClones();
    stopPoll();
  }

  function destroy() {
    isEnabled = false;
    observer.disconnect();
    stopPoll();
    if (rootResize) { rootResize.disconnect(); rootResize = null; }
    clearTimeout(rootResizeDebounce);
    for (const el of tracked) { cancelAnim(el); teardownObservers(el); }
    detachExitClones();
  }

  // The reference forces position:relative so exit clones anchor to the container.
  if (getComputedStyle(parent).position === 'static') parent.style.position = 'relative';
  syncTracked();
  observer.observe(parent, { childList: true });
  startPoll();
  if (hasRO) {
    rootResize = new ResizeObserver(() => {
      clearTimeout(rootResizeDebounce);
      rootResizeDebounce = setTimeout(() => {
        for (const el of tracked) refreshCoords(el);
      }, 100);
    });
    rootResize.observe(parent);
  }

  return { enable, disable, destroy };
}

// --- Height animation (NaviusHeightAnimation) --------------------------------
// Measure a content element's natural height and WAAPI-tween the clipped OUTER element's
// height to match, so a collapse/expand or a content-size change resizes smoothly instead
// of jumping. Two elements: `element` is the overflow:hidden box whose height we animate;
// `content` is the inner wrapper we measure (its border box is the natural height, immune to
// the outer clip). A ResizeObserver on `content` catches content-driven changes.
//
// This deliberately duplicates nothing from navius-interop.js (createSizeObserver): the
// motion package carries zero references to the brain, so it ships its own ~1-screen tween.
//
// options: { durationMs, easing, reduceMotion, expanded }. `expanded` is null (always track
// content size, never collapses), true (open, tracks) or false (collapsed to height 0). The
// tween sets the resting inline height up front and animates the delta with fill:'none', so
// at rest the element sits at its own natural px (tracking stays live) or 0. Under reduced
// motion the tween is skipped and the end state is applied instantly. Returns
// { setExpanded, remeasure, destroy }.

export function createHeightAnimation(element, content, options) {
  const opts = Object.assign(
    { durationMs: 300, easing: 'ease', reduceMotion: 'user', expanded: null },
    options || {}
  );

  let expanded = opts.expanded === true || opts.expanded === false ? opts.expanded : null;
  let current = null;

  const tracking = () => expanded !== false;
  const natural = () => content.getBoundingClientRect().height;
  const currentHeight = () => element.getBoundingClientRect().height;

  // Animate the outer box from `from` to `to`, settling at `restHeight` (the inline value it
  // holds at rest: its own natural px while open, '0px' while collapsed). fill:'none' plus a
  // pre-set resting height means the element reverts to exactly that height when the tween
  // ends, with no snap.
  function tween(from, to, restHeight) {
    if (current) {
      current.cancel();
      current = null;
    }
    element.style.height = restHeight;

    if (
      Math.abs(from - to) < 0.5 ||
      shouldReduceMotion(opts.reduceMotion) ||
      typeof element.animate !== 'function'
    ) {
      return; // snapped via the inline height
    }

    void element.offsetHeight; // commit the from-height before the tween
    const animation = element.animate(
      [{ height: `${from}px` }, { height: `${to}px` }],
      { duration: opts.durationMs, easing: opts.easing, fill: 'none' }
    );
    animation.finished.catch(() => {});
    current = animation;
    animation.finished.then(
      () => {
        if (current !== animation) return;
        current = null;
        if (tracking()) onContentResize(); // catch any content change that landed mid-tween
      },
      () => {}
    );
  }

  function setExpanded(next) {
    const normalized = next === true || next === false ? next : null;
    if (normalized === expanded) return;
    const from = currentHeight();
    expanded = normalized;
    if (expanded === false) {
      tween(from, 0, '0px');
    } else {
      const to = natural();
      tween(from, to, `${to}px`);
    }
  }

  function onContentResize() {
    if (!tracking()) return; // collapsed: content changes stay clipped at 0
    if (current) return; // mid-tween: our own writes, or an in-flight change we will re-check
    const to = natural();
    const from = currentHeight();
    if (Math.abs(from - to) < 0.5) return;
    tween(from, to, `${to}px`);
  }

  let ro = null;
  if (typeof ResizeObserver === 'function') {
    ro = new ResizeObserver(onContentResize);
    ro.observe(content);
  }

  // Initial rest state, applied without animation (existing content never animates in).
  element.style.height = expanded === false ? '0px' : `${natural()}px`;

  return {
    setExpanded,
    remeasure() {
      onContentResize();
    },
    destroy() {
      if (ro) ro.disconnect();
      if (current) {
        current.cancel();
        current = null;
      }
    },
  };
}

// --- Small numeric helpers (M3) ----------------------------------------------

function clamp01(value) {
  return value < 0 ? 0 : value > 1 ? 1 : value;
}

// Round to 4 decimals: stagger 'center' on an even child count yields half-steps,
// so a fixed rounding keeps the emitted CSS var stable and short.
function round4(value) {
  return Math.round(value * 10000) / 10000;
}

// --- Stagger delay math (M3) --------------------------------------------------
// The distance-from-anchor formula, duplicated in C# (Navius.Motion.MotionStagger);
// the JS agreement test compares the two so authoring (C#) and runtime (JS) never
// drift. `from` is 'first' (default), 'last' or 'center'. Center distance is
// fractional for an even count (Motion's stagger semantics), so delays can be
// half-steps. Returns an array of delays in milliseconds, index-aligned to children.

export function staggerDelays(count, stepMs, from) {
  const n = Math.max(0, count | 0);
  const delays = new Array(n);
  const center = (n - 1) / 2;
  for (let i = 0; i < n; i++) {
    let distance;
    if (from === 'last') distance = n - 1 - i;
    else if (from === 'center') distance = Math.abs(i - center);
    else distance = i; // 'first'
    delays[i] = round4(distance * stepMs);
  }
  return delays;
}

// Set --navius-motion-delay on each direct element child of `container` from the
// stagger config, so the CSS tier's transition-delay staggers a group reveal. The
// var composes with the [data-in-view] / enter classes that already read it.
function applyStaggerDelays(container, step, from) {
  const children = [];
  for (const c of container.children) children.push(c);
  const delays = staggerDelays(children.length, step, from);
  children.forEach((child, i) => {
    child.style.setProperty('--navius-motion-delay', `${delays[i]}ms`);
  });
  return children;
}

// --- In-view (M3) -------------------------------------------------------------
// IntersectionObserver v1 (the universally safe visibility primitive; v2 is
// Chromium-only forever). Sets data-in-view on the element while it intersects and
// removes it when it leaves, unless `once`. The CSS tier's .motion-in-view-* classes
// start hidden and transition to visible on [data-in-view], so the reveal is a
// compositor transition with zero JS per frame. An optional dotNetRef receives coarse
// OnInView(bool) edges. With `stagger`, the same intersection also fans data-in-view
// out to the direct children (their --navius-motion-delay set up front), so a group
// reveals in sequence. options: { amount ('some'|'all'|0..1), margin, once, stagger:
// { step, from } }. Returns { destroy }.

export function createInView(element, options, dotNetRef) {
  const opts = Object.assign(
    { amount: 'some', margin: '0px', once: false, stagger: null },
    options || {}
  );
  const threshold =
    opts.amount === 'some' ? 0 : opts.amount === 'all' ? 1 : clamp01(Number(opts.amount) || 0);

  // The staggered child list is re-read on refresh() (below), so children added or removed
  // after creation get their delay var and, if the group is already revealed, data-in-view.
  let staggerChildren = opts.stagger
    ? applyStaggerDelays(element, opts.stagger.step ?? 50, opts.stagger.from ?? 'first')
    : null;

  const notify = (value) => {
    if (dotNetRef) Promise.resolve(dotNetRef.invokeMethodAsync('OnInView', value)).catch(() => {});
  };

  let inside = false;

  function setInView(on) {
    inside = on;
    if (on) {
      element.setAttribute('data-in-view', '');
      if (staggerChildren) for (const c of staggerChildren) c.setAttribute('data-in-view', '');
    } else {
      element.removeAttribute('data-in-view');
      if (staggerChildren) for (const c of staggerChildren) c.removeAttribute('data-in-view');
    }
  }

  // Recompute the per-child stagger delays after the child list changes (add/remove), the
  // in-view counterpart of createStagger().refresh(). If the group is already in view, the
  // new children pick up data-in-view too so they reveal in place. A no-op without stagger
  // (there is no per-child list to track).
  function refresh() {
    if (!opts.stagger) return;
    staggerChildren = applyStaggerDelays(element, opts.stagger.step ?? 50, opts.stagger.from ?? 'first');
    if (inside) for (const c of staggerChildren) c.setAttribute('data-in-view', '');
  }

  // No IntersectionObserver (SSR/prerender, ancient engine): reveal immediately so a
  // hidden-until-in-view element can never stay stuck invisible.
  if (typeof IntersectionObserver !== 'function') {
    setInView(true);
    notify(true);
    return { refresh, destroy() {} };
  }

  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        const on = entry.isIntersecting;
        if (on === inside) continue;
        setInView(on);
        notify(on);
        if (on && opts.once) observer.disconnect();
      }
    },
    { root: null, rootMargin: opts.margin, threshold }
  );
  observer.observe(element);

  return {
    refresh,
    destroy() {
      observer.disconnect();
    },
  };
}

// Standalone stagger: set the delay vars on a container's children without any
// in-view coupling (for groups that reveal by other means, e.g. an immediate enter).
// Returns { refresh, destroy }; refresh re-reads the child list.

export function createStagger(container, options) {
  const opts = Object.assign({ step: 50, from: 'first' }, options || {});
  function refresh() {
    applyStaggerDelays(container, opts.step, opts.from);
  }
  refresh();
  return { refresh, destroy() {} };
}

// --- Selection indicator (M3) -------------------------------------------------
// A spring-animated marker that moves to the active element within a container: the
// Navius Tabs/NavigationMenu setIndicatorPosition math generalized. Position rides a
// compositor transform (translate); size animates width/height so a pill/underline
// tracks items of differing size without scale distortion. The marker is pinned
// position:absolute at the container's top-left; measurements are relative to the
// container's content box (border + scroll compensated), mirroring the auto-animate
// positionAbsolute math. Resize-aware via a ResizeObserver (re-measures and snaps, no
// animation, so a viewport resize never janks). options: { activeSelector, axis
// ('x'|'y'|'both'), durationMs, easing (a baked linear() spring), reduceMotion }.
// Returns { update, destroy }; call update() after the active element changes.

export function createSelectionIndicator(container, indicator, options) {
  const opts = Object.assign(
    { activeSelector: '[data-active]', axis: 'both', durationMs: 200, easing: 'ease', reduceMotion: 'user' },
    options || {}
  );
  const useX = opts.axis === 'x' || opts.axis === 'both';
  const useY = opts.axis === 'y' || opts.axis === 'both';

  let last = null; // last measured rect: the FLIP "First" for the next move
  let current = null; // in-flight move (cancelled before a new move or a snap)

  function activeElement() {
    return container.querySelector(opts.activeSelector);
  }

  function measure(el) {
    const cRect = container.getBoundingClientRect();
    const style = getComputedStyle(container);
    const borderLeft = parseFloat(style.borderLeftWidth) || 0;
    const borderTop = parseFloat(style.borderTopWidth) || 0;
    const r = el.getBoundingClientRect();
    return {
      left: r.left - cRect.left - borderLeft + container.scrollLeft,
      top: r.top - cRect.top - borderTop + container.scrollTop,
      width: r.width,
      height: r.height,
    };
  }

  function frameFor(rect) {
    const tx = useX ? rect.left : 0;
    const ty = useY ? rect.top : 0;
    const frame = { transform: `translate(${tx}px, ${ty}px)` };
    if (useX) frame.width = `${rect.width}px`;
    if (useY) frame.height = `${rect.height}px`;
    return frame;
  }

  function apply(animate) {
    const el = activeElement();
    if (!el) {
      indicator.style.visibility = 'hidden';
      return;
    }
    indicator.style.visibility = '';
    const rect = measure(el);
    const to = frameFor(rect);
    if (current) {
      current.cancel();
      current = null;
    }
    if (animate && last && !shouldReduceMotion(opts.reduceMotion)) {
      const from = frameFor(last);
      current = indicator.animate([from, to], {
        duration: opts.durationMs,
        easing: opts.easing,
        fill: 'both',
      });
      current.finished.catch(() => {});
    } else {
      // First placement, a resize snap, or reduced motion: land immediately. No
      // animation is holding a fill, so the inline style is what shows.
      Object.assign(indicator.style, to);
    }
    last = rect;
  }

  indicator.style.position = 'absolute';
  indicator.style.left = '0';
  indicator.style.top = '0';
  indicator.style.transformOrigin = 'top left';
  apply(false); // initial snap to the active element

  let resize = null;
  if (typeof ResizeObserver === 'function') {
    resize = new ResizeObserver(() => apply(false));
    resize.observe(container);
  }

  return {
    update() {
      apply(true);
    },
    destroy() {
      if (resize) resize.disconnect();
      if (current) current.cancel();
      current = null;
    },
  };
}

// --- Same-document view transitions (M3) -------------------------------------
// The honest, race-free driver for a Blazor same-document navigation. Because Blazor
// mutates the DOM through its own render cycle (not inside the View Transition
// callback), we split the transition into two interop calls: startViewTransition()
// begins the transition and resolves ONLY after the browser has captured the old
// snapshot (the transition callback has fired); the caller then performs the Blazor
// navigation and, once the new page has rendered, calls finishViewTransition() to let
// the browser capture the new state and cross-fade. Because the callback returns a
// promise we hold pending, the old snapshot is guaranteed to precede the DOM update.
// Guarded: with reduced motion or no document.startViewTransition support, it resolves
// false and the caller navigates instantly (the API is inherently progressive). A
// single in-flight transition at a time (one page-transition wrapper is the contract);
// a new start supersedes a stuck one, and a safety timeout resolves a missed finish.

let activeViewTransition = null;

export function startViewTransition(options) {
  const opts = Object.assign({ reduceMotion: 'user', timeoutMs: 2000 }, options || {});
  if (
    shouldReduceMotion(opts.reduceMotion) ||
    typeof document === 'undefined' ||
    typeof document.startViewTransition !== 'function'
  ) {
    return Promise.resolve(false);
  }

  // Supersede any stuck transition so navigation never wedges.
  if (activeViewTransition) {
    activeViewTransition.finish();
    activeViewTransition = null;
  }

  let resolveDone;
  const done = new Promise((resolve) => {
    resolveDone = resolve;
  });
  const entry = {
    finish() {
      resolveDone();
    },
  };
  activeViewTransition = entry;

  return new Promise((resolveStarted) => {
    const transition = document.startViewTransition(() => {
      // Fired AFTER the old snapshot is captured. Signal the caller to navigate, and
      // keep the DOM-update phase pending until finishViewTransition() resolves it.
      resolveStarted(true);
      return done;
    });
    transition.finished.catch(() => {});
    transition.finished.finally(() => {
      if (activeViewTransition === entry) activeViewTransition = null;
    });
    // Safety net: if a finish is ever missed, unblock the page.
    setTimeout(() => {
      if (activeViewTransition === entry) entry.finish();
    }, opts.timeoutMs);
  });
}

export function finishViewTransition() {
  if (activeViewTransition) activeViewTransition.finish();
}

// Resolve after `n` animation frames: the page-transition wrapper waits a couple of
// frames after the new render so the fresh DOM has laid out before the new snapshot.
export function nextFrames(n) {
  const count = Math.max(1, n | 0);
  return new Promise((resolve) => {
    let remaining = count;
    function tick() {
      remaining -= 1;
      if (remaining <= 0) resolve();
      else requestAnimationFrame(tick);
    }
    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(tick);
    else resolve();
  });
}

// --- Sequence executor (M3) ---------------------------------------------------
// The imperative plane. GroupEffect/SequenceEffect are unimplemented in every engine,
// so we own the scheduling: C# (Navius.Motion.MotionSequence) resolves every segment's
// at-offset to an absolute start time and bakes its spring to a linear() easing, then
// serializes the timeline. Here we simply materialize one WAAPI animation per segment
// with delay = its absolute start, so the browser's own clock keeps them in lockstep:
// each animation's delay bakes its place in the timeline, therefore setting every
// animation's currentTime to the same master time yields a coherent snapshot, which is
// what makes seek() a one-liner. Deterministic, no rAF loop, no per-frame callbacks.
//
// NOT supported (documented limits): per-frame / onUpdate callbacks, nested sequences,
// and infinite iterations. A segment target resolves to a single element (a selector's
// first match, scoped to `root`, or a serialized ElementReference). program:
// { segments: [{ target: { selector?|ref? }, keyframes, startMs, durationMs, easing,
// composite }], totalMs, reduceMotion }. Returns a handle: play/pause/seek(ms)/stop,
// finished() (a Promise resolving when the whole timeline completes), duration(),
// destroy().

function resolveTarget(target, scope) {
  if (!target) return null;
  // A serialized ElementReference is revived to the DOM element (recursive __internalId
  // reviver), so target.ref is already the node.
  if (target.ref && typeof target.ref.animate === 'function') return target.ref;
  if (target.selector) return scope.querySelector(target.selector);
  return null;
}

export function runProgram(program, root) {
  const prog = program || {};
  const segments = prog.segments || [];
  const reduce = shouldReduceMotion(prog.reduceMotion);
  const scope = root || (typeof document !== 'undefined' ? document : null);
  const animations = [];
  let total = 0;

  for (const segment of segments) {
    const end = (segment.startMs || 0) + (segment.durationMs || 0);
    if (end > total) total = end;

    const element = resolveTarget(segment.target, scope);
    if (!element) continue;

    let frames = (segment.keyframes || []).map(sanitizeFrame);
    if (reduce) frames = collapseToOpacity(frames);
    if (!hasAnimatableProps(frames)) continue;

    const animation = element.animate(frames, {
      delay: segment.startMs || 0,
      duration: segment.durationMs || 0,
      easing: segment.easing || 'linear',
      fill: 'both',
      composite: segment.composite || 'replace',
    });
    animation.pause();
    animation.currentTime = 0;
    animation.finished.catch(() => {});
    animations.push(animation);
  }

  if (typeof prog.totalMs === 'number') total = prog.totalMs;

  return {
    play() {
      // Resume from the current position. WAAPI's play() auto-rewinds any animation whose
      // currentTime is at/after its effect end (resets it to 0), so on a staggered timeline
      // a plain play() would replay the segments that already finished while the others
      // resume, desyncing the timeline. Skip the finished segments: fill:'both' holds them
      // on their final frame, which is where a resume must leave them. Replay from the top
      // is stop() (rewinds every segment) followed by play().
      for (const a of animations) {
        const endTime = a.effect ? Number(a.effect.getComputedTiming().endTime) : Infinity;
        if (a.currentTime !== null && Number(a.currentTime) >= endTime) continue;
        a.play();
      }
    },
    pause() {
      for (const a of animations) a.pause();
    },
    seek(ms) {
      const t = Math.max(0, Math.min(Number(ms) || 0, total));
      for (const a of animations) a.currentTime = t;
    },
    stop() {
      for (const a of animations) {
        a.pause();
        a.currentTime = 0;
      }
    },
    finished() {
      return Promise.all(animations.map((a) => a.finished.catch(() => {})));
    },
    duration() {
      return total;
    },
    destroy() {
      for (const a of animations) a.cancel();
      animations.length = 0;
    },
  };
}
