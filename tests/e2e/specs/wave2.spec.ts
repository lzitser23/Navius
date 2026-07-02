import { test, expect } from '@playwright/test';

// Wave 2 — spec-parity primitives, exercised on the /wave2 page.
test.beforeEach(async ({ page }) => {
  await page.goto('/wave2');
  await expect(page.locator('[data-navius-context-menu-trigger]')).toBeVisible({ timeout: 60_000 });
});

test('ContextMenu: right-click opens role=menu at the pointer; first item focused; Esc closes', async ({ page }) => {
  const trigger = page.locator('[data-navius-context-menu-trigger]');
  const box = (await trigger.boundingBox())!;
  await page.mouse.click(box.x + 30, box.y + 20, { button: 'right' });

  const content = page.locator('[data-navius-context-menu-popup]');
  await expect(content).toBeVisible();
  await expect(content).toHaveAttribute('role', 'menu');
  // roving focuses the first non-disabled item ("Back")
  await expect(content.locator('[role="menuitem"]').first()).toBeFocused();

  await page.keyboard.press('Escape');
  await expect(content).toBeHidden();
});

test('Menubar: opens a menu, and ArrowRight roves to the next trigger', async ({ page }) => {
  const triggers = page.locator('[data-navius-menubar-trigger]');
  await triggers.nth(0).click();
  await expect(page.locator('[data-navius-menubar-popup]').first()).toBeVisible();
  await expect(triggers.nth(0)).toHaveAttribute('aria-expanded', 'true');

  await page.keyboard.press('Escape');
  await triggers.nth(0).focus();
  await page.keyboard.press('ArrowRight');
  await expect(triggers.nth(1)).toBeFocused();
});

test('Toolbar: role=toolbar, ArrowRight roves focus between items', async ({ page }) => {
  await expect(page.locator('[data-navius-toolbar]')).toHaveAttribute('role', 'toolbar');
  const items = page.locator('[data-navius-toolbar-item]');
  await items.nth(0).focus();
  await page.keyboard.press('ArrowRight');
  await expect(items.nth(1)).toBeFocused();
});

test('AccessibleIcon: names the icon-only button for screen readers', async ({ page }) => {
  await expect(page.getByRole('button', { name: 'Close dialog' })).toBeVisible();
});

test('DirectionProvider: cascades dir=rtl to descendant consumers', async ({ page }) => {
  // DirectionProvider is DOM-transparent (like the spec) — it renders no element and
  // only cascades the reading direction; a descendant consumer reflects it.
  await expect(page.locator('[data-navius-direction-readout]')).toHaveAttribute('dir', 'rtl');
});

test('RTL: an Align="start" anchored popup mirrors to the trigger\'s right edge under dir=rtl', async ({ page }) => {
  const scope = page.locator('[data-testid="rtl-popover"]');
  const trigger = scope.locator('[data-navius-popover-trigger]');
  const popup = page.locator('[data-navius-popover-popup]');

  await trigger.click();
  await expect(popup).toBeVisible();

  const t = (await trigger.boundingBox())!;
  const p = (await popup.boundingBox())!;

  // Align="start" is logical: under RTL the popup right-aligns to the trigger, so a popup
  // wider than the (narrow) trigger extends well LEFT of the trigger's left edge — the
  // opposite of LTR start-alignment (where popup.left ≈ trigger.left). This assertion fails
  // in LTR and fails without the engine's RTL align mirror, so it has teeth. (No vertical
  // assertion — Side="bottom" may collision-flip to top depending on scroll, which is fine.)
  expect(p.x).toBeLessThan(t.x - 10);
});

test('PasswordToggleField: toggles input type + accessible name (the spec omits aria-pressed)', async ({ page }) => {
  const input = page.locator('[data-navius-password-toggle-field-input]');
  const toggle = page.locator('[data-navius-password-toggle-field-toggle]');
  await expect(input).toHaveAttribute('type', 'password');
  // the spec conveys state via a flipping accessible name only — no aria-pressed/data-state.
  await expect(toggle).toHaveAttribute('aria-label', 'Show password');
  await expect(toggle).not.toHaveAttribute('aria-pressed', /.*/);

  await toggle.click();
  await expect(input).toHaveAttribute('type', 'text');
  await expect(toggle).toHaveAttribute('aria-label', 'Hide password');
});

test('OneTimePasswordField: distributes input across cells and aggregates the value', async ({ page }) => {
  const cells = page.locator('[data-navius-otp-input]');
  await cells.nth(0).fill('123'); // multi-char input distributes across cells 0..2
  await expect(cells.nth(3)).toBeFocused();
  await expect(page.locator('[data-otp-value]')).toContainText('123');
  await expect(cells.nth(0)).toHaveAttribute('data-filled', ''); // Base UI discrete
  await expect(cells.nth(4)).not.toHaveAttribute('data-filled', /.*/);
});
