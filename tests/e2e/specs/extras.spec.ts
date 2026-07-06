import { test, expect } from '@playwright/test';

// Extras: the two helm-only presentational surfaces on the /extras page.
// Typography: each variant renders its correct semantic element + type class.
// Timeline: the ol/li structure and the data-* state hooks.
test.beforeEach(async ({ page }) => {
  await page.goto('/extras');
  await expect(page.locator('[data-zits-extras]')).toBeVisible({ timeout: 60_000 });
});

// ---- Typography ----------------------------------------------------------

// Variant -> the semantic element it must render (the shadcn scale mapping).
const VARIANT_ELEMENT: Record<string, string> = {
  H1: 'h1',
  H2: 'h2',
  H3: 'h3',
  H4: 'h4',
  P: 'p',
  Lead: 'p',
  Large: 'div',
  Small: 'small',
  Muted: 'p',
  Blockquote: 'blockquote',
  List: 'ul',
  InlineCode: 'code',
};

test('Typography renders the correct semantic element for every variant', async ({ page }) => {
  const scope = page.locator('[data-testid="typography"]');
  for (const [variant, element] of Object.entries(VARIANT_ELEMENT)) {
    const node = scope.locator(`[data-navius-typography][data-variant="${variant}"]`);
    await expect(node, `variant ${variant} should be present`).toHaveCount(1);
    const tag = await node.evaluate((el) => el.tagName.toLowerCase());
    expect(tag, `variant ${variant} should render <${element}>`).toBe(element);
  }
});

test('Typography applies the per-variant type classes', async ({ page }) => {
  const scope = page.locator('[data-testid="typography"]');
  // A couple of representative variant classes from the shadcn scale.
  await expect(scope.locator('[data-variant="H1"]')).toHaveClass(/font-extrabold/);
  await expect(scope.locator('[data-variant="H1"]')).toHaveClass(/text-4xl/);
  await expect(scope.locator('[data-variant="InlineCode"]')).toHaveClass(/font-mono/);
  await expect(scope.locator('[data-variant="Muted"]')).toHaveClass(/text-muted-foreground/);
  await expect(scope.locator('[data-variant="Blockquote"]')).toHaveClass(/italic/);
});

test('Typography List renders a <ul> that contains its <li> items', async ({ page }) => {
  const list = page.locator('[data-navius-typography][data-variant="List"]');
  await expect(list).toHaveCount(1);
  await expect(list.locator('li')).toHaveCount(3);
});

// ---- Timeline ------------------------------------------------------------

test('Timeline root is an <ol> carrying orientation + align data attributes', async ({ page }) => {
  const timeline = page.locator('[data-navius-timeline]');
  await expect(timeline).toHaveCount(1);
  const tag = await timeline.evaluate((el) => el.tagName.toLowerCase());
  expect(tag).toBe('ol');
  await expect(timeline).toHaveAttribute('data-orientation', 'Vertical');
  await expect(timeline).toHaveAttribute('data-align', 'Left');
});

test('Timeline items are <li> with the complete/current/pending status hook', async ({ page }) => {
  const items = page.locator('[data-navius-timeline-item]');
  await expect(items).toHaveCount(3);

  const firstTag = await items.first().evaluate((el) => el.tagName.toLowerCase());
  expect(firstTag).toBe('li');

  await expect(items.nth(0)).toHaveAttribute('data-status', 'complete');
  await expect(items.nth(1)).toHaveAttribute('data-status', 'current');
  await expect(items.nth(2)).toHaveAttribute('data-status', 'pending');
});

test('Timeline parts: dot carries its variant, time is a <time>, connectors are aria-hidden', async ({ page }) => {
  const scope = page.locator('[data-testid="timeline"]');

  // Dots: one per item, first uses the default ink variant.
  const dots = scope.locator('[data-navius-timeline-dot]');
  await expect(dots).toHaveCount(3);
  await expect(dots.nth(0)).toHaveAttribute('data-variant', 'default');
  await expect(dots.nth(1)).toHaveAttribute('data-variant', 'outline');

  // Connectors are decorative and only run between items (the last item omits it).
  const connectors = scope.locator('[data-navius-timeline-connector]');
  await expect(connectors).toHaveCount(2);
  await expect(connectors.first()).toHaveAttribute('aria-hidden', 'true');

  // Time renders as a semantic <time> element.
  const time = scope.locator('[data-navius-timeline-time]').first();
  const timeTag = await time.evaluate((el) => el.tagName.toLowerCase());
  expect(timeTag).toBe('time');

  // Title + description exist inside the content block.
  await expect(scope.locator('[data-navius-timeline-title]').first()).toHaveText('Order placed');
  await expect(scope.locator('[data-navius-timeline-description]')).toHaveCount(3);
});
