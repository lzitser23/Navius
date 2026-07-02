import { test, expect } from '@playwright/test';

// Wave 1 — spec-parity primitives, exercised on the /wave1 page.
test.beforeEach(async ({ page }) => {
  await page.goto('/wave1');
  await expect(page.locator('[data-navius-separator]').first()).toBeVisible({ timeout: 60_000 });
});

test('Separator: role + orientation; decorative omits role', async ({ page }) => {
  const seps = page.locator('[data-navius-separator]');
  await expect(seps.first()).toHaveAttribute('role', 'separator');
  await expect(seps.first()).toHaveAttribute('data-orientation', 'horizontal');
  const vertical = seps.nth(1);
  await expect(vertical).toHaveAttribute('data-orientation', 'vertical');
  await expect(vertical).not.toHaveAttribute('role', 'separator'); // decorative
});

test('Label: clicking it focuses the associated input', async ({ page }) => {
  await page.locator('[data-navius-label]').click();
  await expect(page.locator('#email')).toBeFocused();
});

test('AspectRatio: renders the ratio wrapper + fill layer', async ({ page }) => {
  await expect(page.locator('[data-navius-aspect-ratio]')).toBeVisible();
  await expect(page.locator('[data-navius-aspect-ratio-inner]')).toBeVisible();
});

test('VisuallyHidden: supplies the button accessible name while clipped', async ({ page }) => {
  await expect(page.getByRole('button', { name: 'Close' })).toBeVisible();
});

test('Progress: progressbar aria values + data-state', async ({ page }) => {
  const p = page.locator('[data-navius-progress]');
  await expect(p).toHaveAttribute('role', 'progressbar');
  await expect(p).toHaveAttribute('aria-valuenow', '60');
  await expect(p).toHaveAttribute('aria-valuemax', '100');
  await expect(p).toHaveAttribute('data-progressing', '');
  await expect(p).not.toHaveAttribute('data-state', /.*/);
});

test('Avatar: broken image src falls back', async ({ page }) => {
  await expect(page.locator('[data-navius-avatar-fallback]')).toContainText('LZ');
});

test('Toggle: aria-pressed + data-pressed (Base UI discrete)', async ({ page }) => {
  const t = page.locator('[data-navius-toggle]');
  await expect(t).toHaveAttribute('aria-pressed', 'false');
  await expect(t).not.toHaveAttribute('data-pressed', /.*/);
  await t.click();
  await expect(t).toHaveAttribute('aria-pressed', 'true');
  await expect(t).toHaveAttribute('data-pressed', '');
  await expect(t).not.toHaveAttribute('data-state', /.*/);
});

test('ToggleGroup (single): exactly one item pressed at a time', async ({ page }) => {
  const items = page.locator('[data-navius-togglegroup-item]');
  await expect(items.nth(1)).toHaveAttribute('data-pressed', ''); // center default
  await items.nth(0).click();
  await expect(items.nth(0)).toHaveAttribute('data-pressed', '');
  await expect(items.nth(1)).not.toHaveAttribute('data-pressed', /.*/);
});

test('Button: data-disabled + focusableWhenDisabled stays focusable', async ({ page }) => {
  const btns = page.locator('[data-navius-button]');
  await expect(btns.first()).not.toHaveAttribute('data-disabled', /.*/);
  const disabled = btns.nth(1);
  await expect(disabled).toHaveAttribute('data-disabled', '');
  await expect(disabled).toHaveAttribute('aria-disabled', 'true');
  await expect(disabled).not.toHaveAttribute('disabled', /.*/); // focusable: no native disabled attr
  await disabled.focus();
  await expect(disabled).toBeFocused();
});

test('Meter: role=meter + aria values, no data-state', async ({ page }) => {
  const m = page.locator('[data-navius-meter]');
  await expect(m).toHaveAttribute('role', 'meter');
  await expect(m).toHaveAttribute('aria-valuenow', '72');
  await expect(m).toHaveAttribute('aria-valuemax', '100');
  await expect(m).not.toHaveAttribute('data-state', /.*/);
  await expect(page.locator('[data-navius-meter-value]')).toContainText('72%');
});

test('RadioGroup: click selects; ArrowDown moves selection', async ({ page }) => {
  const radios = page.locator('[data-navius-radio-group-item]');
  await radios.nth(0).click(); // free
  await expect(radios.nth(0)).toHaveAttribute('aria-checked', 'true');
  await expect(radios.nth(0)).toHaveAttribute('data-checked', ''); // Base UI discrete
  await page.keyboard.press('ArrowDown'); // -> pro
  await expect(radios.nth(1)).toHaveAttribute('aria-checked', 'true');
  await expect(radios.nth(0)).toHaveAttribute('data-unchecked', '');
  await expect(page.locator('[data-radio-value]')).toContainText('pro');
});

test('CheckboxGroup: parent rolls up to indeterminate, then checks/unchecks all', async ({ page }) => {
  const parent = page.locator('[data-cbg-parent]');
  const apple = page.locator('[data-cbg-item="apple"]');
  const banana = page.locator('[data-cbg-item="banana"]');

  // Initial: only apple checked -> the parent is indeterminate.
  await expect(apple).toHaveAttribute('data-checked', '');
  await expect(parent).toHaveAttribute('data-indeterminate', '');
  await expect(parent).toHaveAttribute('aria-checked', 'mixed');

  // Clicking the parent checks every child.
  await parent.click();
  await expect(parent).toHaveAttribute('data-checked', '');
  await expect(banana).toHaveAttribute('data-checked', '');
  await expect(page.locator('[data-cbg-value]')).toContainText('banana');

  // Clicking again clears every child.
  await parent.click();
  await expect(parent).toHaveAttribute('data-unchecked', '');
  await expect(apple).toHaveAttribute('data-unchecked', '');

  // Checking one child rolls the parent back to indeterminate.
  await banana.click();
  await expect(parent).toHaveAttribute('data-indeterminate', '');
});

test('Collapsible: Base UI discrete attrs + deferred unmount on exit', async ({ page }) => {
  const trigger = page.locator('[data-navius-collapsible-trigger]');
  const panel = () => page.locator('[data-navius-collapsible-panel]');

  // Closed: Trigger has no data-panel-open; panel is removed from the DOM (not KeepMounted).
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');
  await expect(trigger).not.toHaveAttribute('data-panel-open', /.*/);
  await expect(panel()).toHaveCount(0);

  // Open: discrete data-open (no data-state, no data-closed); Trigger gets data-panel-open.
  await trigger.click();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');
  await expect(trigger).toHaveAttribute('data-panel-open', '');
  await expect(panel()).toBeVisible();
  await expect(panel()).toHaveAttribute('data-open', '');
  await expect(panel()).not.toHaveAttribute('data-state', /.*/);
  await expect(panel()).not.toHaveAttribute('data-closed', /.*/);

  // Close: the exit animation keeps the panel mounted (data-closed) until it finishes,
  // then the node is unmounted. Observing data-closed proves unmount was deferred.
  await trigger.click();
  await expect(panel()).toHaveAttribute('data-closed', '');
  await expect(panel()).toHaveCount(0);
});

test('PreviewCard: hover reveals the card (discrete data-open)', async ({ page }) => {
  const trigger = page.locator('[data-navius-preview-card-trigger]');
  const popup = page.locator('[data-navius-preview-card-popup]');
  await trigger.hover();
  await expect(popup).toBeVisible();
  await expect(popup).toHaveAttribute('data-open', '');
  await expect(popup).not.toHaveAttribute('data-state', /.*/);
  await expect(trigger).toHaveAttribute('data-popup-open', '');
});

test('AlertDialog: opens, focuses Cancel, backdrop does NOT dismiss, Cancel closes', async ({ page }) => {
  await page.locator('[data-navius-alert-dialog-trigger]').click();
  const content = page.locator('[data-navius-alert-dialog-popup]');
  await expect(content).toBeVisible();
  await expect(content).toHaveAttribute('role', 'alertdialog');
  await expect(page.locator('[data-navius-alert-dialog-cancel]')).toBeFocused();

  // an alert dialog must NOT close on backdrop click
  await page.locator('[data-navius-alert-dialog-backdrop]').click({ position: { x: 5, y: 5 } });
  await expect(content).toBeVisible();

  await page.locator('[data-navius-alert-dialog-cancel]').click();
  await expect(content).toBeHidden();
});
