import { test, expect } from '@playwright/test';

// The drag engines (createSheetSwipe, createCarousel, createSortable) take pointer
// capture to own a gesture. Capturing at pointerdown retargets the compatibility mouse
// events — and `click` with them — to the capturing element, so every control inside
// the sheet, slide, or row goes dead. All three therefore wait for real travel before
// capturing. These tests pin both halves: the click still lands, and the drag still works.

// ----------------------------------------------------------------------- Drawer
test.describe('Drawer sheet: controls inside the panel', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/fidelity');
  });

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
  test.beforeEach(async ({ page }) => {
    await page.goto('/fidelity');
  });

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

// --------------------------------------------------------------------- Sortable
test.describe('Sortable rows: controls inside a row', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/sort');
    await expect(page.locator('[data-navius-sortable-id="Research"]')).toBeVisible({ timeout: 60_000 });
  });

  test('clicking a control inside a row fires it without reordering', async ({ page }) => {
    const row = page.locator('[data-navius-sortable-id="Research"]');

    // The demo rows carry no controls, so plant one — a delete/edit button per row is
    // the ordinary case for a sortable list.
    await row.evaluate((el) => {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.textContent = 'row action';
      btn.addEventListener('click', () => {
        (window as unknown as { __rowClicks?: number }).__rowClicks =
          ((window as unknown as { __rowClicks?: number }).__rowClicks ?? 0) + 1;
      });
      el.prepend(btn);
    });

    await row.getByRole('button', { name: 'row action' }).click();

    expect(await page.evaluate(() => (window as unknown as { __rowClicks?: number }).__rowClicks)).toBe(1);
    await expect(page.getByTestId('brain-order')).toHaveText('Research,Design,Build,Ship');
  });

  test('a press that never travels leaves the row undragged', async ({ page }) => {
    const row = page.locator('[data-navius-sortable-id="Research"]');
    const box = (await row.boundingBox())!;

    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await expect(row).not.toHaveAttribute('data-dragging', /.*/);
    await page.mouse.up();

    await expect(page.getByTestId('brain-order')).toHaveText('Research,Design,Build,Ship');
  });

  test('dragging a row still reorders the list', async ({ page }) => {
    const src = page.locator('[data-navius-sortable-id="Research"]');
    const dst = page.locator('[data-navius-sortable-id="Ship"]');
    const a = (await src.boundingBox())!;
    const b = (await dst.boundingBox())!;

    await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2);
    await page.mouse.down();
    await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2 + 6);
    await page.mouse.move(b.x + b.width / 2, b.y + b.height - 4, { steps: 10 });
    await page.mouse.move(b.x + b.width / 2, b.y + b.height + 12, { steps: 4 });
    await page.mouse.up();

    await expect(page.getByTestId('brain-order')).toHaveText('Design,Build,Ship,Research');
  });
});
