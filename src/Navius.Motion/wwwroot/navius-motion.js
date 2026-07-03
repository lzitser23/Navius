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
