// Navius headless engine — the parts C# can't do directly in the browser.
//
// Blazor (WASM has no DOM; Server marshals interop async over SignalR) cannot
// synchronously read the DOM, move focus, or measure elements. So the genuinely
// hard accessibility behaviour lives here in JS and is driven from C#. This is
// the DOM-touching behaviour layer most Blazor component kits skip or hide — Navius builds it in the open.

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
  '[contenteditable="true"]',
].join(',');

function getFocusable(container) {
  return Array.from(container.querySelectorAll(FOCUSABLE_SELECTOR)).filter(
    (el) => el.offsetWidth > 0 || el.offsetHeight > 0 || el === document.activeElement
  );
}

// Trap Tab/Shift+Tab inside `container`, focus the first focusable element, and
// restore focus to whatever was focused before on release. Returns a small
// handle object; C# keeps a reference to it (IJSObjectReference) and calls
// `release()` on teardown.
//
// options (all optional, defaults preserve original behaviour):
//   initialFocus: a selector string or an element to focus on creation instead
//     of the first focusable. Falls back to the first focusable / container.
//
// release(restoreFocus = true): pass false to skip restoring focus to whatever
// was focused before the trap. Default (no arg) restores, so existing callers
// that call release() are unaffected.
export function createFocusTrap(container, options) {
  const opts = options || {};
  const previouslyFocused =
    document.activeElement instanceof HTMLElement ? document.activeElement : null;

  function onKeydown(e) {
    if (e.key !== 'Tab') return;
    const focusable = getFocusable(container);
    if (focusable.length === 0) {
      e.preventDefault();
      container.focus();
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    if (e.shiftKey && (active === first || !container.contains(active))) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && (active === last || !container.contains(active))) {
      e.preventDefault();
      first.focus();
    }
  }

  // Capture phase so we win over anything inside the container.
  document.addEventListener('keydown', onKeydown, true);

  const focusable = getFocusable(container);
  let initial = null;
  if (opts.initialFocus) {
    initial =
      typeof opts.initialFocus === 'string'
        ? container.querySelector(opts.initialFocus)
        : opts.initialFocus;
  }
  (initial ?? focusable[0] ?? container).focus();

  return {
    release(restoreFocus = true) {
      document.removeEventListener('keydown', onKeydown, true);
      if (restoreFocus && previouslyFocused && document.contains(previouslyFocused)) {
        previouslyFocused.focus();
      }
    },
  };
}

// Reference-counted body scroll lock so nested/stacked overlays compose cleanly.
let scrollLockCount = 0;
let savedOverflow = '';
let savedPaddingRight = '';

export function lockScroll() {
  if (scrollLockCount === 0) {
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    savedOverflow = document.body.style.overflow;
    savedPaddingRight = document.body.style.paddingRight;
    document.body.style.overflow = 'hidden';
    // Compensate for the scrollbar disappearing so the layout doesn't shift.
    if (scrollbarWidth > 0) {
      document.body.style.paddingRight = `${scrollbarWidth}px`;
    }
  }
  scrollLockCount++;
}

export function unlockScroll() {
  scrollLockCount = Math.max(0, scrollLockCount - 1);
  if (scrollLockCount === 0) {
    document.body.style.overflow = savedOverflow;
    document.body.style.paddingRight = savedPaddingRight;
  }
}

// --- Positioning -------------------------------------------------------------
// A compact, self-contained anchored-positioning engine (the Floating-UI role).
// Places `floating` next to `reference` with side/align/offset, flips to the
// opposite side on collision, clamps into the viewport, and re-runs on scroll
// and resize. Returns a handle; C# calls update()/destroy().
// `arrowElement` may be supplied either on `options.arrowElement` or as the 4th
// argument — the latter lets C# pass a live DOM node (which cannot ride inside the
// JSON-serialized options record) without breaking the 3-arg signature.
// `popupArg` (5th argument) is the Base UI Popup element nested inside `floating`
// (the Positioner): when present the engine mirrors data-side / data-align /
// data-anchor-hidden onto it as well, so helm `data-[side=…]` style hooks match on
// the visual Popup, not just the positioning wrapper.
export function createPositioner(reference, floating, options, arrowArg, popupArg) {
  const opts = Object.assign(
    { side: 'bottom', align: 'center', sideOffset: 8, alignOffset: 0, flip: true, padding: 8 },
    options || {}
  );
  if (arrowArg) opts.arrowElement = arrowArg;
  const popupElement = popupArg || null;

  // New collision/arrow options. Defaults map to today's behaviour: avoidCollisions
  // true == the original flip+clamp (still gated by opts.flip for flipping);
  // collisionPadding falls back to padding; sticky/hideWhenDetached/arrow off.
  const avoidCollisions = opts.avoidCollisions !== false;
  const collisionPadding =
    opts.collisionPadding != null ? opts.collisionPadding : opts.padding;
  const sticky = opts.sticky || null; // 'partial' | 'always'
  const hideWhenDetached = !!opts.hideWhenDetached;
  const arrowElement = opts.arrowElement || null;
  const arrowPadding = opts.arrowPadding != null ? opts.arrowPadding : 0;

  // transform-origin keyword for a resolved side/align (so animations scale from
  // the anchored edge, matching the spec's --transform-origin role).
  function originFor(side, align) {
    if (side === 'top' || side === 'bottom') {
      const v = side === 'bottom' ? 'top' : 'bottom';
      const h = align === 'start' ? 'left' : align === 'end' ? 'right' : 'center';
      return `${h} ${v}`;
    }
    const h = side === 'right' ? 'left' : 'right';
    const v = align === 'start' ? 'top' : align === 'end' ? 'bottom' : 'center';
    return `${h} ${v}`;
  }

  function compute() {
    const r = reference.getBoundingClientRect();
    const f = floating.getBoundingClientRect();
    const vw = document.documentElement.clientWidth;
    const vh = document.documentElement.clientHeight;
    const pad = avoidCollisions ? collisionPadding : opts.padding;

    let side = opts.side;
    const align = opts.align;

    // RTL: the logical align (start/end) mirrors on the horizontal axis, so a start-aligned
    // top/bottom popup hugs the anchor's RIGHT edge under dir=rtl. Read the anchor's resolved
    // reading direction (honours a DirectionProvider) instead of threading Dir through every
    // context. `alignH` is the physical alignment used for the x math + transform-origin; the
    // logical `align` is still what we publish as data-align. Vertical align (on left/right
    // sides) is unaffected by reading direction, so it keeps using `align`. The flip below
    // only swaps a side for its opposite on the SAME axis, so a top/bottom stays top/bottom —
    // `alignH` computed here from the initial side remains valid after a flip.
    let rtl = false;
    try { rtl = getComputedStyle(reference).direction === 'rtl'; } catch { /* reference detached */ }
    const alignH =
      rtl && (side === 'top' || side === 'bottom')
        ? (align === 'start' ? 'end' : align === 'end' ? 'start' : 'center')
        : align;

    if (opts.flip && avoidCollisions) {
      const space = { top: r.top, bottom: vh - r.bottom, left: r.left, right: vw - r.right };
      const isVertical = side === 'top' || side === 'bottom';
      const need = (isVertical ? f.height : f.width) + opts.sideOffset + pad;
      const opposite = { top: 'bottom', bottom: 'top', left: 'right', right: 'left' }[side];
      if (space[side] < need && space[opposite] > space[side]) side = opposite;
    }

    let x, y;
    if (side === 'bottom' || side === 'top') {
      y = side === 'bottom' ? r.bottom + opts.sideOffset : r.top - f.height - opts.sideOffset;
      x =
        alignH === 'start' ? r.left + opts.alignOffset
        : alignH === 'end' ? r.right - f.width - opts.alignOffset
        : r.left + (r.width - f.width) / 2 + opts.alignOffset;
    } else {
      x = side === 'right' ? r.right + opts.sideOffset : r.left - f.width - opts.sideOffset;
      y =
        align === 'start' ? r.top + opts.alignOffset
        : align === 'end' ? r.bottom - f.height - opts.alignOffset
        : r.top + (r.height - f.height) / 2 + opts.alignOffset;
    }

    // Shift/clamp into the viewport. Skipped entirely when avoidCollisions is off.
    if (avoidCollisions) {
      x = Math.max(pad, Math.min(x, vw - f.width - pad));
      y = Math.max(pad, Math.min(y, vh - f.height - pad));

      // sticky: keep the floating element attached to the anchor's main axis even
      // when it would otherwise be clamped off it. 'partial' keeps at least a sliver
      // overlapping; 'always' keeps it fully within the anchor's extent.
      if (sticky) {
        if (side === 'top' || side === 'bottom') {
          const lo = sticky === 'always' ? r.left : r.left - f.width;
          const hi = sticky === 'always' ? r.right - f.width : r.right;
          x = Math.max(Math.min(x, hi), lo);
        } else {
          const lo = sticky === 'always' ? r.top : r.top - f.height;
          const hi = sticky === 'always' ? r.bottom - f.height : r.bottom;
          y = Math.max(Math.min(y, hi), lo);
        }
      }
    }

    floating.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
    floating.setAttribute('data-side', side);
    floating.setAttribute('data-align', align);
    if (popupElement) {
      popupElement.setAttribute('data-side', side);
      popupElement.setAttribute('data-align', align);
    }

    // CSS custom props for consumers that size to the anchor and animate from the
    // resolved corner — the Base UI contract names (--anchor-*/--available-*/
    // --transform-origin). The legacy --navius-popper-* aliases were retired in Wave D
    // once Select/Menu/ContextMenu moved onto the un-prefixed names.
    const availW = vw - pad * 2;
    const availH = vh - pad * 2;
    // Physical alignment (alignH) so the popup animates from its true visual corner under RTL.
    const origin = originFor(side, alignH);
    const aw = `${Math.round(r.width)}px`;
    const ah = `${Math.round(r.height)}px`;
    const avw = `${Math.round(availW)}px`;
    const avh = `${Math.round(availH)}px`;
    floating.style.setProperty('--anchor-width', aw);
    floating.style.setProperty('--anchor-height', ah);
    floating.style.setProperty('--available-width', avw);
    floating.style.setProperty('--available-height', avh);
    floating.style.setProperty('--transform-origin', origin);
    floating.style.transformOrigin = origin;

    if (hideWhenDetached) {
      // Hidden when the anchor is fully scrolled out of the viewport. Base UI names
      // this data-anchor-hidden; legacy data-hide is kept as an alias.
      const detached =
        r.bottom < 0 || r.top > vh || r.right < 0 || r.left > vw;
      const setHidden = (el) => {
        el.setAttribute('data-anchor-hidden', '');
        el.setAttribute('data-hide', '');
      };
      const clearHidden = (el) => {
        el.removeAttribute('data-anchor-hidden');
        el.removeAttribute('data-hide');
      };
      if (detached) {
        floating.style.visibility = 'hidden';
        setHidden(floating);
        if (popupElement) setHidden(popupElement);
      } else {
        floating.style.visibility = '';
        clearHidden(floating);
        if (popupElement) clearHidden(popupElement);
      }
    }

    // Arrow: point its cross-axis position at the reference centre, clamped within
    // the floating box (minus arrowPadding), and rotate via a data-side attribute.
    if (arrowElement) {
      const ar = arrowElement.getBoundingClientRect();
      if (side === 'top' || side === 'bottom') {
        const center = r.left + r.width / 2 - x; // relative to floating's left edge
        const min = arrowPadding + ar.width / 2;
        const max = f.width - arrowPadding - ar.width / 2;
        const cx = Math.max(min, Math.min(center, max));
        arrowElement.style.position = 'absolute';
        arrowElement.style.left = `${Math.round(cx - ar.width / 2)}px`;
        arrowElement.style.right = '';
        arrowElement.style.top = side === 'bottom' ? `${-ar.height}px` : '';
        arrowElement.style.bottom = side === 'top' ? `${-ar.height}px` : '';
      } else {
        const center = r.top + r.height / 2 - y;
        const min = arrowPadding + ar.height / 2;
        const max = f.height - arrowPadding - ar.height / 2;
        const cy = Math.max(min, Math.min(center, max));
        arrowElement.style.position = 'absolute';
        arrowElement.style.top = `${Math.round(cy - ar.height / 2)}px`;
        arrowElement.style.bottom = '';
        arrowElement.style.left = side === 'right' ? `${-ar.width}px` : '';
        arrowElement.style.right = side === 'left' ? `${-ar.width}px` : '';
      }
      arrowElement.setAttribute('data-side', side);
    }
  }

  floating.style.position = 'fixed';
  floating.style.top = '0';
  floating.style.left = '0';
  floating.style.margin = '0';

  compute();

  const onScrollOrResize = () => compute();
  window.addEventListener('scroll', onScrollOrResize, true);
  window.addEventListener('resize', onScrollOrResize);

  // Re-place when the floating element or the anchor changes size (Floating-UI
  // autoUpdate's elementResize). Without this, an autocomplete/command listbox that
  // grows or shrinks as the user filters stays at the stale offset a collision
  // flip/clamp computed for the old (taller) size, detaching from the anchor.
  // compute() only writes the transform + constant --available-* vars, never the
  // floating box's own size, so observing `floating` cannot feed back into a resize
  // loop. Coalesced to one compute per frame to avoid layout thrash on rapid input.
  let roFrame = 0;
  let ro = null;
  if (typeof ResizeObserver !== 'undefined') {
    ro = new ResizeObserver(() => {
      if (roFrame) return;
      roFrame = requestAnimationFrame(() => {
        roFrame = 0;
        compute();
      });
    });
    ro.observe(floating);
    ro.observe(reference);
  }

  return {
    update() {
      compute();
    },
    destroy() {
      window.removeEventListener('scroll', onScrollOrResize, true);
      window.removeEventListener('resize', onScrollOrResize);
      if (roFrame) cancelAnimationFrame(roFrame);
      if (ro) ro.disconnect();
    },
  };
}

// --- Dismissable layer -------------------------------------------------------
// Closes an open overlay on Escape and on pointer-down outside both the content
// and its trigger. Calls back into .NET (OnDismiss) so C# stays the source of
// truth for open state. Reused by popover, menu, select, combobox, ...
export function createDismissableLayer(content, reference, dotNetRef, options, reference2) {
  const opts = Object.assign({ closeOnEscape: true, closeOnOutside: true }, options || {});

  function onPointerDown(e) {
    if (!opts.closeOnOutside) return;
    const t = e.target;
    if (content.contains(t)) return;
    if (reference && reference.contains(t)) return; // the trigger toggles itself
    if (reference2 && reference2.contains(t)) return; // a secondary trigger (Autocomplete's dropdown button) also toggles itself
    dotNetRef.invokeMethodAsync('OnDismiss', 'outside');
  }

  function onKeyDown(e) {
    if (opts.closeOnEscape && e.key === 'Escape') {
      dotNetRef.invokeMethodAsync('OnDismiss', 'escape');
    }
  }

  document.addEventListener('pointerdown', onPointerDown, true);
  document.addEventListener('keydown', onKeyDown, true);

  return {
    destroy() {
      document.removeEventListener('pointerdown', onPointerDown, true);
      document.removeEventListener('keydown', onKeyDown, true);
    },
  };
}

// --- Roving focus ------------------------------------------------------------
// The composite-widget keyboard model (menus, menubars, toolbars). Arrow keys
// move focus between items (which are tabindex=-1, so Tab doesn't land on them),
// Home/End jump to ends, and a printable key does type-ahead. preventDefault on
// the navigation keys stops the page from scrolling. Items are matched live, so
// dynamic menus work. Reused by menu, menubar, select, ...
export function createRovingFocus(container, options) {
  const opts = Object.assign({ orientation: 'vertical' }, options || {});
  const selector = opts.selector || '[role="menuitem"]:not([data-disabled])';

  // New options (defaults preserve original behaviour):
  //   loop (default true): modulo-wrap at the ends; false clamps at 0 / len-1.
  //   autoFocus (default true): focus an item on creation; false focuses nothing.
  //   dataHighlight (default false): mirror focus to data-highlighted="" and react
  //     to pointerenter/leave/blur (the spec menu-item highlight model).
  //   dir ('ltr' | 'rtl', default 'ltr'): for horizontal orientation, swap the
  //     next/prev arrow keys under rtl.
  const loop = opts.loop !== false;
  const autoFocus = opts.autoFocus !== false;
  const dataHighlight = opts.dataHighlight === true;
  const rtl = opts.dir === 'rtl';

  const items = () => Array.from(container.querySelectorAll(selector));

  // Text used for type-ahead: prefer an explicit data-navius-text-value when set.
  const textValueOf = (el) =>
    (el.getAttribute('data-navius-text-value') ?? el.textContent ?? '')
      .trim()
      .toLowerCase();

  const setHighlight = (el) => {
    if (!dataHighlight) return;
    for (const it of items()) {
      if (it === el) it.setAttribute('data-highlighted', '');
      else it.removeAttribute('data-highlighted');
    }
  };

  const focusItem = (el) => {
    if (!el) return;
    el.focus();
    setHighlight(el);
  };

  const indexFor = (list, i) => {
    if (loop) return (i + list.length) % list.length;
    return Math.max(0, Math.min(i, list.length - 1));
  };
  const focusAt = (list, i) => {
    if (list.length) focusItem(list[indexFor(list, i)]);
  };

  function onKeyDown(e) {
    const list = items();
    if (!list.length) return;
    // Nested roving layers (open submenus) own their own arrow keys. If focus is
    // inside a descendant roving container, let that inner layer handle the event
    // and do NOT steal focus back to this (outer) container's first item.
    const owner =
      document.activeElement && document.activeElement.closest
        ? document.activeElement.closest('[data-navius-roving]')
        : null;
    if (owner && owner !== container) return;
    const i = list.indexOf(document.activeElement);
    const horizontal = opts.orientation === 'horizontal';
    let nextKey = horizontal ? 'ArrowRight' : 'ArrowDown';
    let prevKey = horizontal ? 'ArrowLeft' : 'ArrowUp';
    if (horizontal && rtl) {
      const tmp = nextKey;
      nextKey = prevKey;
      prevKey = tmp;
    }

    if (e.key === nextKey) {
      e.preventDefault();
      focusAt(list, i < 0 ? 0 : i + 1);
    } else if (e.key === prevKey) {
      e.preventDefault();
      focusAt(list, i < 0 ? list.length - 1 : i - 1);
    } else if (e.key === 'Home') {
      e.preventDefault();
      focusAt(list, 0);
    } else if (e.key === 'End') {
      e.preventDefault();
      focusAt(list, list.length - 1);
    } else if (e.key === ' ') {
      e.preventDefault(); // activation is handled by the item; just stop page scroll
    } else if (horizontal && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      // Menubar/Toolbar triggers: the open key is handled by the component; just
      // stop the page from scrolling so the menu can open in place.
      e.preventDefault();
    } else if (e.key.length === 1 && /\S/.test(e.key)) {
      const ch = e.key.toLowerCase();
      const start = (i + 1) % list.length;
      for (let k = 0; k < list.length; k++) {
        const item = list[(start + k) % list.length];
        if (textValueOf(item).startsWith(ch)) {
          focusItem(item);
          break;
        }
      }
    }
  }

  container.setAttribute('data-navius-roving', ''); // marks this as a roving layer (nested-submenu guard)
  container.addEventListener('keydown', onKeyDown);

  // Pointer-driven highlighting (the spec moves the active item under the cursor).
  function onPointerEnter(e) {
    const item = e.target instanceof Element ? e.target.closest(selector) : null;
    if (item && container.contains(item)) focusItem(item);
  }
  function onPointerLeave(e) {
    const item = e.target instanceof Element ? e.target.closest(selector) : null;
    if (item) item.removeAttribute('data-highlighted');
  }
  function onBlur(e) {
    const item = e.target instanceof Element ? e.target.closest(selector) : null;
    if (item) item.removeAttribute('data-highlighted');
  }
  if (dataHighlight) {
    container.addEventListener('pointerenter', onPointerEnter, true);
    container.addEventListener('pointerleave', onPointerLeave, true);
    container.addEventListener('blur', onBlur, true);
  }

  const initial = items();
  if (autoFocus && initial.length) {
    // Initial focus target. 'first'/'last' (the Select trigger's ArrowDown/ArrowUp on a closed
    // trigger) land on the first / last item; the default 'selected' lands on the selected
    // option if there is one (listbox), else the first.
    const initialFocus = opts.initialFocus || 'selected';
    let target;
    if (initialFocus === 'first') target = initial[0];
    else if (initialFocus === 'last') target = initial[initial.length - 1];
    else target = initial.find((el) => el.getAttribute('aria-selected') === 'true') || initial[0];
    focusItem(target);
  }

  return {
    focusFirst() {
      const l = items();
      if (l.length) focusItem(l[0]);
    },
    destroy() {
      container.removeAttribute('data-navius-roving');
      container.removeEventListener('keydown', onKeyDown);
      if (dataHighlight) {
        container.removeEventListener('pointerenter', onPointerEnter, true);
        container.removeEventListener('pointerleave', onPointerLeave, true);
        container.removeEventListener('blur', onBlur, true);
      }
    },
  };
}

// --- Focus by id -------------------------------------------------------------
// Focus an element by id, caret at the end (e.g. return focus to a password
// input after a pointer toggle). Used by the Password Toggle Field primitive.
export function focusElementById(id) {
  const el = document.getElementById(id);
  if (!el) return;
  el.focus();
  if (typeof el.value === 'string' && typeof el.setSelectionRange === 'function') {
    const n = el.value.length;
    try {
      el.setSelectionRange(n, n);
    } catch {
      /* not a text input */
    }
  }
}

// --- Drag tracker (slider) ---------------------------------------------------
// Translates a pointer position over `trackElement` into a 0..1 fraction and
// streams it to .NET (OnFraction); C# snaps + clamps. Captures the pointer so the
// drag keeps tracking outside the element.
export function createDragTracker(trackElement, dotNetRef, options) {
  const opts = Object.assign({ orientation: 'horizontal' }, options || {});
  const vertical = opts.orientation === 'vertical';
  let dragging = false;

  function fractionFromEvent(e) {
    const rect = trackElement.getBoundingClientRect();
    if (vertical) {
      const h = rect.height || 1;
      return Math.max(0, Math.min(1, (rect.bottom - e.clientY) / h)); // up = larger
    }
    const w = rect.width || 1;
    return Math.max(0, Math.min(1, (e.clientX - rect.left) / w));
  }

  const emit = (e) => dotNetRef.invokeMethodAsync('OnFraction', fractionFromEvent(e));

  function onPointerDown(e) {
    if (e.button !== 0) return;
    dragging = true;
    try { trackElement.setPointerCapture(e.pointerId); } catch {}
    e.preventDefault();
    emit(e);
  }
  function onPointerMove(e) { if (dragging) { e.preventDefault(); emit(e); } }
  function onPointerUp(e) {
    if (!dragging) return;
    dragging = false;
    try { trackElement.releasePointerCapture(e.pointerId); } catch {}
    // Also signal the end of the drag so C# can fire onValueCommit. OnFraction has
    // already streamed the latest position; OnCommit marks the gesture complete.
    // Swallow the rejection if a consumer hasn't (yet) declared [JSInvokable]
    // OnCommit — this stays a pure no-op extension for existing sliders.
    Promise.resolve(dotNetRef.invokeMethodAsync('OnCommit', fractionFromEvent(e))).catch(() => {});
  }

  trackElement.addEventListener('pointerdown', onPointerDown);
  trackElement.addEventListener('pointermove', onPointerMove);
  trackElement.addEventListener('pointerup', onPointerUp);
  trackElement.addEventListener('pointercancel', onPointerUp);

  return {
    destroy() {
      trackElement.removeEventListener('pointerdown', onPointerDown);
      trackElement.removeEventListener('pointermove', onPointerMove);
      trackElement.removeEventListener('pointerup', onPointerUp);
      trackElement.removeEventListener('pointercancel', onPointerUp);
    },
  };
}

// --- Scroll-area observer ----------------------------------------------------
// Reports a viewport's scroll/resize geometry to .NET (OnScrollMetrics) so C# can
// size + position the custom thumb. Native overflow is the source of truth.
export function observeScrollArea(viewport, dotNetRef) {
  const metrics = () => ({
    scrollTop: viewport.scrollTop,
    scrollHeight: viewport.scrollHeight,
    clientHeight: viewport.clientHeight,
    scrollLeft: viewport.scrollLeft,
    scrollWidth: viewport.scrollWidth,
    clientWidth: viewport.clientWidth,
  });
  const report = () => dotNetRef.invokeMethodAsync('OnScrollMetrics', metrics());

  // On every actual scroll event, also signal activity so C# can reveal the
  // scrollbars just-in-time (the spec scrollHideDelay model). Metrics still fire.
  // The activity ping is swallowed if a consumer hasn't declared [JSInvokable]
  // OnScrollActivity, so existing scroll areas are unaffected.
  const onScroll = () => {
    report();
    Promise.resolve(dotNetRef.invokeMethodAsync('OnScrollActivity')).catch(() => {});
  };

  viewport.addEventListener('scroll', onScroll, { passive: true });
  window.addEventListener('resize', report);
  let ro = null;
  if (typeof ResizeObserver !== 'undefined') {
    ro = new ResizeObserver(report);
    ro.observe(viewport);
    if (viewport.firstElementChild) ro.observe(viewport.firstElementChild);
  }
  report();

  return {
    update() { report(); },
    destroy() {
      viewport.removeEventListener('scroll', onScroll);
      window.removeEventListener('resize', report);
      if (ro) ro.disconnect();
    },
  };
}

// --- Scroll-thumb drag -------------------------------------------------------
// Maps a pointer drag on the custom thumb onto viewport.scrollTop/Left.
export function dragScrollThumb(viewport, thumb, orientation) {
  const vertical = orientation !== 'horizontal';
  let dragging = false;
  let start = 0;
  let startScroll = 0;

  function onPointerDown(e) {
    if (e.button !== 0) return;
    dragging = true;
    start = vertical ? e.clientY : e.clientX;
    startScroll = vertical ? viewport.scrollTop : viewport.scrollLeft;
    try { thumb.setPointerCapture(e.pointerId); } catch {}
    e.preventDefault();
  }
  function onPointerMove(e) {
    if (!dragging) return;
    const trackLen = vertical ? viewport.clientHeight : viewport.clientWidth;
    const scrollLen = vertical ? viewport.scrollHeight : viewport.scrollWidth;
    const maxScroll = scrollLen - trackLen;
    if (maxScroll <= 0) return;
    const thumbLen = trackLen * (trackLen / scrollLen);
    const usable = trackLen - thumbLen || 1;
    const delta = (vertical ? e.clientY : e.clientX) - start;
    // In an RTL viewport the horizontal scroll axis is mirrored, so a rightward
    // pointer drag must decrease scrollLeft. Detect direction from the live DOM.
    const rtl = !vertical && getComputedStyle(viewport).direction === 'rtl';
    const next = startScroll + (rtl ? -1 : 1) * (delta / usable) * maxScroll;
    if (vertical) viewport.scrollTop = next;
    else viewport.scrollLeft = next;
    e.preventDefault();
  }
  function onPointerUp(e) {
    if (!dragging) return;
    dragging = false;
    try { thumb.releasePointerCapture(e.pointerId); } catch {}
  }

  thumb.addEventListener('pointerdown', onPointerDown);
  thumb.addEventListener('pointermove', onPointerMove);
  thumb.addEventListener('pointerup', onPointerUp);
  thumb.addEventListener('pointercancel', onPointerUp);

  return {
    destroy() {
      thumb.removeEventListener('pointerdown', onPointerDown);
      thumb.removeEventListener('pointermove', onPointerMove);
      thumb.removeEventListener('pointerup', onPointerUp);
      thumb.removeEventListener('pointercancel', onPointerUp);
    },
  };
}

// --- Teleport (Portal) -------------------------------------------------------
// Move an element to document.body (or a given container element / selector) so
// overlays escape any clipping/stacking ancestor. The returned handle's
// destroy() moves the element back to its original DOM position. Used by Portal
// and every portal-using overlay.
export function teleportToBody(element, container) {
  let target = document.body;
  if (container) {
    target =
      typeof container === 'string' ? document.querySelector(container) : container;
    if (!target) target = document.body;
  }

  // Remember where it came from so we can restore it exactly.
  const originalParent = element.parentNode;
  const originalNextSibling = element.nextSibling;

  // Moving a node in the DOM blurs any focus inside it. Modal overlays (dialog,
  // menu) focus their content right before the portal teleports it, so preserve
  // and restore that focus across the move — otherwise focus silently drops to
  // <body> and the focus trap / roving model lands on nothing.
  const active = document.activeElement;
  target.appendChild(element);
  if (
    active instanceof HTMLElement &&
    element.contains(active) &&
    document.activeElement !== active
  ) {
    try { active.focus({ preventScroll: true }); } catch { /* not focusable anymore */ }
  }

  return {
    destroy() {
      if (!originalParent) {
        if (element.parentNode) element.parentNode.removeChild(element);
        return;
      }
      if (originalNextSibling && originalNextSibling.parentNode === originalParent) {
        originalParent.insertBefore(element, originalNextSibling);
      } else {
        originalParent.appendChild(element);
      }
    },
  };
}

// --- Size observer (Collapsible / Accordion) ---------------------------------
// Publish an element's natural content size as CSS custom props so C# can animate
// open/close with CSS transitions on height/width. Emits the Base UI canonical
// names (--{prefix}-panel-width/height, e.g. --collapsible-panel-height). Updates
// on mount and on resize.
export function createSizeObserver(element, varPrefix) {
  const prefix = varPrefix || 'collapsible';
  const apply = () => {
    const w = `${element.scrollWidth}px`;
    const h = `${element.scrollHeight}px`;
    element.style.setProperty(`--${prefix}-panel-width`, w);
    element.style.setProperty(`--${prefix}-panel-height`, h);
  };

  apply();

  let ro = null;
  if (typeof ResizeObserver !== 'undefined') {
    ro = new ResizeObserver(apply);
    ro.observe(element);
  }

  return {
    update() { apply(); },
    destroy() {
      if (ro) ro.disconnect();
    },
  };
}

// --- Presence / enter-exit transitions (Base UI animation model) -------------
// Base UI's discrete-attribute animation contract: an element animates IN while
// it carries [data-starting-style] (removed one frame after mount so the CSS
// transition runs to the open state) and animates OUT while it carries
// [data-ending-style] (kept until the transition finishes, then the node is
// unmounted). C# owns those attributes (rendered from _entering/_exiting flags);
// the engine only provides the two pieces of timing C# can't do synchronously:
//
//   nextFrame(): resolve after the browser has painted (double rAF) so the
//     starting-style frame is committed before C# removes it.
//   waitForAnimations(element): resolve once every running CSS transition/
//     animation on the element has finished (or immediately if there are none /
//     reduced motion), so C# can defer unmount until the exit animation ends.
export function nextFrame() {
  return new Promise((resolve) => {
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
  });
}

export function waitForAnimations(element) {
  return new Promise((resolve) => {
    // Let the just-rendered ending-style commit so its transition is created,
    // then await all running animations on the element.
    requestAnimationFrame(async () => {
      if (!element || typeof element.getAnimations !== 'function') {
        resolve();
        return;
      }
      try {
        await Promise.all(element.getAnimations().map((a) => a.finished.catch(() => {})));
      } catch {
        /* animations cancelled (e.g. re-open mid-exit) — fall through */
      }
      resolve();
    });
  });
}

// hidden="until-found" panels (Collapsible/Accordion hiddenUntilFound) fire a
// 'beforematch' event when the browser's in-page find expands them; relay it to
// C# so it can flip the component open. Returns a handle; destroy() detaches.
export function observeBeforeMatch(element, dotNetRef) {
  const onBeforeMatch = () => {
    try { dotNetRef.invokeMethodAsync('OnBeforeMatch'); } catch { /* circuit gone */ }
  };
  element.addEventListener('beforematch', onBeforeMatch);
  return {
    destroy() {
      element.removeEventListener('beforematch', onBeforeMatch);
    },
  };
}

// --- NavigationMenu / Command helpers (CSP-safe; replace former eval() calls) -
// These were previously eval()'d source strings (an unsafe-eval CSP smell). They
// are now typed exports driven from C# with element refs / values, so the engine
// runs under a strict Content-Security-Policy (no 'unsafe-eval').

// Mirror the active panel's measured size onto the NavigationMenu viewport element
// as the canonical CSS vars, via ResizeObserver + MutationObserver on the teleport
// slot. Returns a handle; destroy() disconnects the observers.
export function createViewportMirror(viewport, slot) {
  if (!viewport || !slot) return { destroy() {} };
  const write = () => {
    const child = slot.firstElementChild || slot;
    const w = `${child.offsetWidth}px`;
    const h = `${child.offsetHeight}px`;
    viewport.style.setProperty('--navius-navigation-menu-viewport-width', w);
    viewport.style.setProperty('--navius-navigation-menu-viewport-height', h);
    viewport.style.setProperty('--navius-navigationmenu-viewport-width', w);
    viewport.style.setProperty('--navius-navigationmenu-viewport-height', h);
  };
  const ro = new ResizeObserver(write);
  ro.observe(slot);
  const mo = new MutationObserver(write);
  mo.observe(slot, { childList: true });
  write();
  return {
    destroy() { ro.disconnect(); mo.disconnect(); },
  };
}

// Write the active trigger's size/position onto the NavigationMenu indicator
// element as CSS vars (C# measures the trigger rect and passes the values).
export function setIndicatorPosition(element, size, position) {
  if (!element) return;
  const s = `${size}px`;
  const p = `${position}px`;
  element.style.setProperty('--navius-navigation-menu-indicator-size', s);
  element.style.setProperty('--navius-navigation-menu-indicator-position', p);
  element.style.setProperty('--navius-navigationmenu-indicator-size', s);
  element.style.setProperty('--navius-navigationmenu-indicator-position', p);
}

// Focus the first focusable descendant of `element` (APG keyboard-open of a
// NavigationMenu panel), falling back to the element itself.
export function focusFirstDescendant(element) {
  if (!element) return;
  const focusable = element.querySelector(FOCUSABLE_SELECTOR);
  (focusable || element).focus();
}

// Whether focus may safely be restored to a trigger when `element` closes:
// true when focus is still inside `element`, or nothing meaningful holds focus
// (document.body / null) — false when another widget has already grabbed focus
// (switching menubar menus, opening a dialog from a menu item), so a deferred
// close-refocus never steals focus back from where it legitimately moved.
export function isFocusRestorable(element) {
  const active = document.activeElement;
  if (!active || active === document.body) return true;
  return element === active || (!!element && element.contains(active));
}

// Scroll the element with id `id` into view (Command active-item follow).
export function scrollIntoViewById(id, block) {
  const el = document.getElementById(id);
  if (el) el.scrollIntoView({ block: block || 'nearest' });
}

// --- Small DOM readers -------------------------------------------------------
// Read an element's trimmed text. Lets Select auto-register an item's label and
// Toast announce a toast's rendered text — things C# can't observe post-render.
export function getTextContent(element) {
  return element ? (element.textContent || '').trim() : '';
}

// True once an <img> has finished loading successfully. Covers cached / prerendered
// images whose load event fired before Blazor attached its @onload handler (Avatar).
export function isImageComplete(element) {
  return !!(element && element.complete && element.naturalWidth > 0);
}

// --- Field state + constraint validation (Field) -----------------------------
// Mirror a native input's ValidityState AND its interaction state (focused /
// touched / dirty / filled) to .NET (OnFieldStateChange) on input, change,
// invalid, focusin/focusout, and the closest form's submit. C# turns this into
// Base UI's discrete field-state attributes (data-focused/touched/dirty/filled/
// valid/invalid) and the per-rule error model without re-implementing the
// browser's validity rules. `touched` latches once the control is blurred.
export function createConstraintValidation(input, dotNetRef) {
  const initialValue = input.value;
  let focused = false;
  let touched = false;

  const snapshot = () => {
    const v = input.validity;
    return {
      valueMissing: v.valueMissing,
      typeMismatch: v.typeMismatch,
      patternMismatch: v.patternMismatch,
      tooLong: v.tooLong,
      tooShort: v.tooShort,
      rangeUnderflow: v.rangeUnderflow,
      rangeOverflow: v.rangeOverflow,
      stepMismatch: v.stepMismatch,
      badInput: v.badInput,
      customError: v.customError,
      valid: v.valid,
      validationMessage: input.validationMessage,
      value: input.value, // the live DOM value, for custom MatchFn / FormData parity
      focused,
      touched,
      dirty: input.value !== initialValue,
      filled: input.value !== '',
    };
  };

  const report = () => dotNetRef.invokeMethodAsync('OnFieldStateChange', snapshot());

  const onInvalid = (e) => {
    // Suppress the native bubble; C# renders the message.
    e.preventDefault();
    report();
  };
  const onFocusIn = () => { focused = true; report(); };
  const onFocusOut = () => { focused = false; touched = true; report(); };

  input.addEventListener('input', report);
  input.addEventListener('change', report);
  input.addEventListener('invalid', onInvalid);
  input.addEventListener('focusin', onFocusIn);
  input.addEventListener('focusout', onFocusOut);

  const form = input.closest('form');
  const onSubmit = () => report();
  if (form) form.addEventListener('submit', onSubmit, true);

  // Initial snapshot so a default value surfaces as filled (and the field's
  // valid/disabled baseline is established) before any interaction.
  report();

  return {
    destroy() {
      input.removeEventListener('input', report);
      input.removeEventListener('change', report);
      input.removeEventListener('invalid', onInvalid);
      input.removeEventListener('focusin', onFocusIn);
      input.removeEventListener('focusout', onFocusOut);
      if (form) form.removeEventListener('submit', onSubmit, true);
    },
  };
}

// --- Form submit/reset helpers (OTP / PasswordToggleField) -------------------
// Programmatically submit the form an element belongs to (fires validation +
// submit handlers, unlike form.submit()).
export function submitClosestForm(element) {
  const form = element.closest('form');
  if (form && typeof form.requestSubmit === 'function') {
    form.requestSubmit();
  }
}

// Notify .NET when the input's closest form is submitted or reset, so the
// component can clear/restore its internal value (e.g. an OTP's hidden field).
export function createFormResetSubmitListener(input, dotNetRef) {
  const form = input.closest('form');
  const onSubmit = () => dotNetRef.invokeMethodAsync('OnFormSubmit');
  const onReset = () => dotNetRef.invokeMethodAsync('OnFormReset');
  if (form) {
    form.addEventListener('submit', onSubmit);
    form.addEventListener('reset', onReset);
  }
  return {
    destroy() {
      if (form) {
        form.removeEventListener('submit', onSubmit);
        form.removeEventListener('reset', onReset);
      }
    },
  };
}

// --- Long press (ContextMenu touch) ------------------------------------------
// Fire OnLongPress {x, y} after a press-and-hold with no significant movement.
// Cancelled if the pointer moves past a threshold or lifts early. Used to open a
// context menu from touch, and (with getRect) to anchor it at the press point.
export function createLongPress(element, dotNetRef, options) {
  const opts = Object.assign({ duration: 700, moveThreshold: 10 }, options || {});
  let timer = null;
  let startX = 0;
  let startY = 0;

  const cancel = () => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
  };

  function onPointerDown(e) {
    startX = e.clientX;
    startY = e.clientY;
    cancel();
    timer = setTimeout(() => {
      timer = null;
      dotNetRef.invokeMethodAsync('OnLongPress', { x: e.clientX, y: e.clientY });
    }, opts.duration);
  }
  function onPointerMove(e) {
    if (timer === null) return;
    const dx = Math.abs(e.clientX - startX);
    const dy = Math.abs(e.clientY - startY);
    if (dx > opts.moveThreshold || dy > opts.moveThreshold) cancel();
  }

  element.addEventListener('pointerdown', onPointerDown);
  element.addEventListener('pointermove', onPointerMove);
  element.addEventListener('pointerup', cancel);
  element.addEventListener('pointercancel', cancel);
  element.addEventListener('pointerleave', cancel);

  return {
    destroy() {
      cancel();
      element.removeEventListener('pointerdown', onPointerDown);
      element.removeEventListener('pointermove', onPointerMove);
      element.removeEventListener('pointerup', cancel);
      element.removeEventListener('pointercancel', cancel);
      element.removeEventListener('pointerleave', cancel);
    },
  };
}

// Return an element's bounding rect as a plain serializable object. Used for
// keyboard-anchored context menus (anchor to the focused element's rect).
export function getRect(element) {
  const r = element.getBoundingClientRect();
  return {
    x: r.x,
    y: r.y,
    top: r.top,
    right: r.right,
    bottom: r.bottom,
    left: r.left,
    width: r.width,
    height: r.height,
  };
}

// --- Toast interactions ------------------------------------------------------
// Swipe-to-dismiss + pause/resume timers for a toast. Tracks a pointer drag from
// the toast surface, exposes the live offset via data-swipe / CSS vars (the Base UI
// --toast-swipe-movement-* contract), and signals .NET at each phase. Also
// pauses the auto-dismiss timer while the user is hovering/focused or the window
// is blurred, resuming otherwise.
export function createToastInteractions(rootElement, dotNetRef, options) {
  const opts = Object.assign(
    { direction: 'right', threshold: 50 }, // 'right' | 'left' | 'up' | 'down'
    options || {}
  );
  const horizontal = opts.direction === 'left' || opts.direction === 'right';

  let swiping = false;
  let startX = 0;
  let startY = 0;

  // Project a raw delta onto the allowed dismiss direction (no travel backwards).
  const projected = (dx, dy) => {
    switch (opts.direction) {
      case 'right': return Math.max(0, dx);
      case 'left': return Math.min(0, dx);
      case 'down': return Math.max(0, dy);
      case 'up': return Math.min(0, dy);
      default: return 0;
    }
  };

  function setVars(dx, dy) {
    rootElement.style.setProperty('--toast-swipe-movement-x', `${dx}px`);
    rootElement.style.setProperty('--toast-swipe-movement-y', `${dy}px`);
  }
  function clearVars() {
    rootElement.style.removeProperty('--toast-swipe-movement-x');
    rootElement.style.removeProperty('--toast-swipe-movement-y');
  }

  function onPointerDown(e) {
    if (e.button !== 0) return;
    // Don't hijack a press that starts on an interactive control (the toast's
    // Close/Action button, a link, a form field). Capturing the pointer here would
    // swallow that control's click. Swipe still engages from the toast body.
    if (
      e.target instanceof Element &&
      e.target.closest('button, a, input, select, textarea, label, [role="button"]')
    ) {
      return;
    }
    swiping = true;
    startX = e.clientX;
    startY = e.clientY;
    try { rootElement.setPointerCapture(e.pointerId); } catch {}
    rootElement.setAttribute('data-swipe', 'start');
    rootElement.setAttribute('data-swipe-direction', opts.direction);
    dotNetRef.invokeMethodAsync('OnSwipeStart');
  }
  function onPointerMove(e) {
    if (!swiping) return;
    const rawX = e.clientX - startX;
    const rawY = e.clientY - startY;
    const dx = horizontal ? projected(rawX, rawY) : 0;
    const dy = horizontal ? 0 : projected(rawX, rawY);
    rootElement.setAttribute('data-swipe', 'move');
    setVars(dx, dy);
    dotNetRef.invokeMethodAsync('OnSwipeMove', { x: dx, y: dy });
  }
  function onPointerUp(e) {
    if (!swiping) return;
    swiping = false;
    try { rootElement.releasePointerCapture(e.pointerId); } catch {}
    const rawX = e.clientX - startX;
    const rawY = e.clientY - startY;
    const amount = Math.abs(horizontal ? projected(rawX, rawY) : projected(rawX, rawY));
    if (amount >= opts.threshold) {
      rootElement.setAttribute('data-swipe', 'end');
      dotNetRef.invokeMethodAsync('OnSwipeEnd', { x: rawX, y: rawY });
      dotNetRef.invokeMethodAsync('OnSwipeDismiss');
    } else {
      rootElement.setAttribute('data-swipe', 'cancel');
      clearVars();
      dotNetRef.invokeMethodAsync('OnSwipeCancel');
    }
  }

  // Pause/resume the dismiss timer.
  const pause = () => dotNetRef.invokeMethodAsync('OnPause');
  const resume = () => dotNetRef.invokeMethodAsync('OnResume');

  rootElement.addEventListener('pointerdown', onPointerDown);
  rootElement.addEventListener('pointermove', onPointerMove);
  rootElement.addEventListener('pointerup', onPointerUp);
  rootElement.addEventListener('pointercancel', onPointerUp);
  rootElement.addEventListener('pointerenter', pause);
  rootElement.addEventListener('pointerleave', resume);
  rootElement.addEventListener('focusin', pause);
  rootElement.addEventListener('focusout', resume);
  window.addEventListener('blur', pause);
  window.addEventListener('focus', resume);

  return {
    destroy() {
      rootElement.removeEventListener('pointerdown', onPointerDown);
      rootElement.removeEventListener('pointermove', onPointerMove);
      rootElement.removeEventListener('pointerup', onPointerUp);
      rootElement.removeEventListener('pointercancel', onPointerUp);
      rootElement.removeEventListener('pointerenter', pause);
      rootElement.removeEventListener('pointerleave', resume);
      rootElement.removeEventListener('focusin', pause);
      rootElement.removeEventListener('focusout', resume);
      window.removeEventListener('blur', pause);
      window.removeEventListener('focus', resume);
    },
  };
}

// Global hotkey that focuses the toast region (the spec default: F8). Invokes
// OnHotkey on .NET; C# moves focus into the viewport.
export function createToastHotkey(dotNetRef, keys) {
  const combo = Array.isArray(keys) && keys.length ? keys : ['F8'];
  function onKeyDown(e) {
    if (combo.includes(e.key)) {
      dotNetRef.invokeMethodAsync('OnHotkey');
    }
  }
  document.addEventListener('keydown', onKeyDown);
  return {
    destroy() {
      document.removeEventListener('keydown', onKeyDown);
    },
  };
}

// --- Carousel (Embla role) ---------------------------------------------------
// A dependency-free slide carousel. `viewport` is the overflow-clipping element;
// its first element child is the track that holds the slides. Pointer/touch drag
// translates the track (transform), and on release the engine snaps to the
// nearest slide — biased one snap further when the drag passed a distance or
// velocity threshold (the flick gesture). Snap points are grouped by
// `slidesToScroll` and aligned start/center/end. Loop wraps at the ends.
//
// Calls back into .NET (all swallowed if the consumer hasn't declared them):
//   OnSelect(index)                   — the selected snap changed
//   OnSettle()                        — a snap animation finished
//   OnCanScrollChange(canPrev, canNext)
//
// Keyboard + autoplay are owned by C#; this engine is pointer + geometry only.
// Returns a handle: scrollNext/scrollPrev/scrollTo(index)/canScrollPrev/
// canScrollNext/reInit/destroy.
export function createCarousel(viewport, dotNetRef, options) {
  const opts = Object.assign(
    { orientation: 'horizontal', loop: false, align: 'start', slidesToScroll: 1, duration: 350 },
    options || {}
  );
  const vertical = opts.orientation === 'vertical';
  const step = Math.max(1, opts.slidesToScroll | 0);
  const track = viewport.firstElementChild;

  // No track → an inert handle so callers don't need to null-check.
  if (!track) {
    return {
      scrollNext() {}, scrollPrev() {}, scrollTo() {},
      canScrollPrev() { return false; }, canScrollNext() { return false; },
      reInit() {}, destroy() {},
    };
  }

  const DRAG_THRESHOLD = 30; // px past which a slow drag still advances a slide
  const VELOCITY_THRESHOLD = 0.4; // px/ms past which a flick advances a slide

  let slides = [];
  let targets = [0]; // translate value (negative px) for each snap
  let selectedIndex = 0;
  let currentTranslate = 0;

  const notify = (method, ...args) =>
    Promise.resolve(dotNetRef.invokeMethodAsync(method, ...args)).catch(() => {});

  const viewportSize = () => (vertical ? viewport.clientHeight : viewport.clientWidth);
  // offsetTop/offsetLeft are layout-based and ignore the track's transform, so
  // they stay correct while we're mid-drag.
  const slideStart = (el) => (vertical ? el.offsetTop : el.offsetLeft);
  const slideSize = (el) => (vertical ? el.offsetHeight : el.offsetWidth);

  function measure() {
    slides = Array.from(track.children);
    if (!slides.length) {
      targets = [0];
      return;
    }
    const base = slideStart(slides[0]);
    const vs = viewportSize();
    const last = slides[slides.length - 1];
    const contentSize = slideStart(last) - base + slideSize(last);
    const maxScroll = Math.max(0, contentSize - vs);

    targets = [];
    for (let i = 0; i < slides.length; i += step) {
      const el = slides[i];
      const start = slideStart(el) - base;
      let align = 0;
      if (opts.align === 'center') align = (vs - slideSize(el)) / 2;
      else if (opts.align === 'end') align = vs - slideSize(el);
      let t = -(start - align);
      // Without loop, keep snaps within the scrollable extent so edge slides sit
      // flush against the viewport edge (the trim-snaps behaviour).
      if (!opts.loop) t = Math.min(0, Math.max(-maxScroll, t));
      targets.push(t);
    }
    if (!targets.length) targets = [0];
  }

  const snapCount = () => targets.length;
  const clampIndex = (i) => {
    const n = snapCount();
    if (opts.loop) return ((i % n) + n) % n;
    return Math.max(0, Math.min(n - 1, i));
  };
  const canPrev = () => (opts.loop ? snapCount() > 1 : selectedIndex > 0);
  const canNext = () => (opts.loop ? snapCount() > 1 : selectedIndex < snapCount() - 1);

  function setTransform(v, animate) {
    currentTranslate = v;
    track.style.transition = animate
      ? `transform ${opts.duration}ms cubic-bezier(0.16, 1, 0.3, 1)`
      : 'none';
    track.style.transform = vertical
      ? `translate3d(0, ${v}px, 0)`
      : `translate3d(${v}px, 0, 0)`;
  }

  function publishVars() {
    viewport.style.setProperty('--navius-carousel-selected-index', String(selectedIndex));
    viewport.style.setProperty('--navius-carousel-snap-count', String(snapCount()));
  }

  function select(index, animate) {
    const next = clampIndex(index);
    const changed = next !== selectedIndex;
    selectedIndex = next;
    setTransform(targets[selectedIndex] || 0, animate);
    publishVars();
    if (changed) notify('OnSelect', selectedIndex);
    notify('OnCanScrollChange', canPrev(), canNext());
  }

  function closestSnap(t) {
    let best = 0;
    let bestDist = Infinity;
    for (let i = 0; i < targets.length; i++) {
      const d = Math.abs(targets[i] - t);
      if (d < bestDist) {
        bestDist = d;
        best = i;
      }
    }
    return best;
  }

  // --- Drag ---
  let dragging = false;
  let startPos = 0;
  let startTranslate = 0;
  let lastPos = 0;
  let lastTime = 0;
  let velocity = 0;

  const eventPos = (e) => (vertical ? e.clientY : e.clientX);

  // Rubber-band resistance past the ends when loop is off.
  function rubber(t) {
    if (opts.loop) return t;
    const maxT = targets[0];
    const minT = targets[targets.length - 1];
    if (t > maxT) return maxT + (t - maxT) * 0.35;
    if (t < minT) return minT + (t - minT) * 0.35;
    return t;
  }

  function onPointerDown(e) {
    if (e.button !== 0) return;
    dragging = true;
    startPos = lastPos = eventPos(e);
    startTranslate = currentTranslate;
    lastTime = e.timeStamp || performance.now();
    velocity = 0;
    try { viewport.setPointerCapture(e.pointerId); } catch {}
    track.style.transition = 'none';
  }

  function onPointerMove(e) {
    if (!dragging) return;
    const pos = eventPos(e);
    const now = e.timeStamp || performance.now();
    const dt = now - lastTime;
    if (dt > 0) velocity = (pos - lastPos) / dt;
    lastPos = pos;
    lastTime = now;
    const delta = pos - startPos;
    setTransform(rubber(startTranslate + delta), false);
    viewport.style.setProperty('--navius-carousel-drag-offset', `${delta}px`);
    e.preventDefault();
  }

  function onPointerUp(e) {
    if (!dragging) return;
    dragging = false;
    try { viewport.releasePointerCapture(e.pointerId); } catch {}
    viewport.style.removeProperty('--navius-carousel-drag-offset');
    const delta = eventPos(e) - startPos;
    const flicked = Math.abs(velocity) > VELOCITY_THRESHOLD;
    const dragged = Math.abs(delta) > DRAG_THRESHOLD;
    let targetIndex;
    if (flicked || dragged) {
      // A leftward/upward drag (negative delta) advances to the next slide.
      const dir = delta < 0 ? 1 : -1;
      targetIndex = selectedIndex + dir;
      if (!opts.loop && (targetIndex < 0 || targetIndex >= snapCount())) {
        targetIndex = closestSnap(currentTranslate);
      }
    } else {
      targetIndex = closestSnap(currentTranslate);
    }
    select(targetIndex, true);
  }

  function onTransitionEnd(e) {
    if (e.target !== track || e.propertyName !== 'transform') return;
    notify('OnSettle');
  }

  function reInit() {
    measure();
    selectedIndex = clampIndex(selectedIndex);
    setTransform(targets[selectedIndex] || 0, false);
    publishVars();
    notify('OnCanScrollChange', canPrev(), canNext());
  }

  viewport.addEventListener('pointerdown', onPointerDown);
  viewport.addEventListener('pointermove', onPointerMove);
  viewport.addEventListener('pointerup', onPointerUp);
  viewport.addEventListener('pointercancel', onPointerUp);
  track.addEventListener('transitionend', onTransitionEnd);

  let ro = null;
  if (typeof ResizeObserver !== 'undefined') {
    let first = true;
    ro = new ResizeObserver(() => {
      // The first callback fires synchronously on observe — we've already measured.
      if (first) { first = false; return; }
      reInit();
    });
    ro.observe(viewport);
  }

  measure();
  setTransform(targets[0] || 0, false);
  publishVars();
  notify('OnCanScrollChange', canPrev(), canNext());

  return {
    scrollNext() { select(selectedIndex + 1, true); },
    scrollPrev() { select(selectedIndex - 1, true); },
    scrollTo(index) { select(index, true); },
    canScrollPrev() { return canPrev(); },
    canScrollNext() { return canNext(); },
    reInit() { reInit(); },
    destroy() {
      viewport.removeEventListener('pointerdown', onPointerDown);
      viewport.removeEventListener('pointermove', onPointerMove);
      viewport.removeEventListener('pointerup', onPointerUp);
      viewport.removeEventListener('pointercancel', onPointerUp);
      track.removeEventListener('transitionend', onTransitionEnd);
      if (ro) ro.disconnect();
    },
  };
}

// --- Sheet swipe (Drawer / Vaul role) ----------------------------------------
// Drag-to-dismiss for a side sheet. A pointer drag on `content` translates the
// sheet along its side axis (only in the dismiss direction — no travel backward),
// publishing the live offset on `--navius-sheet-drag-offset` and a matching
// transform. On release the engine compares the dragged distance against
// `dismissThreshold` (a fraction of the sheet's size on that axis): past it fires
// OnDismiss (leaving the offset in place so C#'s close animation continues from
// there), otherwise it snaps back and fires OnReset. A drag that never moved is
// treated as a tap and ignored so inner clicks still work. Mirrors
// createToastInteractions' pointer-capture + threshold model. For Drawer.
export function createSheetSwipe(content, dotNetRef, options) {
  const opts = Object.assign({ side: 'bottom', dismissThreshold: 0.25 }, options || {});
  const side = opts.side;
  const horizontal = side === 'left' || side === 'right';

  let dragging = false;
  let moved = false;
  let startX = 0;
  let startY = 0;

  const notify = (method, ...args) =>
    Promise.resolve(dotNetRef.invokeMethodAsync(method, ...args)).catch(() => {});

  // Project the raw delta onto the dismiss direction, clamped to >= 0.
  function projected(dx, dy) {
    switch (side) {
      case 'bottom': return Math.max(0, dy);
      case 'top': return Math.max(0, -dy);
      case 'right': return Math.max(0, dx);
      case 'left': return Math.max(0, -dx);
      default: return 0;
    }
  }

  function setOffset(amount) {
    content.style.setProperty('--navius-sheet-drag-offset', `${amount}px`);
    let tx = 0;
    let ty = 0;
    if (side === 'bottom') ty = amount;
    else if (side === 'top') ty = -amount;
    else if (side === 'right') tx = amount;
    else if (side === 'left') tx = -amount;
    // Base UI drawer-swipe vars (dual-emitted alongside the legacy offset var).
    content.style.setProperty('--drawer-swipe-movement-x', `${tx}px`);
    content.style.setProperty('--drawer-swipe-movement-y', `${ty}px`);
    content.style.transform = `translate3d(${tx}px, ${ty}px, 0)`;
  }

  function clearOffset() {
    content.style.removeProperty('--navius-sheet-drag-offset');
    content.style.removeProperty('--drawer-swipe-movement-x');
    content.style.removeProperty('--drawer-swipe-movement-y');
    content.style.transform = '';
  }

  function onPointerDown(e) {
    if (e.button !== 0) return;
    dragging = true;
    moved = false;
    startX = e.clientX;
    startY = e.clientY;
    try { content.setPointerCapture(e.pointerId); } catch {}
    content.style.transition = 'none';
  }

  function onPointerMove(e) {
    if (!dragging) return;
    const amount = projected(e.clientX - startX, e.clientY - startY);
    if (amount > 0) {
      if (!moved) content.setAttribute('data-swiping', ''); // Base UI: data-swiping during a drag
      moved = true;
      setOffset(amount);
      e.preventDefault();
    } else if (moved) {
      setOffset(0);
    }
  }

  function onPointerUp(e) {
    if (!dragging) return;
    dragging = false;
    try { content.releasePointerCapture(e.pointerId); } catch {}
    content.removeAttribute('data-swiping');
    if (!moved) {
      clearOffset();
      return; // a tap, not a drag
    }
    const amount = projected(e.clientX - startX, e.clientY - startY);
    const size = horizontal ? content.offsetWidth : content.offsetHeight;
    const passed = size > 0 && amount / size >= opts.dismissThreshold;
    if (passed) {
      notify('OnDismiss'); // leave the offset so the exit animation continues
    } else {
      content.style.transition = ''; // restore the stylesheet transition to ease back
      clearOffset();
      notify('OnReset');
    }
  }

  content.addEventListener('pointerdown', onPointerDown);
  content.addEventListener('pointermove', onPointerMove);
  content.addEventListener('pointerup', onPointerUp);
  content.addEventListener('pointercancel', onPointerUp);

  return {
    destroy() {
      content.removeEventListener('pointerdown', onPointerDown);
      content.removeEventListener('pointermove', onPointerMove);
      content.removeEventListener('pointerup', onPointerUp);
      content.removeEventListener('pointercancel', onPointerUp);
    },
  };
}

// --- Message scroller ----------------------------------------------------------
// Conversation-transcript scroll manager (the shadcn MessageScroller role). The
// guiding rule: never move the reader against their intent. `viewport` is the
// scroll container; the engine locates the root frame via the
// data-navius-messagescroller ancestor and the transcript container via the
// data-navius-messagescroller-content descendant (whose direct children are the
// data-navius-messagescroller-item rows, plus a trailing
// data-navius-messagescroller-spacer element the engine sizes to make room for
// anchored turns).
//
// Behaviours:
//   - Anchored turns: when a row marked data-scroll-anchor="true" is appended
//     while the reader is at the live edge, the viewport scrolls it near the top
//     (scrollMargin + scrollPreviousItemPeek below the edge, so part of the
//     previous item stays visible) and grows the spacer so the position is
//     reachable while the reply streams in below.
//   - Streamed-reply follow: with autoScroll enabled, the viewport sticks to the
//     live edge only while the reader is already there. Wheel, touch, keyboard
//     scrolling, scrollbar drags and explicit jumps release the follow; pressing
//     the scroll button or calling scrollToEnd re-engages it.
//   - Prepend preservation: when older rows are prepended, the first visible row
//     (keyed on its stable data-message-id) keeps its on-screen position.
//   - Edge state: data-scrollable="start"/"end"/"start end" is mirrored on the
//     root and the viewport (scrollEdgeThreshold px from an edge still counts as
//     being at it); data-autoscrolling is present while the engine is
//     programmatically scrolling toward the latest message.
//   - Visibility tracking (subscription-gated, costs nothing until enabled via
//     setVisibilityTracking): reports the current anchor id (the last anchor row
//     at or above the reading line) and the message ids intersecting the
//     viewport, in document order.
//
// Calls back into .NET (all swallowed if the consumer hasn't declared them):
//   OnScrollableChange(start, end)          - the scrollable edges changed
//   OnVisibilityChange(currentAnchorId, ids) - only while visibility tracking runs
//
// Returns a handle: scrollToMessage(id, opts)/scrollToStart(opts)/scrollToEnd(opts)
// (opts: { align, behavior, scrollMargin }), update(options),
// setVisibilityTracking(enabled), destroy(). scrollToMessage queues its target
// when called before any rows exist (client-resolved permalinks) and returns
// false only for an id that is missing from a mounted transcript.
export function createMessageScroller(viewport, dotNetRef, options) {
  const opts = Object.assign(
    {
      autoScroll: false,
      defaultScrollPosition: 'end',
      scrollEdgeThreshold: 8,
      scrollMargin: 0,
      scrollPreviousItemPeek: 64,
      preserveScrollOnPrepend: true,
    },
    options || {}
  );

  const root = viewport.closest('[data-navius-messagescroller]');
  const content = viewport.querySelector('[data-navius-messagescroller-content]');

  // No transcript container: an inert handle so callers don't need to null-check.
  if (!content) {
    return {
      scrollToMessage() { return false; }, scrollToStart() { return false; },
      scrollToEnd() { return false; }, update() {}, setVisibilityTracking() {}, destroy() {},
    };
  }

  const spacer = content.querySelector(':scope > [data-navius-messagescroller-spacer]');
  const ITEM_SELECTOR = ':scope > [data-navius-messagescroller-item]';

  const notify = (method, ...args) =>
    Promise.resolve(dotNetRef.invokeMethodAsync(method, ...args)).catch(() => {});

  let itemList = [];
  let following = false;
  let initialApplied = false;
  let pendingTarget = null; // { id, options } queued before any rows exist
  let anchorEl = null; // the row the spacer keeps reachable near the top
  let refEl = null; // reading-position reference (first visible row)
  let refId = null;
  let refOffset = 0;
  let nearEnd = true; // pre-mutation "at the live edge" snapshot
  let programmaticTarget = null;
  let programmaticTimer = 0;
  let autoscrollAttrTimer = 0;
  let suppressUserScrollUntil = 0; // layout-induced scroll events are not intent
  let lastSpacerHeight = 0;
  let lastScrollableKey = null;
  let trackVisibility = false;
  let io = null;
  let visibleSet = new Set();
  let visibilityRaf = 0;
  let lastVisibilityKey = '';
  let roRaf = 0;

  // The engine owns position preservation, so the browser's own scroll anchoring
  // must not double-compensate.
  const prevOverflowAnchor = viewport.style.overflowAnchor;
  viewport.style.overflowAnchor = 'none';

  const refreshItems = () => { itemList = Array.from(content.querySelectorAll(ITEM_SELECTOR)); };
  const maxScroll = () => Math.max(0, viewport.scrollHeight - viewport.clientHeight);
  const atEnd = () => maxScroll() - viewport.scrollTop <= opts.scrollEdgeThreshold;
  const clampTop = (t) => Math.max(0, Math.min(t, maxScroll()));
  const messageId = (el) => el.getAttribute('data-message-id');
  const isAnchor = (el) => el.getAttribute('data-scroll-anchor') === 'true';
  const anchorMargin = () => opts.scrollMargin + opts.scrollPreviousItemPeek;
  const suppress = (ms) => { suppressUserScrollUntil = performance.now() + ms; };

  // An element's top edge in the viewport's scroll space (what scrollTop must be
  // for the edge to sit at the visible top).
  function scrollSpaceTop(el) {
    const vRect = viewport.getBoundingClientRect();
    return el.getBoundingClientRect().top - vRect.top - viewport.clientTop + viewport.scrollTop;
  }

  function targetFor(el, align, margin) {
    const top = scrollSpaceTop(el);
    const height = el.getBoundingClientRect().height;
    const ch = viewport.clientHeight;
    if (align === 'end') return clampTop(top + height - ch + margin);
    if (align === 'center') return clampTop(top + height / 2 - ch / 2);
    if (align === 'nearest') {
      const cur = viewport.scrollTop;
      if (top >= cur + margin && top + height <= cur + ch - margin) return cur; // already in view
      return top < cur + margin ? clampTop(top - margin) : clampTop(top + height - ch + margin);
    }
    return clampTop(top - margin);
  }

  function normalizeScrollOptions(o) {
    return {
      align: o && o.align != null ? o.align : 'start',
      behavior: o && o.behavior != null ? o.behavior : 'auto',
      margin: o && o.scrollMargin != null ? o.scrollMargin : opts.scrollMargin,
    };
  }

  function setAutoscrollingAttr(on) {
    for (const el of [root, viewport]) {
      if (!el) continue;
      if (on) el.setAttribute('data-autoscrolling', '');
      else el.removeAttribute('data-autoscrolling');
    }
  }

  // toLatest scrolls carry the data-autoscrolling attribute (they move toward the
  // newest message); the attribute lingers briefly so per-chunk follow jumps read
  // as one continuous autoscroll.
  function programmaticScroll(top, behavior, toLatest) {
    const t = clampTop(top);
    if (toLatest) {
      setAutoscrollingAttr(true);
      clearTimeout(autoscrollAttrTimer);
      autoscrollAttrTimer = setTimeout(() => { if (programmaticTarget == null) setAutoscrollingAttr(false); }, 200);
    }
    if (Math.abs(viewport.scrollTop - t) < 0.5) { updateEdgeState(); return; }
    programmaticTarget = t;
    renewProgrammaticTimer();
    suppress(80);
    viewport.scrollTo({ top: t, behavior: behavior === 'smooth' ? 'smooth' : 'auto' });
  }

  function renewProgrammaticTimer() {
    clearTimeout(programmaticTimer);
    programmaticTimer = setTimeout(settleProgrammatic, 300);
  }

  function settleProgrammatic() {
    programmaticTarget = null;
    clearTimeout(programmaticTimer);
    // Let data-autoscrolling linger briefly so per-chunk follow jumps read as one
    // continuous autoscroll instead of flickering between chunks.
    clearTimeout(autoscrollAttrTimer);
    autoscrollAttrTimer = setTimeout(() => setAutoscrollingAttr(false), 120);
    captureRef();
    updateEdgeState();
  }

  function captureRef() {
    if (!itemList.length) { refEl = null; refId = null; return; }
    const vTop = viewport.getBoundingClientRect().top + viewport.clientTop;
    // Binary search for the first row whose bottom edge is below the viewport top.
    let lo = 0;
    let hi = itemList.length - 1;
    let found = itemList.length - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >> 1;
      if (itemList[mid].getBoundingClientRect().bottom > vTop) { found = mid; hi = mid - 1; }
      else lo = mid + 1;
    }
    refEl = itemList[found];
    refId = messageId(refEl);
    refOffset = refEl.getBoundingClientRect().top - vTop;
  }

  function restoreRef() {
    let el = refEl && refEl.isConnected ? refEl : null;
    if (!el && refId != null) el = itemList.find((e) => messageId(e) === refId) || null;
    if (!el) return;
    const vTop = viewport.getBoundingClientRect().top + viewport.clientTop;
    const delta = (el.getBoundingClientRect().top - vTop) - refOffset;
    if (Math.abs(delta) < 0.5) return;
    suppress(80);
    viewport.scrollTop += delta;
  }

  function setSpacerHeight(h) {
    if (!spacer || Math.abs(h - lastSpacerHeight) < 1) return;
    lastSpacerHeight = h;
    suppress(80);
    spacer.style.flexShrink = '0';
    spacer.style.height = h > 0 ? `${h}px` : '';
  }

  // Keep enough room below the current anchor row that its near-the-top position
  // stays reachable; shrinks back to zero as the streamed reply fills the space.
  function updateSpacer() {
    if (!spacer) return;
    let h = 0;
    if (anchorEl && anchorEl.isConnected) {
      const target = Math.max(0, scrollSpaceTop(anchorEl) - anchorMargin());
      const spacerTop = scrollSpaceTop(spacer);
      h = Math.max(0, target + viewport.clientHeight - spacerTop);
    }
    setSpacerHeight(h);
  }

  function updateEdgeState() {
    const start = viewport.scrollTop > opts.scrollEdgeThreshold;
    const end = maxScroll() - viewport.scrollTop > opts.scrollEdgeThreshold;
    nearEnd = !end;
    const value = start && end ? 'start end' : start ? 'start' : end ? 'end' : null;
    for (const el of [root, viewport]) {
      if (!el) continue;
      if (value) el.setAttribute('data-scrollable', value);
      else el.removeAttribute('data-scrollable');
    }
    const key = `${start}|${end}`;
    if (key !== lastScrollableKey) {
      lastScrollableKey = key;
      notify('OnScrollableChange', start, end);
    }
  }

  function positionAnchor(el, behavior) {
    anchorEl = el;
    updateSpacer();
    // An anchored turn is a deliberate reading position, not the live edge, so
    // the streamed reply grows into the screen below instead of being followed.
    following = false;
    programmaticScroll(targetFor(el, 'start', anchorMargin()), behavior, true);
  }

  function stickToEnd() {
    programmaticScroll(maxScroll(), 'auto', true);
  }

  function applyInitialPosition() {
    initialApplied = true;
    if (pendingTarget) {
      const el = itemList.find((e) => messageId(e) === pendingTarget.id);
      if (el) {
        const t = pendingTarget;
        pendingTarget = null;
        scrollToMessage(t.id, t.options);
        return;
      }
      // Keep the target queued in case the row arrives with a later batch.
    }
    if (opts.defaultScrollPosition === 'start') { following = false; return; }
    if (opts.defaultScrollPosition === 'last-anchor') {
      const anchors = itemList.filter(isAnchor);
      const el = anchors[anchors.length - 1];
      if (el) {
        // Clamping to maxScroll is the documented fallback: when the last turn
        // fits in the viewport this lands at the end.
        following = false;
        suppress(80);
        viewport.scrollTop = targetFor(el, 'start', anchorMargin());
        return;
      }
    }
    suppress(80);
    viewport.scrollTop = maxScroll();
    following = opts.autoScroll;
  }

  function resolvePendingTarget() {
    if (!pendingTarget) return;
    const el = itemList.find((e) => messageId(e) === pendingTarget.id);
    if (!el) return;
    const t = pendingTarget;
    pendingTarget = null;
    scrollToMessage(t.id, t.options);
  }

  // --- reader-intent listeners -------------------------------------------------
  function onUserIntent() {
    if (programmaticTarget != null) {
      // The reader interrupts a programmatic scroll: stop where we are.
      viewport.scrollTo({ top: viewport.scrollTop, behavior: 'auto' });
      settleProgrammatic();
    }
    following = opts.autoScroll && atEnd();
  }

  const SCROLL_KEYS = [' ', 'PageUp', 'PageDown', 'Home', 'End', 'ArrowUp', 'ArrowDown'];
  const onWheel = () => onUserIntent();
  const onTouchMove = () => onUserIntent();
  const onKeyDown = (e) => { if (SCROLL_KEYS.includes(e.key)) onUserIntent(); };
  // A pointerdown whose target is the viewport itself hits its own box (the
  // scrollbar or padding), so a scrollbar drag counts as intent too.
  const onPointerDown = (e) => { if (e.target === viewport) onUserIntent(); };

  function onScroll() {
    if (programmaticTarget != null) {
      if (Math.abs(viewport.scrollTop - programmaticTarget) <= 1) settleProgrammatic();
      else renewProgrammaticTimer();
      updateEdgeState();
    } else {
      const intent = performance.now() >= suppressUserScrollUntil;
      if (intent) following = opts.autoScroll && atEnd();
      captureRef();
      updateEdgeState();
    }
    if (trackVisibility) scheduleVisibility();
  }

  function onResize() {
    updateSpacer();
    if (following) stickToEnd();
    else if (programmaticTarget == null) restoreRef();
    captureRef();
    updateEdgeState();
    if (trackVisibility) scheduleVisibility();
  }

  const mo = new MutationObserver(() => {
    const prevFirst = itemList[0] || null;
    const prevLast = itemList[itemList.length - 1] || null;
    const hadItems = itemList.length > 0;
    const wasNearEnd = nearEnd;
    refreshItems();
    if (trackVisibility) observeAllItems();
    if (!itemList.length) {
      anchorEl = null;
      setSpacerHeight(0);
      refEl = null;
      refId = null;
      updateEdgeState();
      return;
    }

    if (!initialApplied) {
      applyInitialPosition();
      captureRef();
      updateEdgeState();
      if (trackVisibility) scheduleVisibility();
      return;
    }

    const prepended = hadItems && prevFirst ? itemList.indexOf(prevFirst) > 0 : false;
    const lastIdx = prevLast ? itemList.indexOf(prevLast) : -1;
    const appended = lastIdx >= 0 ? itemList.slice(lastIdx + 1) : (hadItems ? [] : itemList);

    if (prepended && opts.preserveScrollOnPrepend) restoreRef();

    const anchors = appended.filter(isAnchor);
    if (anchors.length && (wasNearEnd || following)) {
      // Only an at-the-live-edge reader gets moved to the new turn; anyone
      // reading history keeps their place and the content arrives offscreen.
      positionAnchor(anchors[anchors.length - 1], 'smooth');
    } else if (following) {
      stickToEnd();
    }

    resolvePendingTarget();
    captureRef();
    updateEdgeState();
    if (trackVisibility) scheduleVisibility();
  });
  mo.observe(content, { childList: true });

  let ro = null;
  if (typeof ResizeObserver !== 'undefined') {
    // rAF-coalesced like createPositioner: one pass per frame while streaming.
    ro = new ResizeObserver(() => {
      if (roRaf) return;
      roRaf = requestAnimationFrame(() => { roRaf = 0; onResize(); });
    });
    ro.observe(content);
    ro.observe(viewport);
  }

  // --- visibility tracking (lazy) ------------------------------------------------
  function observeAllItems() {
    if (!io) return;
    io.disconnect();
    visibleSet.clear();
    for (const el of itemList) io.observe(el);
  }

  function scheduleVisibility() {
    if (!trackVisibility || visibilityRaf) return;
    visibilityRaf = requestAnimationFrame(reportVisibility);
  }

  function reportVisibility() {
    visibilityRaf = 0;
    if (!trackVisibility) return;
    const ids = [];
    for (const el of itemList) {
      if (!visibleSet.has(el)) continue;
      const id = messageId(el);
      if (id != null) ids.push(id);
    }
    // The current anchor is the last anchor row at or above the reading line
    // (one pixel past the anchored-turn resting position); it stays current
    // after scrolling above the viewport.
    const vTop = viewport.getBoundingClientRect().top + viewport.clientTop;
    const line = anchorMargin() + 1;
    let current = null;
    for (const el of itemList) {
      if (!isAnchor(el)) continue;
      if (el.getBoundingClientRect().top - vTop <= line) current = messageId(el);
      else break;
    }
    const key = `${current ?? ''}|${ids.join(',')}`;
    if (key === lastVisibilityKey) return;
    lastVisibilityKey = key;
    notify('OnVisibilityChange', current, ids);
  }

  function setVisibilityTracking(enabled) {
    if (trackVisibility === !!enabled) return;
    trackVisibility = !!enabled;
    if (trackVisibility) {
      if (!io && typeof IntersectionObserver !== 'undefined') {
        io = new IntersectionObserver((entries) => {
          for (const entry of entries) {
            if (entry.isIntersecting) visibleSet.add(entry.target);
            else visibleSet.delete(entry.target);
          }
          scheduleVisibility();
        }, { root: viewport });
      }
      observeAllItems();
      scheduleVisibility();
    } else {
      if (io) io.disconnect();
      visibleSet.clear();
      lastVisibilityKey = '';
      if (visibilityRaf) { cancelAnimationFrame(visibilityRaf); visibilityRaf = 0; }
    }
  }

  // --- imperative API --------------------------------------------------------------
  function scrollToMessage(id, o) {
    const el = itemList.find((e) => messageId(e) === id);
    if (!el) {
      if (!itemList.length) { pendingTarget = { id, options: o || null }; return true; }
      return false;
    }
    const norm = normalizeScrollOptions(o);
    following = false; // an explicit jump releases the follow
    programmaticScroll(targetFor(el, norm.align, norm.margin), norm.behavior, false);
    return true;
  }

  function scrollToStart(o) {
    const norm = normalizeScrollOptions(o);
    following = false;
    programmaticScroll(0, norm.behavior, false);
    return true;
  }

  function scrollToEnd(o) {
    const norm = normalizeScrollOptions(o);
    if (opts.autoScroll) following = true; // the explicit return to the live edge re-engages follow
    programmaticScroll(maxScroll(), norm.behavior, true);
    return true;
  }

  viewport.addEventListener('scroll', onScroll, { passive: true });
  viewport.addEventListener('wheel', onWheel, { passive: true });
  viewport.addEventListener('touchmove', onTouchMove, { passive: true });
  viewport.addEventListener('keydown', onKeyDown);
  viewport.addEventListener('pointerdown', onPointerDown);

  refreshItems();
  if (itemList.length) applyInitialPosition();
  captureRef();
  updateEdgeState();

  return {
    scrollToMessage,
    scrollToStart,
    scrollToEnd,
    update(newOptions) {
      Object.assign(opts, newOptions || {});
      following = opts.autoScroll && atEnd();
      updateSpacer();
      updateEdgeState();
    },
    setVisibilityTracking,
    destroy() {
      mo.disconnect();
      if (ro) ro.disconnect();
      if (io) io.disconnect();
      if (roRaf) cancelAnimationFrame(roRaf);
      if (visibilityRaf) cancelAnimationFrame(visibilityRaf);
      clearTimeout(programmaticTimer);
      clearTimeout(autoscrollAttrTimer);
      viewport.removeEventListener('scroll', onScroll);
      viewport.removeEventListener('wheel', onWheel);
      viewport.removeEventListener('touchmove', onTouchMove);
      viewport.removeEventListener('keydown', onKeyDown);
      viewport.removeEventListener('pointerdown', onPointerDown);
      setAutoscrollingAttr(false);
      for (const el of [root, viewport]) { if (el) el.removeAttribute('data-scrollable'); }
      if (spacer) { spacer.style.height = ''; spacer.style.flexShrink = ''; }
      viewport.style.overflowAnchor = prevOverflowAnchor;
    },
  };
}

// --- 2D pointer tracker (ColorPicker area thumb) -----------------------------
// The 2D sibling of createDragTracker. Translates a pointer position over
// `element` into a normalized { x, y } pair (each clamped 0..1) and streams it to
// .NET (OnFraction2D); on release it fires OnFraction2D's final value once more via
// OnCommit2D (swallowed if the consumer has no [JSInvokable] OnCommit2D). Captures
// the pointer so the drag keeps tracking outside the element. Geometry is
// DOM-natural: x grows to the right, y grows downward (top edge = 0, bottom = 1);
// C# owns the channel mapping (e.g. a saturation/value area reads value = 1 - y).
// x is NOT mirrored under RTL: the only consumer is the ColorPicker, whose saturation
// and hue axes are a physical color surface (not reading-order dependent), and whose
// keyboard handlers never flip either. Mirroring here made pointer and keyboard
// disagree under dir=rtl, so both now agree on right = increasing.
export function createPointerTracker2D(element, dotNetRef) {
  let dragging = false;

  function fractionFromEvent(e) {
    const rect = element.getBoundingClientRect();
    const w = rect.width || 1;
    const h = rect.height || 1;
    const x = Math.max(0, Math.min(1, (e.clientX - rect.left) / w));
    const y = Math.max(0, Math.min(1, (e.clientY - rect.top) / h));
    return { x, y };
  }

  const emit = (e) => dotNetRef.invokeMethodAsync('OnFraction2D', fractionFromEvent(e));

  function onPointerDown(e) {
    if (e.button !== 0) return;
    dragging = true;
    try { element.setPointerCapture(e.pointerId); } catch {}
    e.preventDefault();
    emit(e);
  }
  function onPointerMove(e) { if (dragging) { e.preventDefault(); emit(e); } }
  function onPointerUp(e) {
    if (!dragging) return;
    dragging = false;
    try { element.releasePointerCapture(e.pointerId); } catch {}
    // OnFraction2D has already streamed the latest position; OnCommit2D marks the
    // gesture complete (so C# can fire onValueCommit). Swallowed if undeclared.
    Promise.resolve(dotNetRef.invokeMethodAsync('OnCommit2D', fractionFromEvent(e))).catch(() => {});
  }

  element.addEventListener('pointerdown', onPointerDown);
  element.addEventListener('pointermove', onPointerMove);
  element.addEventListener('pointerup', onPointerUp);
  element.addEventListener('pointercancel', onPointerUp);

  return {
    destroy() {
      element.removeEventListener('pointerdown', onPointerDown);
      element.removeEventListener('pointermove', onPointerMove);
      element.removeEventListener('pointerup', onPointerUp);
      element.removeEventListener('pointercancel', onPointerUp);
    },
  };
}

// --- File dropzone (FileUpload) ----------------------------------------------
// Wires drag/drop on `element` to relay dropped files into a real hidden
// <input type="file"> (`inputElement`) so Blazor's InputFile sees them through its
// own native change handler (the a11y source of truth stays the native input).
// dragenter/over/leave toggle a data-dragging attribute on `element` (drag depth is
// counted so moving across child nodes does not flicker it) and report the state to
// .NET via OnDraggingChange(bool) (swallowed if undeclared). On drop the files are
// copied through a DataTransfer onto `inputElement.files`, then a bubbling change
// event is dispatched so InputFile fires. The returned handle also exposes
// clickToOpen(), which forwards a Trigger/Dropzone activation to inputElement.click()
// to open the native file dialog.
export function createFileDropzone(element, inputElement, dotNetRef) {
  let depth = 0;

  const setDragging = (on) => {
    if (on) element.setAttribute('data-dragging', '');
    else element.removeAttribute('data-dragging');
    if (dotNetRef) {
      Promise.resolve(dotNetRef.invokeMethodAsync('OnDraggingChange', on)).catch(() => {});
    }
  };

  function onDragEnter(e) {
    e.preventDefault();
    depth += 1;
    if (depth === 1) setDragging(true);
  }
  function onDragOver(e) {
    e.preventDefault(); // required so the browser fires a drop event
    if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
  }
  function onDragLeave(e) {
    e.preventDefault();
    depth = Math.max(0, depth - 1);
    if (depth === 0) setDragging(false);
  }
  function onDrop(e) {
    e.preventDefault();
    depth = 0;
    setDragging(false);
    if (!inputElement || !e.dataTransfer) return;
    const files = e.dataTransfer.files;
    if (!files || !files.length) return;
    try {
      const dt = new DataTransfer();
      for (const f of files) dt.items.add(f);
      inputElement.files = dt.files;
      inputElement.dispatchEvent(new Event('change', { bubbles: true }));
    } catch {
      /* DataTransfer / files assignment unsupported; the native picker path still works */
    }
  }

  element.addEventListener('dragenter', onDragEnter);
  element.addEventListener('dragover', onDragOver);
  element.addEventListener('dragleave', onDragLeave);
  element.addEventListener('drop', onDrop);

  return {
    clickToOpen() {
      if (inputElement && typeof inputElement.click === 'function') inputElement.click();
    },
    destroy() {
      element.removeEventListener('dragenter', onDragEnter);
      element.removeEventListener('dragover', onDragOver);
      element.removeEventListener('dragleave', onDragLeave);
      element.removeEventListener('drop', onDrop);
    },
  };
}

// --- Masked selection (MaskedInput / CurrencyInput) --------------------------
// Two atomic, synchronous helpers over a text `input` so the C# caret-stable
// masking pipeline can read and write value + selection in single round trips
// without a caret flicker between them. getState() snapshots the live value and
// selection (falling back to the value length when the browser reports null, e.g.
// on a freshly focused field). setState() assigns the value and calls
// setSelectionRange in one call; pass selectionStart === selectionEnd for a
// collapsed caret, or distinct bounds to restore a range selection (the pipeline
// recomputes Start and End independently). No listeners; destroy() is a no-op kept
// for the uniform factory/dispose idiom.
export function createMaskedSelection(input) {
  return {
    getState() {
      const value = input.value;
      return {
        value,
        selectionStart: input.selectionStart != null ? input.selectionStart : value.length,
        selectionEnd: input.selectionEnd != null ? input.selectionEnd : value.length,
      };
    },
    setState(value, selectionStart, selectionEnd) {
      input.value = value;
      const end = selectionEnd == null ? selectionStart : selectionEnd;
      try {
        input.setSelectionRange(selectionStart, end);
      } catch {
        /* input type does not support text selection */
      }
    },
    destroy() {},
  };
}

// --- Sortable (drag-to-reorder) ----------------------------------------------
// Pointer-driven reorder over the direct element children of `container`. C# owns
// the ordered collection: the engine never mutates the DOM order (Blazor commits
// the reorder on re-render), it only tracks the pointer and reports indices +
// paints style hooks. On press (optionally only when the press starts on a
// `options.handle` selector inside an item) it records the item rects once, sets
// data-dragging on the active item, and emits OnDragStart(index). As the pointer
// moves it computes the target index by comparing the pointer against each item's
// midpoint (options.axis: 'vertical' uses the Y midpoint, 'horizontal' the X
// midpoint, 'grid' the nearest 2D center), mirrors data-drop-target onto the item
// occupying that slot, and emits OnDragOver(fromIndex, toIndex) when it changes. On
// release it emits OnDrop(fromIndex, toIndex) (only when the position actually
// moved) or OnCancel; Escape (or pointercancel / a dropped-in-place release) emits
// OnCancel. All four callbacks are swallowed if undeclared. Keyboard reordering is
// owned by C#. Returns a handle; destroy() detaches every listener.
export function createSortable(container, options, dotNetRef) {
  const opts = Object.assign({ axis: 'vertical', handle: null }, options || {});
  const axis = opts.axis; // 'vertical' | 'horizontal' | 'grid'
  const handleSelector = opts.handle || null;

  let dragging = false;
  let fromIndex = -1;
  let toIndex = -1;
  let activeItem = null;
  let rects = []; // item rects captured at drag start (DOM order is stable until commit)
  let pointerId = null;

  const notify = (method, ...args) =>
    Promise.resolve(dotNetRef.invokeMethodAsync(method, ...args)).catch(() => {});

  const directChildren = () =>
    Array.from(container.children).filter((el) => el.nodeType === 1);

  // Map an event target up to the direct child of `container` it belongs to.
  function itemFrom(target) {
    let el = target instanceof Element ? target : null;
    while (el && el.parentElement !== container) el = el.parentElement;
    return el && el.parentElement === container ? el : null;
  }

  // Insertion slot from the pointer, converted to a target index relative to the
  // removed item (so OnDrop's toIndex is a direct list index for C#).
  function indexFromPoint(x, y) {
    if (axis === 'grid') {
      let best = 0;
      let bestDist = Infinity;
      for (let i = 0; i < rects.length; i++) {
        const r = rects[i];
        const dx = x - (r.left + r.width / 2);
        const dy = y - (r.top + r.height / 2);
        const d = dx * dx + dy * dy;
        if (d < bestDist) { bestDist = d; best = i; }
      }
      return best;
    }
    const horizontal = axis === 'horizontal';
    let slot = rects.length; // default: past the last item
    for (let i = 0; i < rects.length; i++) {
      const r = rects[i];
      const mid = horizontal ? r.left + r.width / 2 : r.top + r.height / 2;
      const p = horizontal ? x : y;
      if (p < mid) { slot = i; break; }
    }
    if (slot > fromIndex) slot -= 1; // account for the dragged item leaving its slot
    return Math.max(0, Math.min(rects.length - 1, slot));
  }

  function clearDropTargets() {
    for (const el of directChildren()) el.removeAttribute('data-drop-target');
  }
  function setDropTarget(index) {
    clearDropTargets();
    const kids = directChildren();
    if (index >= 0 && index < kids.length) kids[index].setAttribute('data-drop-target', '');
  }

  function onPointerDown(e) {
    if (e.button !== 0) return;
    const item = itemFrom(e.target);
    if (!item) return;
    if (handleSelector) {
      const h = e.target instanceof Element ? e.target.closest(handleSelector) : null;
      if (!h || !item.contains(h)) return; // press must start on a handle
    }
    const kids = directChildren();
    fromIndex = kids.indexOf(item);
    if (fromIndex < 0) return;
    dragging = true;
    activeItem = item;
    toIndex = fromIndex;
    rects = kids.map((el) => el.getBoundingClientRect());
    pointerId = e.pointerId;
    try { container.setPointerCapture(e.pointerId); } catch {}
    item.setAttribute('data-dragging', '');
    e.preventDefault();
    notify('OnDragStart', fromIndex);
  }

  function onPointerMove(e) {
    if (!dragging) return;
    e.preventDefault();
    const next = indexFromPoint(e.clientX, e.clientY);
    if (next !== toIndex) {
      toIndex = next;
      setDropTarget(toIndex);
      notify('OnDragOver', fromIndex, toIndex);
    }
  }

  function finish(commit) {
    if (!dragging) return;
    dragging = false;
    try { if (pointerId != null) container.releasePointerCapture(pointerId); } catch {}
    pointerId = null;
    if (activeItem) activeItem.removeAttribute('data-dragging');
    clearDropTargets();
    const from = fromIndex;
    const to = toIndex;
    activeItem = null;
    fromIndex = -1;
    toIndex = -1;
    rects = [];
    if (commit && to >= 0 && to !== from) notify('OnDrop', from, to);
    else notify('OnCancel');
  }

  function onPointerUp(e) {
    if (!dragging) return;
    e.preventDefault();
    finish(true);
  }
  function onPointerCancel() { finish(false); }
  const scrollKeys = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', ' ']);
  function onKeyDown(e) {
    if (e.key === 'Escape' && dragging) { e.preventDefault(); finish(false); return; }
    // Suppress the page scroll for the reducer's own keys synchronously (no Blazor render lag)
    // while a sortable row itself is the focus target, leaving Tab / Enter / typing untouched.
    if (
      scrollKeys.has(e.key) &&
      e.target instanceof Element &&
      e.target.matches('[data-navius-sortable-item]') &&
      container.contains(e.target)
    ) {
      e.preventDefault();
    }
  }

  container.addEventListener('pointerdown', onPointerDown);
  container.addEventListener('pointermove', onPointerMove);
  container.addEventListener('pointerup', onPointerUp);
  container.addEventListener('pointercancel', onPointerCancel);
  document.addEventListener('keydown', onKeyDown, true);

  return {
    destroy() {
      if (dragging) finish(false);
      container.removeEventListener('pointerdown', onPointerDown);
      container.removeEventListener('pointermove', onPointerMove);
      container.removeEventListener('pointerup', onPointerUp);
      container.removeEventListener('pointercancel', onPointerCancel);
      document.removeEventListener('keydown', onKeyDown, true);
    },
  };
}
