import { test, expect, Locator } from '@playwright/test';

// Fidelity showcase — the seven high-fidelity helm components on the Navius brain.
// These assert real behaviour (keyboard nav, filtering, sorting, paging, slide
// movement, drag-sheet dialog, range painting) THROUGH the styled wrappers.
// Selectors are the page's data-demo hooks + the components' own data-* / ARIA.
test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  await page.goto('/fidelity');
  await expect(page.locator('[data-fidelity-showcase]')).toBeVisible({ timeout: 60_000 });
});

// --------------------------------------------------------------------- Command
test.describe('Command (cmdk)', () => {
  test('ArrowDown then Enter selects the active item', async ({ page }) => {
    const cmd = page.locator('[data-demo="command"]');
    const input = cmd.locator('[role="combobox"]');

    await input.click();
    await expect(input).toHaveAttribute('aria-expanded', 'true');

    // Virtual focus: input keeps DOM focus while a highlight roves the items.
    await input.press('ArrowDown'); // first selectable (Calendar) -> Search Emoji
    await input.press('Enter');

    await expect(cmd.locator('[data-command-last]')).toHaveText('Search Emoji');
  });

  test('typing filters the option rows', async ({ page }) => {
    const cmd = page.locator('[data-demo="command"]');
    const options = cmd.locator('[role="option"]');

    await expect(options).toHaveCount(6);

    // "cal" matches only Calendar + Calculator (the disabled one still renders).
    await cmd.locator('[role="combobox"]').fill('cal');
    await expect(options).toHaveCount(2);
  });
});

// ------------------------------------------------------------------- DataTable
test.describe('DataTable', () => {
  test('the global filter reduces the visible rows', async ({ page }) => {
    const dt = page.locator('[data-demo="datatable"]');
    const rows = dt.locator('tbody tr');

    // 7 rows @ pageSize 5 -> a full first page.
    await expect(rows).toHaveCount(5);

    await dt.getByPlaceholder('Filter name...').fill('Alice');
    await expect(rows).toHaveCount(1);
  });

  test('clicking a sortable header reorders the rows', async ({ page }) => {
    const dt = page.locator('[data-demo="datatable"]');
    // td[0] is the selection checkbox; td[1] is the Name cell.
    const firstName = dt.locator('tbody tr').first().locator('td').nth(1);

    await expect(firstName).toHaveText('Eve'); // unsorted insertion order
    await dt.getByRole('button', { name: 'Name' }).click(); // -> ascending
    await expect(firstName).toHaveText('Alice');
  });

  test('pagination advances to the next page', async ({ page }) => {
    const dt = page.locator('[data-demo="datatable"]');

    await expect(dt.getByText(/Page\s*1\s*of\s*2/)).toBeVisible();
    await dt.getByRole('button', { name: 'Next', exact: true }).click();
    await expect(dt.getByText(/Page\s*2\s*of\s*2/)).toBeVisible();
  });

  test('the View menu hides a column', async ({ page }) => {
    const dt = page.locator('[data-demo="datatable"]');

    await expect(dt.getByRole('button', { name: 'Email' })).toBeVisible(); // header
    await dt.getByRole('button', { name: 'View' }).click();
    // The menu portals out of the table; toggle the Email column off.
    await page.getByRole('menuitemcheckbox', { name: 'Email' }).click();
    await expect(dt.getByRole('button', { name: 'Email' })).toHaveCount(0);
  });
});

// --------------------------------------------------------------------- Carousel
test.describe('Carousel (embla)', () => {
  // The engine publishes the current snap on the viewport as a CSS var.
  const selectedIndex = (viewport: Locator) =>
    viewport.evaluate((el) =>
      getComputedStyle(el).getPropertyValue('--navius-carousel-selected-index').trim()
    );

  test('Next advances the slide; both controls enabled when looping', async ({ page }) => {
    const car = page.locator('[data-demo="carousel"]');
    const next = car.getByRole('button', { name: 'Next slide' });
    const prev = car.getByRole('button', { name: 'Previous slide' });
    const viewport = car.locator('[data-carousel-viewport]');

    // The engine attaches asynchronously; with Loop both controls become enabled.
    await expect(next).toBeEnabled();
    await expect(prev).toBeEnabled();

    await expect.poll(() => selectedIndex(viewport), { timeout: 10_000 }).toBe('0');
    await next.click();
    await expect.poll(() => selectedIndex(viewport), { timeout: 10_000 }).toBe('1');
  });
});

// ----------------------------------------------------------------------- Drawer
test.describe('Drawer (brain primitive: modal sheet + drag-to-dismiss)', () => {
  test('the trigger opens the drawer sheet and Escape closes it', async ({ page }) => {
    await page.locator('[data-demo="drawer"]').getByRole('button', { name: 'Open Drawer' }).click();

    // Promoted to a real brain primitive: the Popup IS the draggable sheet.
    const panel = page.locator('[data-navius-drawer-popup]');
    await expect(panel).toBeVisible();
    await expect(panel).toHaveAttribute('role', 'dialog');
    await expect(panel).toHaveAttribute('aria-modal', 'true');
    await expect(panel).toHaveAttribute('data-drawer-direction', 'bottom');
    await expect(panel).toHaveAttribute('data-open', '');

    await page.keyboard.press('Escape');
    await expect(panel).toBeHidden();
  });
});

// --------------------------------------------------------------------- Calendar
test.describe('Calendar (range)', () => {
  test('selecting a start then a later day paints the range ends', async ({ page }) => {
    const cal = page.locator('[data-demo="calendar"]');
    const start = cal.locator('[data-day="2025-01-10"]');
    const end = cal.locator('[data-day="2025-01-20"]');

    await start.click();
    await end.click();

    await expect(start).toHaveAttribute('data-range-start', 'true');
    await expect(end).toHaveAttribute('data-range-end', 'true');
  });
});
