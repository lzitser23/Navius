import { test, expect, type Locator } from '@playwright/test';

// Navius.Motion (M0-b), exercised on /motion. Two tiers are under test: the CSS tier
// (generated navius-motion.css presence classes riding the discrete state attributes of
// real primitives) and the runtime tier (the WAAPI executor reached through
// MotionJsInterop). Springs are ~270-740ms baked linear() runs, so mid-flight reads have
// a wide window; settle assertions poll. Overlays that animate `transform` are centred
// with the independent `translate` property, so `transform` reads `none` at rest.

const opacity = (el: Locator) => el.evaluate((n) => parseFloat(getComputedStyle(n).opacity));
const transform = (el: Locator) => el.evaluate((n) => getComputedStyle(n).transform);
// Identity has two computed spellings: the `none` keyword (CSS transform:none, or a
// backwards-filling animation that has reverted to the natural style) and the identity
// matrix (a forwards-filling animation still holding an animated `transform: none`).
const IDENTITY = /^(none|matrix\(1, 0, 0, 1, 0, 0\))$/;

test.beforeEach(async ({ page }) => {
  await page.goto('/motion');
  await expect(page.locator('[data-testid="motion-dialog-trigger"]')).toBeVisible({ timeout: 60_000 });
});

test('presence enter (CSS tier): the dialog animates in from the hidden frame, then settles open', async ({ page }) => {
  await page.locator('[data-testid="motion-dialog-trigger"]').click();
  const popup = page.locator('[data-testid="motion-dialog-popup"]');
  await popup.waitFor({ state: 'attached' });

  // It mounts carrying data-starting-style (the hidden frame: opacity 0, scale 0.9), so a
  // read taken inside the enter window is strictly below full opacity: it is animating in.
  expect(await opacity(popup)).toBeLessThan(1);

  // It settles fully open: opacity 1 and the animated transform back to identity (the
  // -50%/-50% centring lives on `translate`, not `transform`, so this reads `none`).
  await expect.poll(() => opacity(popup)).toBe(1);
  await expect.poll(() => transform(popup)).toBe('none');
});

test('presence exit (CSS tier): the dialog defers unmount through its exit, then removes the node', async ({ page }) => {
  await page.locator('[data-testid="motion-dialog-trigger"]').click();
  const popup = page.locator('[data-testid="motion-dialog-popup"]');
  await expect.poll(() => opacity(popup)).toBe(1); // fully open first

  await page.locator('[data-testid="motion-dialog-close"]').click();

  // Deferred-unmount cooperation: the node stays mounted through the exit transition,
  // carrying data-ending-style, rather than vanishing the instant it closes.
  await expect(popup).toHaveAttribute('data-ending-style', '');

  // Once the exit finishes, the presence machine unmounts it.
  await expect(popup).toHaveCount(0);
});

test('presence retarget (CSS tier): rapid open/close/open settles OPEN with no stuck transform', async ({ page }) => {
  // The stress button slams the controlled open state faster than the 740ms enter and
  // 270ms exit, driving the presence machine's re-open-mid-exit + transition-interruption
  // hotspot. It ends OPEN.
  await page.locator('[data-testid="dialog-stress"]').click();

  const popup = page.locator('[data-testid="motion-dialog-popup"]');
  await expect(popup).toBeVisible();

  // No stuck mid-state: fully open, transform back to identity, no lingering exit.
  await expect.poll(() => opacity(popup)).toBe(1);
  await expect.poll(() => transform(popup)).toBe('none');
  await expect(popup).toHaveAttribute('data-open', '');
  await expect(popup).not.toHaveAttribute('data-ending-style', /.*/);
});

test('presence (CSS tier): the menu fades in on open and unmounts after its fade-out', async ({ page }) => {
  await page.locator('[data-testid="motion-menu-trigger"]').click();
  const popup = page.locator('[data-testid="motion-menu-popup"]');
  await popup.waitFor({ state: 'attached' });

  // motion-fade is opacity-only; caught mid-fade below full.
  expect(await opacity(popup)).toBeLessThan(1);
  await expect.poll(() => opacity(popup)).toBe(1);

  await page.keyboard.press('Escape');
  await expect(popup).toHaveCount(0); // fade-out then deferred unmount
});

test('presence (runtime tier): the WAAPI overlay scales in on a bouncy spring and hides on close', async ({ page }) => {
  const overlay = page.locator('[data-testid="runtime-popup"]');

  await page.locator('[data-testid="runtime-toggle"]').click(); // open
  // The enter runs on the compositor: a real transform (the bouncy scale) is applied
  // mid-flight, proving CreatePresenceMotionAsync animated it (not a CSS class).
  await expect.poll(() => transform(overlay)).not.toBe('none');

  // Settles fully open.
  await expect.poll(() => opacity(overlay)).toBe(1);
  await expect.poll(() => transform(overlay)).toBe('none');

  await page.locator('[data-testid="runtime-toggle"]').click(); // close
  await expect.poll(() => opacity(overlay)).toBe(0); // exit runs, overlay returns to hidden
});

test('press gesture (runtime tier): a fast click leaves the transform at identity (stuck-gesture regression)', async ({ page }) => {
  const btn = page.locator('[data-testid="press-button"]');

  // Playwright click dispatches pointerdown then pointerup in quick succession: the exact
  // scenario the fixed navius-motion.js gesture bug got stuck on (pinned at scale 0.97).
  await btn.click();

  // The release tween returns the transform to identity and holds it there (never stuck at
  // the pressed scale(0.97) = matrix(0.97, ...), which is what the fixed bug produced).
  await expect.poll(() => transform(btn)).toMatch(IDENTITY);
});

test('reduced motion (runtime guard): transform collapses to none while opacity still animates', async ({ page }) => {
  // The navius-motion.js guard strips transform keyframes under reduced motion (collapse to
  // opacity-only) but keeps animating opacity. shouldReduceMotion is read at play() time, so
  // emulating before the open is enough.
  await page.emulateMedia({ reducedMotion: 'reduce' });

  const overlay = page.locator('[data-testid="runtime-popup"]');
  await page.locator('[data-testid="runtime-toggle"]').click(); // open

  // Transform collapsed: the overlay never scales.
  expect(await transform(overlay)).toBe('none');

  // Opacity still animates and completes to fully visible.
  await expect.poll(() => opacity(overlay)).toBe(1);
  expect(await transform(overlay)).toBe('none');
});

test('enter wrapper: replays the enter with smooth, snappy and bouncy springs, settling visible', async ({ page }) => {
  const el = page.locator('[data-testid="wrapper-el"]');
  for (const spring of ['smooth', 'snappy', 'bouncy']) {
    await page.locator(`[data-testid="wrapper-${spring}"]`).click();
    // Each pick remounts NaviusMotion (@key) and replays the enter; it settles fully
    // visible with the animated transform back to identity.
    await expect.poll(() => opacity(el)).toBe(1);
    await expect.poll(() => transform(el)).toBe('none');
  }
});

// --- Micro pack (M1) ----------------------------------------------------------
// The number of in-flight animations on an element (CSS + WAAPI both surface here).
const animationCount = (el: Locator) => el.evaluate((n) => n.getAnimations().length);

test('micro pack: shake plays once on a fake validation error, then settles at identity', async ({ page }) => {
  const field = page.locator('[data-testid="shake-field"]');

  // Submit with the field empty: the fake validation fires and the field shakes (runtime
  // one-shot). The error message proves the handler ran.
  await page.locator('[data-testid="shake-submit"]').click();
  await expect(page.locator('[data-testid="shake-error"]')).toBeVisible();

  // Catch the shake in flight: poll for an animation to appear on the field (it starts a
  // beat after the interop round trip) rather than racing the ~450ms one-shot.
  const ran = await field.evaluate(async (n) => {
    const start = performance.now();
    while (performance.now() - start < 2000) {
      if (n.getAnimations().length > 0) return true;
      await new Promise((r) => requestAnimationFrame(r));
    }
    return false;
  });
  expect(ran).toBe(true);

  // One-shot with fill:none returns the transform to identity and holds it there.
  await expect.poll(() => transform(field)).toMatch(IDENTITY);
});

test('micro pack: the pulse dot loops (an infinite animation runs) and stops on toggle', async ({ page }) => {
  const dot = page.locator('[data-testid="pulse-dot"]');

  // Autoplayed on load: an animation is running and it iterates forever.
  await expect.poll(() => animationCount(dot)).toBeGreaterThan(0);
  const looping = await dot.evaluate((n) =>
    n.getAnimations().some((a) => a.effect!.getComputedTiming().iterations === Infinity)
  );
  expect(looping).toBe(true);

  // Stop it: the loop is cancelled and no animation remains.
  await page.locator('[data-testid="pulse-toggle"]').click();
  await expect.poll(() => animationCount(dot)).toBe(0);
});

test('micro pack: the shimmer sweeps (CSS tier) and its reduced-motion fallback is static', async ({ page }) => {
  const block = page.locator('[data-testid="shimmer-block"]');

  // The generated CSS class drives an infinite background-position sweep.
  await expect.poll(() => animationCount(block)).toBeGreaterThan(0);
  expect(await block.evaluate((n) => getComputedStyle(n).animationName)).toBe('navius-shimmer');

  // Documented reduced-motion fallback: the sweep stops (animation: none) and the
  // gradient surface rests statically, so no animation remains.
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect.poll(() => block.evaluate((n) => getComputedStyle(n).animationName)).toBe('none');
  await expect.poll(() => animationCount(block)).toBe(0);
});

// --- Auto-animate (M2) --------------------------------------------------------
// The FLIP-on-mutation container on /motion: a NaviusAutoAnimate list with add / remove
// first / shuffle (reverse) / storm buttons and a comma-joined order readout of the C#
// state. The container FLIPs on a baked Spring.Default linear() run (~0.5s window), so
// mid-flight reads are catchable. Item ids are monotonic and never reused, so a removed
// item's exit clone can never collide with a live item.
const items = (page: import('@playwright/test').Page) =>
  page.locator('[data-testid="auto-animate-list"] > [data-testid="auto-animate-item"]');

// True once a frame shows any list item carrying a non-identity translate (a live FLIP).
async function sawLiveTranslate(page: import('@playwright/test').Page, windowMs: number): Promise<boolean> {
  return page.evaluate(async (ms) => {
    const nodes = () => Array.from(document.querySelectorAll('[data-testid="auto-animate-item"]'));
    const start = performance.now();
    while (performance.now() - start < ms) {
      for (const n of nodes()) {
        const t = getComputedStyle(n).transform;
        const m = t && t !== 'none' ? t.match(/matrix\(([^)]+)\)/) : null;
        if (m) {
          const p = m[1].split(',').map((v) => parseFloat(v));
          if (Math.abs(p[4]) > 0.5 || Math.abs(p[5]) > 0.5) return true;
        }
      }
      await new Promise((r) => requestAnimationFrame(r));
    }
    return false;
  }, windowMs);
}

// The in-flow DOM order must equal the C# state (the order readout): proves the exit
// clones never corrupt Blazor's reconciliation of the container's children.
async function expectDomMatchesState(page: import('@playwright/test').Page) {
  const readout = ((await page.locator('[data-testid="auto-animate-order"]').textContent()) ?? '').trim();
  const dom = await items(page).evaluateAll((ns) =>
    ns.map((n) => n.getAttribute('data-item')).join(',')
  );
  expect(dom).toBe(readout);
}

test('auto-animate: a reorder FLIPs, a moved item carries a live translate mid-flight, then settles', async ({ page }) => {
  await expect(items(page)).toHaveCount(5);

  await page.locator('[data-testid="auto-animate-shuffle"]').click();

  // Reverse moves every item: at least one shows a live translate on the compositor.
  expect(await sawLiveTranslate(page, 3000)).toBe(true);

  // Every item settles back to identity (FLIP fills none, resting transform is none).
  await expect
    .poll(() =>
      items(page).evaluateAll((ns) =>
        ns.every((n) => {
          const t = getComputedStyle(n).transform;
          return t === 'none' || t === 'matrix(1, 0, 0, 1, 0, 0)';
        })
      )
    )
    .toBe(true);
  await expectDomMatchesState(page);
});

test('auto-animate: a removed item pins position:absolute and animates out, then detaches (no orphan)', async ({ page }) => {
  const id = await items(page).first().getAttribute('data-item');
  const exiting = page.locator(`[data-testid="auto-animate-item"][data-item="${id}"]`);

  await page.locator('[data-testid="auto-animate-remove"]').click();

  // Still present mid-exit: pinned out of flow (position:absolute) with a running animation.
  await expect.poll(() => exiting.evaluate((n) => getComputedStyle(n).position)).toBe('absolute');
  expect(await exiting.evaluate((n) => n.getAnimations().length)).toBeGreaterThan(0);

  // The exit finishes and the node detaches: nothing left behind.
  await expect(exiting).toHaveCount(0);
  await expect(items(page)).toHaveCount(4);
});

test('auto-animate: an added item scales and fades in, then settles visible', async ({ page }) => {
  const before = await items(page).count();

  await page.locator('[data-testid="auto-animate-add"]').click();
  const added = items(page).first(); // prepended

  // Caught mid-enter: the new node is below full opacity (fading/scaling in) at some frame.
  const faded = await added.evaluate(async (n) => {
    const start = performance.now();
    while (performance.now() - start < 2000) {
      if (parseFloat(getComputedStyle(n).opacity) < 1) return true;
      await new Promise((r) => requestAnimationFrame(r));
    }
    return false;
  });
  expect(faded).toBe(true);

  await expect.poll(() => opacity(added)).toBe(1);
  await expect(items(page)).toHaveCount(before + 1);
});

test('auto-animate (reduced motion): a reorder applies instantly with no transform animation', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(items(page)).toHaveCount(5);
  const orderBefore = await page.locator('[data-testid="auto-animate-order"]').textContent();

  await page.locator('[data-testid="auto-animate-shuffle"]').click();

  // The guard strips the FLIP transform: no live translate ever appears on any item.
  expect(await sawLiveTranslate(page, 800)).toBe(false);

  // The reorder still happened (layout applied instantly), and the DOM matches state.
  const orderAfter = await page.locator('[data-testid="auto-animate-order"]').textContent();
  expect(orderAfter).not.toBe(orderBefore);
  await expectDomMatchesState(page);
});

test('auto-animate: a rapid mutation storm settles at the correct layout with no orphaned absolute nodes', async ({ page }) => {
  await page.locator('[data-testid="auto-animate-storm"]').click();

  const list = page.locator('[data-testid="auto-animate-list"]');

  // Settle: no animations running in the subtree and no absolutely-positioned exit clones.
  await expect
    .poll(
      () =>
        list.evaluate((el) => {
          const running = el.getAnimations({ subtree: true }).length;
          const orphans = Array.from(el.children).filter(
            (c) => getComputedStyle(c).position === 'absolute'
          ).length;
          return running === 0 && orphans === 0;
        }),
      { timeout: 15_000 }
    )
    .toBe(true);

  const orphans = await list.evaluate(
    (el) => Array.from(el.children).filter((c) => getComputedStyle(c).position === 'absolute').length
  );
  expect(orphans).toBe(0);

  // No reconciliation corruption: the surviving DOM children equal the C# state exactly.
  await expectDomMatchesState(page);
});
