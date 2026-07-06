import { test, expect, type Locator } from '@playwright/test';

// The runtime theming engine on /theme: the ZitsThemeSwitcher writes the six
// data-zits-* dimension attributes (+ the .dark class) onto <html> and persists the
// selection to localStorage; the pre-paint zits-theme-init.js restores it before
// first paint on reload; ZitsThemeScope carries a full scoped theme on a subtree,
// including a forced light/dark that ignores the page mode. These assert the effects
// THROUGH the generated OKLCH token CSS: real computed --primary / --background
// tokens, real border-radius and font-family, and real scoped background colours.

// A custom property read off <html>. The primary/style blocks set these directly on
// the element carrying the data-zits-* attribute, so this resolves to a real value.
const cssVar = (page: import('@playwright/test').Page, name: string) =>
  page.locator('html').evaluate((el, n) => getComputedStyle(el).getPropertyValue(n).trim(), name);

const fontFamily = (page: import('@playwright/test').Page) =>
  page.locator('html').evaluate((el) => getComputedStyle(el).fontFamily);

const sampleRadius = (page: import('@playwright/test').Page) =>
  page
    .locator('[data-testid="theme-sample-button"]')
    .evaluate((el) => getComputedStyle(el).borderTopLeftRadius);

const hasDark = (page: import('@playwright/test').Page) =>
  page.locator('html').evaluate((el) => el.classList.contains('dark'));

// Normalise any computed background colour (rgb / oklch / color()) to a 0..255
// brightness via a 1x1 canvas, so the scoped-theme assertions don't depend on the
// serialised colour format.
const brightness = (loc: Locator) =>
  loc.evaluate((el) => {
    const c = getComputedStyle(el as Element).backgroundColor;
    const cvs = document.createElement('canvas');
    cvs.width = cvs.height = 1;
    const ctx = cvs.getContext('2d')!;
    ctx.fillStyle = '#000';
    ctx.fillStyle = c;
    ctx.fillRect(0, 0, 1, 1);
    const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
    return (r + g + b) / 3;
  });

// Open the switcher panel (idempotent: reopens if a pick closed it) and return it.
async function openPanel(page: import('@playwright/test').Page): Promise<Locator> {
  const panel = page.locator('[data-slot="theme-switcher-panel"]');
  if (!(await panel.isVisible().catch(() => false))) {
    await page.locator('[data-slot="theme-switcher-trigger"]').click();
    await expect(panel).toBeVisible();
  }
  return panel;
}

// Click one dimension option (data-dimension + data-value) inside the panel.
async function pick(
  page: import('@playwright/test').Page,
  dimension: string,
  value: string
): Promise<void> {
  const panel = await openPanel(page);
  await panel.locator(`[data-dimension="${dimension}"][data-value="${value}"]`).click();
}

test.beforeEach(async ({ page }) => {
  await page.goto('/theme');
  await expect(page.locator('[data-slot="theme-switcher-trigger"]')).toBeVisible({ timeout: 60_000 });
});

test('the switcher opens and picking primary blue sets the attribute and the --primary token', async ({ page }) => {
  const html = page.locator('html');
  const before = await cssVar(page, '--primary');

  await page.locator('[data-slot="theme-switcher-trigger"]').click();
  await expect(page.locator('[data-slot="theme-switcher-panel"]')).toBeVisible();
  await page
    .locator('[data-slot="theme-switcher-panel"]')
    .locator('[data-dimension="primary"][data-value="blue"]')
    .click();

  await expect(html).toHaveAttribute('data-zits-primary', 'blue');
  const after = await cssVar(page, '--primary');
  expect(after).not.toBe(before);
  expect(after.length).toBeGreaterThan(0);
});

test('picking mode light removes the .dark class and mode dark adds it', async ({ page }) => {
  const html = page.locator('html');

  await pick(page, 'mode', 'light');
  await expect(html).toHaveAttribute('data-zits-mode', 'light');
  await expect.poll(() => hasDark(page)).toBe(false);

  await pick(page, 'mode', 'dark');
  await expect(html).toHaveAttribute('data-zits-mode', 'dark');
  await expect.poll(() => hasDark(page)).toBe(true);
});

test('a non-default selection survives a reload via the pre-paint restore', async ({ page }) => {
  const html = page.locator('html');

  await pick(page, 'mode', 'dark');
  await pick(page, 'primary', 'blue');
  await pick(page, 'radius', 'none');
  await expect(html).toHaveAttribute('data-zits-radius', 'none');

  const primaryBefore = await cssVar(page, '--primary');
  const radiusBefore = await sampleRadius(page);

  await page.reload();
  await expect(page.locator('[data-slot="theme-switcher-trigger"]')).toBeVisible({ timeout: 60_000 });

  // The attributes are on <html> from the pre-paint init script, before Blazor booted.
  await expect(html).toHaveAttribute('data-zits-mode', 'dark');
  await expect(html).toHaveAttribute('data-zits-primary', 'blue');
  await expect(html).toHaveAttribute('data-zits-radius', 'none');
  await expect.poll(() => hasDark(page)).toBe(true);
  expect(await cssVar(page, '--primary')).toBe(primaryBefore);
  expect(await sampleRadius(page)).toBe(radiusBefore);
});

test('a forced-light scope stays light and a forced-dark scope stays dark, independent of the page', async ({ page }) => {
  const light = await brightness(page.locator('[data-testid="theme-scope-light"]'));
  const dark = await brightness(page.locator('[data-testid="theme-scope-dark"]'));

  expect(light).toBeGreaterThan(160);
  expect(dark).toBeLessThan(110);
  expect(light).toBeGreaterThan(dark + 80);
});

test('picking radius none zeroes the sample button radius and xl enlarges it', async ({ page }) => {
  const html = page.locator('html');

  await pick(page, 'radius', 'none');
  await expect(html).toHaveAttribute('data-zits-radius', 'none');
  const none = await sampleRadius(page);
  expect(parseFloat(none)).toBe(0);

  await pick(page, 'radius', 'xl');
  await expect(html).toHaveAttribute('data-zits-radius', 'xl');
  const xl = await sampleRadius(page);
  expect(parseFloat(xl)).toBeGreaterThan(parseFloat(none));
});

test('picking font serif changes the page font-family to the serif stack', async ({ page }) => {
  const before = await fontFamily(page);

  await pick(page, 'font', 'serif');
  await expect(page.locator('html')).toHaveAttribute('data-zits-font', 'serif');

  await expect.poll(() => fontFamily(page)).toContain('Charter');
  expect(await fontFamily(page)).not.toBe(before);
});

test('picking style tinted changes the page background token versus standard', async ({ page }) => {
  const html = page.locator('html');

  await pick(page, 'style', 'standard');
  await expect(html).toHaveAttribute('data-zits-style', 'standard');
  const standard = await cssVar(page, '--background');

  await pick(page, 'style', 'tinted');
  await expect(html).toHaveAttribute('data-zits-style', 'tinted');
  const tinted = await cssVar(page, '--background');

  expect(tinted).not.toBe(standard);
  expect(tinted.length).toBeGreaterThan(0);
});

test('reset clears the data-zits-* attributes and removes the persisted key', async ({ page }) => {
  await pick(page, 'primary', 'blue');
  await pick(page, 'style', 'contrast');
  await expect(page.locator('html')).toHaveAttribute('data-zits-primary', 'blue');

  const panel = await openPanel(page);
  await panel.getByRole('button', { name: /reset/i }).click();

  await expect
    .poll(() =>
      page
        .locator('html')
        .evaluate((el) => el.getAttributeNames().filter((n) => n.startsWith('data-zits-')).length)
    )
    .toBe(0);
  expect(await page.evaluate(() => localStorage.getItem('zits-theme'))).toBeNull();
});
