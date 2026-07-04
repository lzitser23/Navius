import { test, expect, type Locator } from '@playwright/test';

// Chat / MessageScroller: the shadcn conversation layer, exercised on /chat.
// The scroll hot path is engine-driven and async, so scroll assertions poll
// (the ScrollArea test in wave3.spec.ts is the precedent).

test.beforeEach(async ({ page }) => {
  await page.goto('/chat');
  await expect(page.locator('[data-navius-messagescroller-viewport]')).toBeVisible({ timeout: 60_000 });
});

const metrics = (viewport: Locator) =>
  viewport.evaluate((el) => ({ top: el.scrollTop, max: el.scrollHeight - el.clientHeight }));

async function rowOffset(row: Locator, viewport: Locator): Promise<number> {
  const rb = await row.boundingBox();
  const vb = await viewport.boundingBox();
  return (rb!.y) - (vb!.y);
}

test('MessageScroller: viewport is a labelled log region (a11y + Base UI contract)', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');
  await expect(viewport).toHaveAttribute('role', 'region');
  await expect(viewport).toHaveAttribute('aria-label', 'Messages');
  await expect(viewport).toHaveAttribute('tabindex', '0');
  // Never the retired Radix data-state token.
  await expect(viewport).not.toHaveAttribute('data-state', /.*/);

  const content = page.locator('[data-navius-messagescroller-content]');
  await expect(content).toHaveAttribute('role', 'log');
  await expect(content).toHaveAttribute('aria-relevant', 'additions');

  // A seeded user row is a turn anchor and carries its stable id.
  const first = page.locator('[data-message-id="m0"]');
  await expect(first).toHaveAttribute('data-scroll-anchor', 'true');
});

test('MessageScroller: opens at the live edge (defaultScrollPosition="end")', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');
  await expect.poll(async () => (await metrics(viewport)).max).toBeGreaterThan(0); // it overflows
  await expect.poll(async () => {
    const m = await metrics(viewport);
    return m.max - m.top;
  }).toBeLessThanOrEqual(12);
});

test('MessageScroller: autoScroll follows a streamed reply to the live edge', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');
  const before = await metrics(viewport);

  for (let i = 0; i < 5; i++) {
    await page.locator('[data-testid="stream"]').click();
  }

  // The content grew and the viewport stayed pinned to the (new, larger) edge.
  await expect.poll(async () => (await metrics(viewport)).max).toBeGreaterThan(before.max);
  await expect.poll(async () => {
    const m = await metrics(viewport);
    return m.max - m.top;
  }).toBeLessThanOrEqual(12);
});

test('MessageScroller: scrolling up disengages follow (new content stays offscreen)', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');

  await viewport.hover();
  await page.mouse.wheel(0, -240);
  await expect.poll(async () => {
    const m = await metrics(viewport);
    return m.max - m.top;
  }).toBeGreaterThan(40); // clearly away from the edge
  const parked = (await metrics(viewport)).top;

  for (let i = 0; i < 5; i++) {
    await page.locator('[data-testid="stream"]').click();
  }

  // Follow is disengaged: the reader stays put, the reply grows offscreen below.
  const after = await metrics(viewport);
  expect(Math.abs(after.top - parked)).toBeLessThan(24);
  expect(after.max - after.top).toBeGreaterThan(40);
});

test('MessageScroller: the scroll button activates when scrolled away and returns to the edge', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');
  const button = page.locator('[data-navius-messagescroller-button]');

  // At the live edge the end button is inactive (inert, hidden).
  await expect(button).toHaveAttribute('data-active', 'false');

  await viewport.hover();
  await page.mouse.wheel(0, -240);
  await expect(button).toHaveAttribute('data-active', 'true');

  await button.click();
  await expect.poll(async () => {
    const m = await metrics(viewport);
    return m.max - m.top;
  }).toBeLessThanOrEqual(12);
  await expect(button).toHaveAttribute('data-active', 'false');
});

test('MessageScroller: appending an anchored turn moves it near the top with a peek', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');

  await page.locator('[data-testid="add-turn"]').click();

  // The new user turn (an anchor) is positioned near the top, keeping a peek of the
  // previous item above it (scrollPreviousItemPeek), rather than at the very top.
  const turn = page.locator('[data-message-id="t0-u"]');
  await expect(turn).toBeVisible();
  await expect.poll(() => rowOffset(turn, viewport)).toBeGreaterThan(16);
  await expect.poll(() => rowOffset(turn, viewport)).toBeLessThan(140);
});

test('MessageScroller: prepend preserves the reading position (stable ids)', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');
  const first = page.locator('[data-message-id="m0"]');

  // Bring m0 to the top and let the programmatic jump fully settle so the engine
  // captures m0 as the reading-position reference before the prepend.
  await page.locator('[data-testid="jump-first"]').click();
  await expect.poll(async () => (await metrics(viewport)).top).toBeLessThanOrEqual(12);
  await expect(page.locator('[data-testid="current-anchor"]')).toHaveText('m0');

  // Poll the SETTLED scroll state instead of a fixed delay: wait until scrollTop
  // stops moving (two consecutive samples within 1px) while parked at the top, so
  // the baseline below is captured from a viewport at rest, not mid-animation.
  let prevTop = Number.NaN;
  await expect
    .poll(async () => {
      const top = (await metrics(viewport)).top;
      const settled = Number.isFinite(prevTop) && Math.abs(top - prevTop) <= 1 && top <= 12;
      prevTop = top;
      return settled;
    })
    .toBe(true);

  const before = await metrics(viewport);
  const yBefore = await rowOffset(first, viewport);

  await page.locator('[data-testid="prepend"]').click();

  // Older rows arrived above, so scrollTop grew (incidental margin, kept modest to
  // catch a "prepend did not shift" regression without flaking)...
  await expect.poll(async () => (await metrics(viewport)).top).toBeGreaterThan(before.top + 20);
  // ...but m0 stayed exactly where it was. This is the meaningful reading-position
  // assertion, kept tight to catch a real anchoring regression.
  await expect.poll(async () => Math.abs((await rowOffset(first, viewport)) - yBefore)).toBeLessThan(8);
});

test('MessageScroller: jump-to-message scrolls a target row into view', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');
  const last = page.locator('[data-message-id="m7"]');

  // From the live edge, jump to the first message: it lands at the top and the last is offscreen.
  await page.locator('[data-testid="jump-first"]').click();
  await expect.poll(async () => (await metrics(viewport)).top).toBeLessThanOrEqual(12);
  await expect(page.locator('[data-message-id="m0"]')).toBeVisible();

  // Jump to the newest message id: it scrolls back down into view.
  await page.locator('[data-testid="jump-last"]').click();
  await expect(last).toBeVisible();
  await expect.poll(async () => (await metrics(viewport)).top).toBeGreaterThan(100);
});

test('MessageScroller: visibility tracking reports visible ids and the current anchor', async ({ page }) => {
  const viewport = page.locator('[data-navius-messagescroller-viewport]');

  // A subscriber (the inspector) is mounted, so tracking runs: some rows are visible.
  await expect.poll(async () => Number(await page.locator('[data-testid="visible-count"]').textContent()))
    .toBeGreaterThan(0);

  await page.locator('[data-testid="jump-first"]').click();
  await expect.poll(async () => (await metrics(viewport)).top).toBeLessThanOrEqual(12);
  await expect(page.locator('[data-testid="current-anchor"]')).toHaveText('m0');
});
