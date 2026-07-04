import { test, expect } from '@playwright/test';

// Uncontrolled (Default*) usage of the styled helm form wrappers. Each wrapper used to
// forward its value/open param unconditionally, which forced the brain primitive into
// controlled mode and killed the uncontrolled Default* path. These assert that a
// Default*-only wrapper both seeds its initial state AND stays interactive.
test.beforeEach(async ({ page }) => {
  await page.goto('/uncontrolled');
  await expect(page.locator('[data-uncontrolled-showcase]')).toBeVisible({ timeout: 60_000 });
});

test('Switch: DefaultChecked seeds on and toggles uncontrolled', async ({ page }) => {
  const sw = page.locator('[data-testid="u-switch"]');
  await expect(sw).toHaveAttribute('data-checked', ''); // DefaultChecked="true"
  await sw.click();
  await expect(sw).toHaveAttribute('data-unchecked', '');
  await expect(sw).not.toHaveAttribute('data-checked', /.*/);
});

test('Checkbox: DefaultChecked seeds checked (not indeterminate) and toggles uncontrolled', async ({ page }) => {
  const cb = page.locator('[data-testid="u-checkbox"]');
  await expect(cb).toHaveAttribute('data-checked', ''); // DefaultChecked="true"
  await expect(cb).not.toHaveAttribute('data-indeterminate', /.*/);
  await cb.click();
  await expect(cb).toHaveAttribute('data-unchecked', '');
});

test('RadioGroup: DefaultValue seeds the checked radio and selection moves uncontrolled', async ({ page }) => {
  const a = page.locator('[data-testid="u-radio-a"]');
  const b = page.locator('[data-testid="u-radio-b"]');

  await expect(b).toHaveAttribute('aria-checked', 'true'); // DefaultValue="b"
  await expect(a).toHaveAttribute('aria-checked', 'false');

  await a.click();
  await expect(a).toHaveAttribute('aria-checked', 'true');
  await expect(b).toHaveAttribute('aria-checked', 'false');
});

test('Select: DefaultValue seeds the value and opening + picking works uncontrolled', async ({ page }) => {
  const trigger = page.locator('[data-testid="u-select-trigger"]');

  // Opening is the uncontrolled open path that the wart used to force shut.
  await trigger.click();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');

  // DefaultValue="banana" seeded the selection: the banana option is selected on
  // first open. (The trigger label resolves only after items mount, in controlled
  // and uncontrolled mode alike, so aria-selected is the honest DefaultValue proof.)
  await expect(page.locator('[data-testid="u-select-banana"]')).toHaveAttribute('aria-selected', 'true');

  // Picking a different option updates the value uncontrolled and closes the listbox.
  await page.locator('[data-testid="u-select-apple"]').click();
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');

  // Reopen: the uncontrolled pick stuck (apple is now the selected option). The
  // trigger label is not asserted because the label registry drops entries when
  // items unmount on close, in controlled and uncontrolled mode alike (pre-existing
  // component quirk, tracked separately); selection state is the honest signal.
  await trigger.click();
  await expect(page.locator('[data-testid="u-select-apple"]')).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator('[data-testid="u-select-banana"]')).toHaveAttribute('aria-selected', 'false');
});

test('Slider: DefaultValue seeds the thumb and the keyboard moves it uncontrolled', async ({ page }) => {
  const thumb = page.locator('[data-testid="u-slider"] [role="slider"]');

  await expect(thumb).toHaveAttribute('aria-valuenow', '40'); // DefaultValue=[40]
  await thumb.focus();
  await page.keyboard.press('ArrowRight'); // + Step (1)
  await expect(thumb).toHaveAttribute('aria-valuenow', '41');
});

test('Sheet: opens from internal state without two-way binding and Esc closes it', async ({ page }) => {
  await page.locator('[data-testid="u-sheet-trigger"]').click();
  const popup = page.locator('[data-navius-dialog-popup]');
  await expect(popup).toBeVisible();
  await expect(popup).toContainText('Uncontrolled sheet');
  await page.keyboard.press('Escape');
  await expect(popup).toBeHidden();
});
