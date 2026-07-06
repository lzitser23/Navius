import { test, expect, type Locator } from '@playwright/test';

// The imperative services on /services: ZitsDialogService (awaitable confirm/alert/custom
// dialogs composed over the real ZitsAlertDialog / ZitsDialog wrappers), KeyboardShortcutService
// (a global mod+k registration whose preventDefault fires JS-side), and NaviusHeightAnimation
// (a clipped element whose height WAAPI-tweens on a baked spring). The dialog store resolves a
// Task, so the "result" readouts prove the await actually completed with the right value.

const boxHeight = (el: Locator) => el.evaluate((n) => n.getBoundingClientRect().height);
const animationCount = (el: Locator) => el.evaluate((n) => n.getAnimations().length);

test.beforeEach(async ({ page }) => {
  await page.goto('/services');
  await expect(page.locator('[data-testid="confirm-trigger"]')).toBeVisible({ timeout: 60_000 });
});

// --- DialogService ------------------------------------------------------------

test('confirm: clicking the action resolves the awaited Task with true', async ({ page }) => {
  await expect(page.locator('[data-testid="confirm-result"]')).toHaveText('not run');

  await page.locator('[data-testid="confirm-trigger"]').click();

  // The styled alert dialog mounts over the real Navius primitive (portal + focus trap).
  const popup = page.locator('[data-navius-alert-dialog-popup]');
  await expect(popup).toBeVisible();

  await page.locator('[data-testid="dialog-confirm"]').click();

  // The awaited ConfirmAsync completed with true, and the dialog closed.
  await expect(page.locator('[data-testid="confirm-result"]')).toHaveText('deleted');
  await expect(popup).toHaveCount(0);
});

test('confirm: cancelling resolves the awaited Task with false', async ({ page }) => {
  await page.locator('[data-testid="confirm-trigger"]').click();
  await expect(page.locator('[data-navius-alert-dialog-popup]')).toBeVisible();

  await page.locator('[data-testid="dialog-cancel"]').click();

  await expect(page.locator('[data-testid="confirm-result"]')).toHaveText('kept');
});

test('confirm: Escape dismisses and resolves false (an alert dialog never closes on outside click)', async ({ page }) => {
  await page.locator('[data-testid="confirm-trigger"]').click();
  const popup = page.locator('[data-navius-alert-dialog-popup]');
  await expect(popup).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(page.locator('[data-testid="confirm-result"]')).toHaveText('kept');
  await expect(popup).toHaveCount(0);
});

test('alert: acknowledging resolves the awaited Task', async ({ page }) => {
  await page.locator('[data-testid="alert-trigger"]').click();
  await expect(page.locator('[data-navius-alert-dialog-popup]')).toBeVisible();

  await page.locator('[data-testid="dialog-ok"]').click();

  await expect(page.locator('[data-testid="alert-result"]')).toHaveText('acknowledged');
});

test('custom ShowAsync: the handle closes with a typed result', async ({ page }) => {
  await page.locator('[data-testid="prompt-trigger"]').click();

  // Custom content renders inside the real ZitsDialog popup.
  const popup = page.locator('[data-navius-dialog-popup]');
  await expect(popup).toBeVisible();

  const input = page.locator('[data-testid="rename-input"]');
  await input.fill('Roadmap');
  await page.locator('[data-testid="rename-save"]').click();

  await expect(page.locator('[data-testid="prompt-result"]')).toHaveText("renamed to 'Roadmap'");
  await expect(popup).toHaveCount(0);
});

test('custom ShowAsync: cancelling resolves with the default (null)', async ({ page }) => {
  await page.locator('[data-testid="prompt-trigger"]').click();
  await expect(page.locator('[data-navius-dialog-popup]')).toBeVisible();

  await page.locator('[data-testid="rename-cancel"]').click();

  await expect(page.locator('[data-testid="prompt-result"]')).toHaveText('cancelled');
});

// --- KeyboardShortcutService --------------------------------------------------

test('shortcut: mod+k fires the registered handler and toggles the palette', async ({ page }) => {
  // Give the async listener registration (module import + createShortcutListener) a beat to
  // attach after the first render.
  await page.waitForTimeout(500);

  await expect(page.locator('[data-testid="palette"]')).toHaveCount(0);
  await expect(page.locator('[data-testid="shortcut-count"]')).toHaveText('0');

  await page.keyboard.press('Control+KeyK');

  // The handler ran (count incremented) and its state change is visible.
  await expect(page.locator('[data-testid="shortcut-count"]')).toHaveText('1');
  await expect(page.locator('[data-testid="palette"]')).toBeVisible();

  // It is a toggle: a second press hides it again.
  await page.keyboard.press('Control+KeyK');
  await expect(page.locator('[data-testid="shortcut-count"]')).toHaveText('2');
  await expect(page.locator('[data-testid="palette"]')).toHaveCount(0);
});

// --- HeightAnimation ----------------------------------------------------------

test('height: expand animates from 0 to the natural height, collapse settles back to 0', async ({ page }) => {
  const region = page.locator('[data-testid="height-region"]');

  // Starts collapsed: the clipped box is at height 0 (its 1px borders are all that remain).
  await expect.poll(() => boxHeight(region)).toBeLessThan(12);

  await page.locator('[data-testid="height-toggle"]').click();

  // A WAAPI height tween runs (catch it in flight rather than racing the settle).
  const ran = await region.evaluate(async (n) => {
    const start = performance.now();
    while (performance.now() - start < 2000) {
      if (n.getAnimations().length > 0) return true;
      await new Promise((r) => requestAnimationFrame(r));
    }
    return false;
  });
  expect(ran).toBe(true);

  // Settles open at its natural content height (several paragraphs, comfortably tall).
  await expect.poll(() => boxHeight(region)).toBeGreaterThan(80);

  // Collapse: it tweens back and settles at 0 (just the borders remain).
  await page.locator('[data-testid="height-toggle"]').click();
  await expect.poll(() => boxHeight(region)).toBeLessThan(12);
});

test('height (content-tracking): adding content grows the always-tracking region', async ({ page }) => {
  const region = page.locator('[data-testid="track-region"]');

  await expect(page.locator('[data-testid="track-line"]')).toHaveCount(1);
  const before = await boxHeight(region);

  await page.locator('[data-testid="track-add"]').click();

  // The ResizeObserver on the content fires and the region tweens up to the new natural height.
  await expect(page.locator('[data-testid="track-line"]')).toHaveCount(2);
  await expect.poll(() => boxHeight(region)).toBeGreaterThan(before + 5);
});

test('height: a running tween settles with no animation left on the element', async ({ page }) => {
  const region = page.locator('[data-testid="height-region"]');
  await page.locator('[data-testid="height-toggle"]').click();

  // After it opens, no WAAPI animation is left holding a fill (the tween uses fill:none and the
  // element rests at its own inline natural height).
  await expect.poll(() => boxHeight(region)).toBeGreaterThan(80);
  await expect.poll(() => animationCount(region)).toBe(0);
});
