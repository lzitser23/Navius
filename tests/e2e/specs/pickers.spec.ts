import { test, expect } from '@playwright/test';

// Pickers (ColorPicker, FileUpload, Rating) exercised on the /pickers page.
test.beforeEach(async ({ page }) => {
  await page.goto('/pickers');
  await expect(page.locator('[data-navius-rating-item]').first()).toBeVisible({ timeout: 60_000 });
});

test('Rating: APG radio group, arrows check the star + move the roving tab stop', async ({ page }) => {
  const rating = page.locator('[data-testid="rating"]');
  const items = rating.locator('[data-navius-rating-item]');
  const value = rating.locator('[data-rating-value]');

  await expect(rating.getByRole('radiogroup')).toBeVisible();
  await expect(items).toHaveCount(5);
  await expect(items.first()).toHaveAttribute('role', 'radio');

  // Unrated: nothing checked; the first star is the single tab stop.
  await expect(value).toHaveText('none');
  await expect(items.nth(0)).toHaveAttribute('aria-checked', 'false');
  await expect(items.nth(0)).toHaveAttribute('tabindex', '0');
  await expect(items.nth(1)).toHaveAttribute('tabindex', '-1');

  await items.nth(0).focus();
  await page.keyboard.press('ArrowRight'); // -> 1
  await expect(items.nth(0)).toHaveAttribute('aria-checked', 'true');
  await expect(value).toHaveText('1');

  await page.keyboard.press('ArrowRight'); // -> 2
  await expect(items.nth(1)).toHaveAttribute('aria-checked', 'true');
  await expect(items.nth(0)).toHaveAttribute('aria-checked', 'false');
  // Roving tabindex followed the checked star.
  await expect(items.nth(1)).toHaveAttribute('tabindex', '0');
  await expect(items.nth(0)).toHaveAttribute('tabindex', '-1');
  await expect(value).toHaveText('2');

  await page.keyboard.press('End'); // -> Max
  await expect(items.nth(4)).toHaveAttribute('aria-checked', 'true');
  await expect(value).toHaveText('5');

  await page.keyboard.press('Home'); // -> 1
  await expect(items.nth(0)).toHaveAttribute('aria-checked', 'true');
  await expect(value).toHaveText('1');
});

test('Rating: clicking a star selects it; clicking the current value clears it (AllowClear)', async ({ page }) => {
  const rating = page.locator('[data-testid="rating"]');
  const items = rating.locator('[data-navius-rating-item]');
  const value = rating.locator('[data-rating-value]');

  await items.nth(2).click(); // star 3
  await expect(value).toHaveText('3');
  await expect(items.nth(2)).toHaveAttribute('aria-checked', 'true');

  await items.nth(2).click(); // re-click clears
  await expect(value).toHaveText('none');
  await expect(items.nth(2)).toHaveAttribute('aria-checked', 'false');
});

test('ColorPicker: the area thumb is a slider and ArrowRight changes the color', async ({ page }) => {
  const cp = page.locator('[data-testid="colorpicker"]');
  const thumb = cp.locator('[data-navius-color-picker-area-thumb]');
  const value = cp.locator('[data-color-value]');

  await expect(cp.locator('[data-navius-color-picker-area]')).toHaveAttribute('role', 'group');
  await expect(thumb).toHaveAttribute('role', 'slider');
  await expect(thumb).toHaveAttribute('aria-valuetext', /hue/);

  const before = await value.textContent();
  await thumb.focus();
  await page.keyboard.press('ArrowRight'); // saturation up
  await expect.poll(async () => value.textContent()).not.toBe(before);
});

test('ColorPicker: a pointer drag across the area moves the color', async ({ page }) => {
  const cp = page.locator('[data-testid="colorpicker"]');
  const area = cp.locator('[data-navius-color-picker-area]');
  const value = cp.locator('[data-color-value]');

  const before = await value.textContent();
  const box = await area.boundingBox();
  if (!box) throw new Error('area has no box');

  await page.mouse.move(box.x + 6, box.y + 6);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width - 6, box.y + box.height - 6, { steps: 4 });
  await page.mouse.up();

  await expect.poll(async () => value.textContent()).not.toBe(before);
});

test('FileUpload: the dropzone reflects drag-over state via data-dragging', async ({ page }) => {
  const fu = page.locator('[data-testid="fileupload"]');
  const dropzone = fu.locator('[data-navius-file-upload-dropzone]');

  await expect(dropzone).toHaveAttribute('role', 'button');
  await expect(dropzone).not.toHaveAttribute('data-dragging', /.*/);

  // Synthetic drag over the dropzone bubbles to the engine's listeners on the root.
  await dropzone.dispatchEvent('dragenter', { bubbles: true });
  await dropzone.dispatchEvent('dragover', { bubbles: true });
  await expect(dropzone).toHaveAttribute('data-dragging', '');

  await dropzone.dispatchEvent('dragleave', { bubbles: true });
  await expect(dropzone).not.toHaveAttribute('data-dragging', /.*/);
});

test('FileUpload: selecting a file via the hidden input renders a removable row', async ({ page }) => {
  const fu = page.locator('[data-testid="fileupload"]');
  const input = fu.locator('[data-navius-file-upload-input]');

  await input.setInputFiles({
    name: 'hello.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('hi'),
  });

  const item = fu.locator('[data-navius-file-upload-item]');
  await expect(item).toHaveCount(1);
  await expect(item.locator('[data-navius-file-upload-item-name]')).toHaveText('hello.txt');
  await expect(item.locator('[data-navius-file-upload-item-size]')).toContainText('B');

  // A polite status announces the addition.
  await expect(fu.locator('[data-navius-file-upload-status]')).toContainText('added');

  await item.locator('[data-navius-file-upload-item-delete]').click();
  await expect(fu.locator('[data-navius-file-upload-item]')).toHaveCount(0);
});
