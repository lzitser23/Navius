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
