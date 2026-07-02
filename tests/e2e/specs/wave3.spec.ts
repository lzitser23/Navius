import { test, expect } from '@playwright/test';

// Wave 3 — the hard 5, exercised on the /wave3 page.
test.beforeEach(async ({ page }) => {
  await page.goto('/wave3');
  await expect(page.locator('[data-navius-slider-thumb]')).toBeVisible({ timeout: 60_000 });
});

test('Slider: role=slider, keyboard moves the value', async ({ page }) => {
  const thumb = page.locator('[data-navius-slider-thumb]');
  await expect(thumb).toHaveAttribute('role', 'slider');
  await expect(thumb).toHaveAttribute('data-index', '0'); // Base UI per-thumb index
  await expect(thumb).toHaveAttribute('aria-valuenow', '50');

  await thumb.focus();
  await page.keyboard.press('Home');
  await expect(thumb).toHaveAttribute('aria-valuenow', '0');
  await page.keyboard.press('ArrowRight');
  await expect(thumb).toHaveAttribute('aria-valuenow', '1');
  await page.keyboard.press('End');
  await expect(thumb).toHaveAttribute('aria-valuenow', '100');
  await expect(page.locator('[data-slider-value]')).toContainText('100');
});

test('ScrollArea: native overflow scrolls; custom scrollbar is present', async ({ page }) => {
  const viewport = page.locator('[data-navius-scrollarea-viewport]');
  await expect(page.locator('[data-navius-scrollarea-scrollbar]')).toBeVisible();

  await viewport.hover();
  await page.mouse.wheel(0, 300);
  await expect.poll(() => viewport.evaluate((el) => el.scrollTop)).toBeGreaterThan(0);
});

test('NavigationMenu: focus opens positioned content; Esc closes', async ({ page }) => {
  const trigger = page.locator('[data-navius-navigationmenu-trigger]').first();
  await trigger.focus(); // focus opens immediately (a click would focus-open then toggle-close)
  const content = page.locator('[data-navius-navigationmenu-content]').first();
  await expect(content).toBeVisible();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');

  await page.keyboard.press('Escape');
  await expect(content).toBeHidden();
});

test('NavigationMenu: Base UI discrete contract — no data-state, data-popup-open + data-side', async ({ page }) => {
  const trigger = page.locator('[data-navius-navigationmenu-trigger]').first();
  const list = page.locator('[data-navius-navigationmenu-list]').first();

  // Closed: the trigger carries no open marker (and NEVER the retired data-state).
  await expect(trigger).not.toHaveAttribute('data-popup-open', /.*/);
  await expect(trigger).not.toHaveAttribute('data-state', /.*/);

  await trigger.focus();
  const content = page.locator('[data-navius-navigationmenu-content]').first();
  const popup = page.locator('[data-navius-navigationmenu-popup]').first();
  await expect(content).toBeVisible();

  // Open: discrete data-popup-open on the trigger (present, empty value) + the List.
  await expect(trigger).toHaveAttribute('data-popup-open', '');
  await expect(list).toHaveAttribute('data-popup-open', '');

  // The engine mirrors data-side onto the Popup; nothing emits data-state anymore.
  await expect(popup).toHaveAttribute('data-side', /top|bottom|left|right/);
  await expect(trigger).not.toHaveAttribute('data-state', /.*/);
  await expect(content).not.toHaveAttribute('data-state', /.*/);
  await expect(popup).not.toHaveAttribute('data-state', /.*/);

  await page.keyboard.press('Escape');
  await expect(content).toBeHidden();
  await expect(trigger).not.toHaveAttribute('data-popup-open', /.*/);
});

test('NavigationMenu (shared viewport): one popup teleports panels, re-anchors on switch, no data-state', async ({ page }) => {
  const shared = page.locator('[data-navius-navmenu-shared]');
  const learn = shared.getByRole('button', { name: 'Learn' });
  const company = shared.getByRole('button', { name: 'Company' });

  await learn.focus(); // focus opens the shared menu on Learn
  const popup = page.locator('[data-navius-navigationmenu-popup]').first();
  await expect(popup).toBeVisible();
  await expect(popup).toHaveAttribute('data-side', /top|bottom|left|right/);
  await expect(popup).not.toHaveAttribute('data-state', /.*/);
  // Learn's Content teleported into the single shared viewport inside the popup.
  await expect(page.getByRole('link', { name: 'Guides' })).toBeVisible();

  // Switch to Company while open: the SAME popup stays mounted (morphs + re-anchors) and
  // now hosts Company's panel; the direction of travel surfaces as data-activation-direction.
  await company.focus();
  await expect(popup).toBeVisible();
  await expect(page.getByRole('link', { name: /About us/ })).toBeVisible();
  // The direction of travel (Learn -> Company, i.e. rightward) surfaces on the popup.
  await expect(popup).toHaveAttribute('data-activation-direction', /left|right|up|down/);
  await expect(popup).not.toHaveAttribute('data-state', /.*/);

  await page.keyboard.press('Escape');
  await expect(popup).toBeHidden();
});

test('NavigationMenu: ArrowDown from a focus-opened trigger moves focus into the panel', async ({ page }) => {
  // Regression (fix #1): a keyboard "enter content" on an ALREADY-open item must move focus
  // into the panel. Focus-open leaves Value unchanged, so the ArrowDown open-request is a
  // no-op transition — pre-fix nothing focused the panel and focus stayed on the trigger.
  const trigger = page.locator('[data-navius-navigationmenu-trigger]').first(); // Products (standalone)
  await trigger.focus(); // focus opens the panel; focus stays on the trigger (hover-parity)
  const content = page.locator('[data-navius-navigationmenu-content]').first();
  await expect(content).toBeVisible();
  await expect(trigger).toBeFocused();

  // APG "enter content" (horizontal -> ArrowDown): focus the first focusable child of the panel.
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('link', { name: 'Analytics' })).toBeFocused();
});

test('NavigationMenu (shared viewport): switching panels leaves no stray/duplicate panel in the DOM', async ({ page }) => {
  // Regression (fix #4): switching the shared viewport must release AND unmount the outgoing
  // panel. Pre-fix the un-teleport raced the unmount and re-appended a Blazor-untracked copy
  // into the item's <li>, so every switch orphaned another interactive panel (count grows).
  const shared = page.locator('[data-navius-navmenu-shared]');
  const learn = shared.getByRole('button', { name: 'Learn' });
  const company = shared.getByRole('button', { name: 'Company' });

  await learn.focus(); // focus-opens the shared menu on Learn (its panel teleports into the viewport)
  await expect(page.getByRole('link', { name: 'Guides' })).toBeVisible();
  await expect(page.locator('[data-navius-navigationmenu-content]')).toHaveCount(1);

  // Switch to Company: exactly ONE live panel must remain (Learn's is dropped, not orphaned).
  await company.focus();
  await expect(page.getByRole('link', { name: /About us/ })).toBeVisible();
  await expect(page.locator('[data-navius-navigationmenu-content]')).toHaveCount(1);
  // And Learn's unique panel content is gone from the document (no stray interactive copy left).
  await expect(page.getByRole('link', { name: 'Guides' })).toHaveCount(0);
});

test('Field: onChange validation flips discrete state + aria-invalid and shows the matching error', async ({ page }) => {
  const email = page.locator('[data-navius-email]');
  const error = page.locator('[data-navius-email-error]');

  // Pristine: native required is unmet but validity is not surfaced yet (onChange, no edit).
  await expect(email).not.toHaveAttribute('data-invalid', /.*/);
  await expect(email).not.toHaveAttribute('aria-invalid', /.*/);
  await expect(error).toBeHidden();

  await email.fill('not-an-email');
  await expect(email).toHaveAttribute('aria-invalid', 'true');
  await expect(email).toHaveAttribute('data-invalid', '');
  await expect(email).toHaveAttribute('data-filled', '');
  await expect(email).toHaveAttribute('data-dirty', '');
  await expect(error).toBeVisible();
  await expect(error).toContainText('valid email');

  // Blur latches data-touched and drops data-focused.
  await email.press('Tab');
  await expect(email).toHaveAttribute('data-touched', '');
  await expect(email).not.toHaveAttribute('data-focused', /.*/);

  // A valid value flips to data-valid and hides the error.
  await email.fill('user@example.com');
  await expect(email).toHaveAttribute('data-valid', '');
  await expect(email).not.toHaveAttribute('data-invalid', /.*/);
  await expect(email).not.toHaveAttribute('aria-invalid', /.*/);
  await expect(error).toBeHidden();
});

test('Form: errors-by-name surface on the named field and auto-clear on edit', async ({ page }) => {
  const promo = page.locator('[data-navius-promo]');
  const promoError = page.locator('[data-navius-promo-error]');

  await expect(promoError).toBeHidden();
  await page.getByRole('button', { name: 'Reject promo' }).click();

  await expect(promoError).toBeVisible();
  await expect(promoError).toContainText('invalid');
  await expect(promo).toHaveAttribute('data-invalid', '');

  // Editing the field auto-clears the server error (the spec behaviour).
  await promo.fill('NEWCODE');
  await expect(promoError).toBeHidden();
  await expect(promo).not.toHaveAttribute('data-invalid', /.*/);
});

test('Fieldset: disabled cascades data-disabled to the legend + fields and disables controls', async ({ page }) => {
  const fieldset = page.locator('[data-navius-fieldset-demo]');
  const legend = page.locator('[data-navius-legend]');
  const city = page.locator('[data-navius-city]');

  await expect(fieldset).not.toHaveAttribute('data-disabled', /.*/);
  await expect(city).toBeEnabled();

  await page.locator('[data-navius-fieldset-toggle]').check();

  await expect(fieldset).toHaveAttribute('data-disabled', '');
  await expect(legend).toHaveAttribute('data-disabled', '');
  await expect(city).toBeDisabled();
});

test('Input (standalone): reflects filled + focused/touched field state', async ({ page }) => {
  const input = page.locator('[data-navius-standalone-input]');

  await expect(input).toHaveAttribute('data-filled', ''); // DefaultValue="hello"
  await input.focus();
  await expect(input).toHaveAttribute('data-focused', '');
  await input.blur();
  await expect(input).not.toHaveAttribute('data-focused', /.*/);
  await expect(input).toHaveAttribute('data-touched', '');
});

test('NumberField: buttons + keyboard step within min/max bounds', async ({ page }) => {
  const input = page.locator('[data-navius-numfield-input]');
  const inc = page.locator('[data-navius-numfield-inc]');
  const dec = page.locator('[data-navius-numfield-dec]');

  await expect(input).toHaveValue('3');
  await expect(input).toHaveAttribute('role', 'spinbutton');

  await inc.click();
  await expect(input).toHaveValue('4');
  await expect(page.locator('[data-numfield-value]')).toContainText('4');

  await input.focus();
  await page.keyboard.press('ArrowUp');
  await expect(input).toHaveValue('5');

  await page.keyboard.press('Home'); // -> Min (0)
  await expect(input).toHaveValue('0');
  await expect(dec).toBeDisabled(); // clamped at Min

  await page.keyboard.press('End'); // -> Max (10)
  await expect(input).toHaveValue('10');
  await expect(inc).toBeDisabled(); // clamped at Max
});

test('Slot: forwards behavioural props onto the consumer child (asChild)', async ({ page }) => {
  const link = page.getByRole('link', { name: 'Go to dashboard' });
  // the parent's data-* + ARIA land on the child <a> — the spec asChild, Blazor-style
  await expect(link).toHaveAttribute('data-navius-trigger', 'open');
  await expect(link).toHaveAttribute('aria-haspopup', 'dialog');
  // the child keeps its own styling (its literal class, written after @attributes, wins)
  await expect(link).toHaveClass(/bg-primary/);
});
