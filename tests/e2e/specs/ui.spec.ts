import { test, expect } from '@playwright/test';

// zits/ui — the styled helm. These assert that the Navius brain's behaviour
// (focus trap, data-state, ARIA, keyboard) still works THROUGH the styled wrappers.
test.beforeEach(async ({ page }) => {
  await page.goto('/ui');
  await expect(page.locator('[data-zits-ui-showcase]')).toBeVisible();
});

test('Button renders with the styled primary variant', async ({ page }) => {
  const btn = page.getByRole('button', { name: 'Default', exact: true });
  await expect(btn).toBeVisible();
  await expect(btn).toHaveClass(/bg-primary/);
});

test('Dialog opens through the helm and Esc closes it (brain focus trap)', async ({ page }) => {
  await page.getByRole('button', { name: 'Open dialog' }).click();
  const content = page.locator('[data-navius-dialog-popup]');
  await expect(content).toBeVisible();
  await expect(content).toHaveAttribute('aria-modal', 'true');
  await page.keyboard.press('Escape');
  await expect(content).toBeHidden();
});

test('Switch toggles its discrete state through the helm', async ({ page }) => {
  const sw = page.locator('[data-navius-switch]').first();
  await expect(sw).toHaveAttribute('data-checked', ''); // showcase seeds it on
  await sw.click();
  await expect(sw).toHaveAttribute('data-unchecked', '');
  await expect(sw).not.toHaveAttribute('data-checked', /.*/);
});

test('Accordion expands the clicked item through the helm', async ({ page }) => {
  const trigger = page.getByRole('button', { name: 'Is it styled?' });
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');
  await trigger.click();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');
});
