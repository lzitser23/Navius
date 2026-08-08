import { test, expect, Locator } from '@playwright/test';

// Charts showcase: the four new helm chart types (Pie/Donut, Radar, RadialBar,
// Scatter) on the Navius brain, over the hand-rolled inline-SVG + ChartMath
// architecture. These assert real rendering THROUGH the styled wrappers: one
// slice/ring/point per data row, that a hover reveals the shared ZitsChartTooltip,
// and that the container's injected `--color-<key>` vars actually paint the marks.
test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  await page.goto('/charts');
  await expect(page.locator('[data-charts-showcase]')).toBeVisible({ timeout: 60_000 });
});

// getComputedStyle resolves the injected `var(--color-<key>)` chain to a real colour,
// so these prove the ChartStyle var pipeline end to end. #e76f51 -> rgb(231, 111, 81).
const fill = (loc: Locator) => loc.evaluate((el) => getComputedStyle(el as Element).fill);
const stroke = (loc: Locator) => loc.evaluate((el) => getComputedStyle(el as Element).stroke);
const CHROME = 'rgb(231, 111, 81)';

// ------------------------------------------------------------------- Pie / Donut
test.describe('Pie / Donut', () => {
  test('renders one slice per data row with the configured dither pattern', async ({ page }) => {
    const pie = page.locator('[data-demo="pie"]');
    await expect(pie.locator('[data-slice]')).toHaveCount(5);
    expect(await fill(pie.locator('[data-slice-key="chrome"]'))).toContain('url(');
    expect(await fill(pie.locator('pattern[id*="chrome-gradient"] rect').first())).toBe(CHROME);
  });

  // Clicking a slice is a pointer shortcut; the slices are not controls, so the legend
  // carries the selection for the keyboard and the screen reader.
  test('a slice click locks a series and the legend toggles it back off', async ({ page }) => {
    const pie = page.locator('[data-demo="pie"]');
    const chrome = pie.locator('[data-slice-key="chrome"]');
    const safariLegend = pie.getByRole('button', { name: 'Safari' });

    await pie.locator('[data-slice-key="safari"]').click();
    await expect(safariLegend).toHaveAttribute('aria-pressed', 'true');
    await expect(chrome).toHaveAttribute('opacity', '0.3');

    await safariLegend.press('Enter');
    await expect(safariLegend).toHaveAttribute('aria-pressed', 'false');
    await expect(chrome).toHaveAttribute('opacity', '1');
  });

  test('the plotted slices are not focus stops — the legend is the keyboard surface', async ({ page }) => {
    const pie = page.locator('[data-demo="pie"]');

    await expect(pie.locator('[data-slice][tabindex]')).toHaveCount(0);
    await expect(pie.getByRole('button', { name: 'Safari' })).toHaveCount(1);
  });

  test('hovering a slice reveals the tooltip', async ({ page }) => {
    const pie = page.locator('[data-demo="pie"]');
    await expect(pie.locator('[data-chart-tooltip]')).toHaveCount(0);

    await pie.locator('[data-slice-key="safari"]').dispatchEvent('mouseover');

    const tip = pie.locator('[data-chart-tooltip]');
    await expect(tip).toBeVisible();
    await expect(tip).toContainText('Safari');
  });

  test('the donut leaves a hole with a centre total', async ({ page }) => {
    const donut = page.locator('[data-demo="donut"]');
    await expect(donut.locator('[data-slice]')).toHaveCount(5);
    await expect(donut.locator('[data-center]')).toContainText('925');
  });
});

// -------------------------------------------------------------------------- Radar
test.describe('Radar', () => {
  test('renders one polygon per series over the shared category axes', async ({ page }) => {
    const radar = page.locator('[data-demo="radar"]');
    await expect(radar.locator('[data-series]')).toHaveCount(2);
    await expect(radar.locator('[data-hit]')).toHaveCount(6); // one hover wedge per category
    expect(await fill(radar.locator('[data-series-key="desktop"]'))).toContain('url(');
    expect(await fill(radar.locator('pattern[id*="desktop-gradient"] rect').first())).toBe(CHROME);
  });

  test('hovering a category wedge reveals the tooltip with both series', async ({ page }) => {
    const radar = page.locator('[data-demo="radar"]');

    await radar.locator('[data-hit]').first().dispatchEvent('mouseover');

    const tip = radar.locator('[data-chart-tooltip]');
    await expect(tip).toBeVisible();
    await expect(tip).toContainText('Desktop');
    await expect(tip).toContainText('Mobile');
  });

  test('renders sparkles and activates opt-in bloom on hover', async ({ page }) => {
    const radar = page.locator('[data-demo="radar"]');
    await expect(radar.locator('.chart-sparkle')).toHaveCount(2);

    const series = radar.locator('[data-series-key="desktop"]');
    await expect(series).toHaveCSS('animation-name', /chart-.*-polar/);
    await radar.locator('[data-hit]').first().dispatchEvent('mouseover');
    await expect(series).not.toHaveCSS('filter', 'none');
  });

  test('disables reveals and sparkle loops for reduced motion', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.reload();

    const radar = page.locator('[data-demo="radar"]');
    await expect(radar.locator('[data-series-key="desktop"]')).toHaveCSS('animation-name', 'none');
    await expect(radar.locator('.chart-sparkle').first()).toHaveCSS('animation-name', 'none');
  });
});

// ---------------------------------------------------------------------- RadialBar
test.describe('RadialBar', () => {
  test('renders one ring per category, stroked from the --color vars', async ({ page }) => {
    const radial = page.locator('[data-demo="radial"]');
    await expect(radial.locator('[data-bar]')).toHaveCount(5);
    // A radial bar carries its colour on the stroke, not the fill.
    expect(await stroke(radial.locator('[data-bar-key="chrome"]'))).toBe(CHROME);
  });

  test('hovering a ring reveals the tooltip', async ({ page }) => {
    const radial = page.locator('[data-demo="radial"]');

    await radial.locator('[data-hit]').first().dispatchEvent('mouseover');

    await expect(radial.locator('[data-chart-tooltip]')).toBeVisible();
  });
});

// ------------------------------------------------------------------------ Scatter
test.describe('Scatter', () => {
  test('renders one marker per point, coloured by its series', async ({ page }) => {
    const scatter = page.locator('[data-demo="scatter"]');
    await expect(scatter.locator('[data-point]')).toHaveCount(6);
    expect(await fill(scatter.locator('[data-point-key="groupA"]').first())).toBe(CHROME);
  });

  test('hovering a point reveals the tooltip', async ({ page }) => {
    const scatter = page.locator('[data-demo="scatter"]');

    await scatter.locator('[data-point]').first().dispatchEvent('mouseover');

    await expect(scatter.locator('[data-chart-tooltip]')).toBeVisible();
  });
});

test('clickable legend locks selection across sibling chart parts', async ({ page }) => {
  await page.goto('/fidelity');
  const chart = page.locator('[data-demo="chart"]');

  await chart.getByRole('button', { name: 'Mobile' }).click();
  await expect(chart.locator('[data-series-key="mobile"]').first()).toHaveAttribute('opacity', '1');
  await expect(chart.locator('[data-series-key="desktop"]').first()).toHaveAttribute('opacity', '0.3');
});
