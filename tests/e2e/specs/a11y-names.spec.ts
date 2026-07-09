import { test, expect } from '@playwright/test';

// Accessible-name + ARIA-state wiring added by the a11y-naming-and-states fix.
// These assert the NEW wiring against the existing playground demos:
//   - Fieldset root -> Legend via aria-labelledby (/wave3)
//   - Field.Description auto-joins the control's aria-describedby (/wave3)
//   - Menu popup -> trigger via aria-labelledby (/)
//   - MaskedInput keeps aria-invalid coupled to data-invalid (/tokens)
//   - a group-style DateInput never emits a dangling aria-labelledby, and resolves to
//     a mounted label when field-wrapped (/dates)
// NOTE: authored in an isolated worktree where the playground cannot build (it references a
// sibling repo path), so these were NOT run here; they run in the main checkout / CI.

test('Fieldset: root aria-labelledby points at the mounted legend', async ({ page }) => {
  await page.goto('/wave3');
  const fieldset = page.locator('[data-navius-fieldset-demo]');
  await expect(fieldset).toBeVisible({ timeout: 60_000 });

  // The root emits aria-labelledby only once the legend has registered (re-render after mount).
  await expect(fieldset).toHaveAttribute('aria-labelledby', /.+/);
  const labelledBy = await fieldset.getAttribute('aria-labelledby');
  await expect(page.locator('[data-navius-legend]')).toHaveAttribute('id', labelledBy!);
});

test('Field.Description: its id joins the control aria-describedby', async ({ page }) => {
  await page.goto('/wave3');
  const desc = page.locator('[data-navius-field-description]');
  await expect(desc).toBeVisible({ timeout: 60_000 });

  const descId = await desc.getAttribute('id');
  expect(descId).toBeTruthy();

  // The email control's aria-describedby contains the description id (it reconciles after mount).
  await expect(page.locator('[data-navius-email]')).toHaveAttribute(
    'aria-describedby',
    new RegExp(descId!)
  );
});

test('Menu popup: aria-labelledby points at the trigger', async ({ page }) => {
  await page.goto('/');
  const trigger = page.locator('[data-navius-menu-trigger]');
  await expect(trigger).toBeVisible({ timeout: 60_000 });

  await trigger.click();
  const popup = page.locator('[data-navius-menu-popup]');
  await expect(popup).toBeVisible();
  await expect(popup).toHaveAttribute('role', 'menu');

  await expect(popup).toHaveAttribute('aria-labelledby', /.+/);
  const labelledBy = await popup.getAttribute('aria-labelledby');
  await expect(trigger).toHaveAttribute('id', labelledBy!);
});

test('MaskedInput: aria-invalid stays coupled to data-invalid', async ({ page }) => {
  await page.goto('/tokens');
  const input = page.locator('[data-navius-masked-input]').first();
  await expect(input).toBeVisible({ timeout: 60_000 });

  // The demo is valid, so both are absent; aria-invalid must mirror data-invalid either way.
  const dataInvalid = await input.getAttribute('data-invalid');
  const ariaInvalid = await input.getAttribute('aria-invalid');
  expect(!!ariaInvalid).toBe(dataInvalid !== null);
  if (dataInvalid !== null) {
    expect(ariaInvalid).toBe('true');
  }
});

test('DateInput group: no dangling aria-labelledby standalone; resolves to a label when field-wrapped', async ({ page }) => {
  await page.goto('/dates');
  const standalone = page.locator('[data-demo=date-controlled] [data-navius-date-input]');
  await expect(standalone).toBeVisible({ timeout: 60_000 });

  // Outside a Field there is no label to point at, so the group must not carry aria-labelledby.
  await expect(standalone).not.toHaveAttribute('aria-labelledby', /.+/);

  // If a field-wrapped date group is present, its aria-labelledby must resolve to a mounted label.
  const labelled = page.locator('[data-navius-date-input][aria-labelledby]');
  if ((await labelled.count()) > 0) {
    const id = await labelled.first().getAttribute('aria-labelledby');
    expect(id).toBeTruthy();
    await expect(page.locator(`#${id}`)).toHaveCount(1);
  }
});
