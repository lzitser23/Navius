import { test, expect } from '@playwright/test';

// The drag engines (createSheetSwipe, createCarousel) take pointer capture to own a
// gesture. Capturing at pointerdown retargets the compatibility mouse events — and
// `click` with them — to the capturing element, so every control inside the sheet or
// slide goes dead. Both engines therefore wait for real travel before capturing.
// These tests pin both halves: the click still lands, and the drag still works.

test.beforeEach(async ({ page }) => {
  await page.goto('/fidelity');
});

// ----------------------------------------------------------------------- Drawer
test.describe('Drawer sheet: controls inside the panel', () => {
  const openDrawer = async (page: import('@playwright/test').Page) => {
    await page.locator('[data-demo="drawer"]').getByRole('button', { name: 'Open Drawer' }).click();
    const panel = page.locator('[data-navius-drawer-popup]');
    await expect(panel).toBeVisible();
    return panel;
  };

  test('the Cancel button inside the drawer closes it', async ({ page }) => {
    const panel = await openDrawer(page);

    await panel.getByRole('button', { name: 'Cancel' }).click();

    await expect(panel).toHaveCount(0);
  });

  test('a press inside the sheet leaves no inline transition lock behind', async ({ page }) => {
    const panel = await openDrawer(page);

    // A drag locks the sheet's transition so it tracks the pointer; a press that never
    // became a drag must not, or the sheet loses its close animation for good.
    await panel.getByText('Move Goal').click();

    await expect.poll(() => panel.evaluate((el: HTMLElement) => el.style.transition)).toBe('');
  });

  test('dragging the sheet past the dismiss threshold still closes it', async ({ page }) => {
    const panel = await openDrawer(page);
    const box = (await panel.boundingBox())!;
    const x = box.x + box.width / 2;
    const y = box.y + 12; // the drag handle sits at the top of a bottom sheet

    await page.mouse.move(x, y);
    await page.mouse.down();
    await page.mouse.move(x, y + box.height * 0.6, { steps: 12 });
    await page.mouse.up();

    await expect(panel).toHaveCount(0);
  });
});

// --------------------------------------------------------------------- Carousel
test.describe('Carousel viewport: controls inside a slide', () => {
  const selectedIndex = (viewport: import('@playwright/test').Locator) =>
    viewport.evaluate((el) =>
      getComputedStyle(el).getPropertyValue('--navius-carousel-selected-index').trim()
    );

  test('clicking a control inside a slide fires it without moving the carousel', async ({ page }) => {
    const car = page.locator('[data-demo="carousel"]');
    const viewport = car.locator('[data-carousel-viewport]');
    await expect(car.getByRole('button', { name: 'Next slide' })).toBeEnabled();
    await expect.poll(() => selectedIndex(viewport), { timeout: 10_000 }).toBe('0');

    // The demo slides carry no controls, so plant one — real slides routinely do.
    await viewport.evaluate((el) => {
      const btn = document.createElement('button');
      btn.textContent = 'slide cta';
      btn.addEventListener('click', () => {
        (window as unknown as { __slideClicks?: number }).__slideClicks =
          ((window as unknown as { __slideClicks?: number }).__slideClicks ?? 0) + 1;
      });
      el.firstElementChild!.firstElementChild!.prepend(btn);
    });

    await viewport.getByRole('button', { name: 'slide cta' }).click();

    expect(await page.evaluate(() => (window as unknown as { __slideClicks?: number }).__slideClicks)).toBe(1);
    expect(await selectedIndex(viewport)).toBe('0');
  });

  test('dragging the viewport still advances the slide', async ({ page }) => {
    const car = page.locator('[data-demo="carousel"]');
    const viewport = car.locator('[data-carousel-viewport]');
    await expect(car.getByRole('button', { name: 'Next slide' })).toBeEnabled();
    await expect.poll(() => selectedIndex(viewport), { timeout: 10_000 }).toBe('0');

    // page.mouse works in viewport coordinates and does no auto-scrolling of its own.
    await viewport.scrollIntoViewIfNeeded();
    const box = (await viewport.boundingBox())!;
    const y = box.y + box.height / 2;

    await page.mouse.move(box.x + box.width - 12, y);
    await page.mouse.down();
    await page.mouse.move(box.x + 12, y, { steps: 12 });
    await page.mouse.up();

    await expect.poll(() => selectedIndex(viewport), { timeout: 10_000 }).toBe('1');
  });
});
