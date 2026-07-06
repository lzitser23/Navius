import { test, expect, Page } from '@playwright/test';

// Sortable, exercised on the /sort page: headless brain (engine pointer drag + APG
// grab-and-move keyboard), the handle-scoped variant, and the AutoAnimate pairing.
test.beforeEach(async ({ page }) => {
  await page.goto('/sort');
  await expect(page.locator('[data-navius-sortable-id="Research"]')).toBeVisible({ timeout: 60_000 });
});

// Drag the center of `from` to just past the bottom edge of `to` (a real pointer drag).
async function dragPast(page: Page, from: string, to: string) {
  const src = page.locator(`[data-navius-sortable-id="${from}"]`);
  const dst = page.locator(`[data-navius-sortable-id="${to}"]`);
  const a = (await src.boundingBox())!;
  const b = (await dst.boundingBox())!;
  await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2);
  await page.mouse.down();
  await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2 + 6); // nudge -> the engine starts the drag
  await page.mouse.move(b.x + b.width / 2, b.y + b.height - 4, { steps: 10 });
  await page.mouse.move(b.x + b.width / 2, b.y + b.height + 12, { steps: 4 }); // below the last midpoint
  await page.mouse.up();
}

test('Sortable: item + list expose the ARIA + data-attribute shape', async ({ page }) => {
  const list = page.locator('[data-navius-sortable]').first();
  await expect(list).toHaveAttribute('role', 'list');
  await expect(list).toHaveAttribute('data-orientation', 'vertical');

  const item = page.locator('[data-navius-sortable-id="Research"]');
  await expect(item).toHaveAttribute('data-navius-sortable-item', '');
  await expect(item).toHaveAttribute('role', 'listitem');
  await expect(item).toHaveAttribute('aria-roledescription', 'sortable item');

  // The polite live region for keyboard announcements exists.
  await expect(page.locator('[data-navius-sortable-status]').first()).toBeAttached();
});

test('Sortable: pointer drag reorders the list (drag first past last)', async ({ page }) => {
  await expect(page.getByTestId('brain-order')).toHaveText('Research,Design,Build,Ship');
  await dragPast(page, 'Research', 'Ship');
  await expect(page.getByTestId('brain-order')).toHaveText('Design,Build,Ship,Research');
  await expect(page.getByTestId('brain-reorder')).toHaveText('0 -> 3');
});

test('Sortable: pointer drag paints data-dragging on the item and data-drop-target on the slot', async ({ page }) => {
  const research = page.locator('[data-navius-sortable-id="Research"]');
  const design = page.locator('[data-navius-sortable-id="Design"]');
  const a = (await research.boundingBox())!;
  const b = (await design.boundingBox())!;

  await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2);
  await page.mouse.down();
  await expect(research).toHaveAttribute('data-dragging', ''); // set on pointerdown
  await page.mouse.move(b.x + b.width / 2, b.y + b.height - 4, { steps: 8 });
  // exactly one slot is marked in the brain section while hovering
  await expect(page.locator('[data-testid="brain-vertical"] [data-drop-target]')).toHaveCount(1);
  await page.mouse.up();

  // Cleared after drop.
  await expect(research).not.toHaveAttribute('data-dragging', /.*/);
  await expect(page.locator('[data-testid="brain-vertical"] [data-drop-target]')).toHaveCount(0);
});

test('Sortable: keyboard grab -> move -> drop reorders and announces', async ({ page }) => {
  const research = page.locator('[data-navius-sortable-id="Research"]');
  const status = page.locator('[data-navius-sortable-status]').first();

  await research.focus();
  await page.keyboard.press('Space'); // grab
  await expect(research).toHaveAttribute('data-keyboard-grabbed', '');
  await expect(research).toHaveAttribute('aria-grabbed', 'true');
  await expect(status).toContainText('Grabbed Research');

  await page.keyboard.press('ArrowDown'); // move down one (live)
  await expect(page.getByTestId('brain-order')).toHaveText('Design,Research,Build,Ship');
  await expect(status).toContainText('Moved Research to position 2 of 4');

  await page.keyboard.press('Space'); // drop
  await expect(research).not.toHaveAttribute('data-keyboard-grabbed', /.*/);
  await expect(page.getByTestId('brain-order')).toHaveText('Design,Research,Build,Ship');
  await expect(page.getByTestId('brain-reorder')).toHaveText('0 -> 1');
  await expect(status).toContainText('Dropped Research');
});

test('Sortable: Escape cancels a keyboard reorder and restores the original order', async ({ page }) => {
  const research = page.locator('[data-navius-sortable-id="Research"]');

  await research.focus();
  await page.keyboard.press('Space');
  await page.keyboard.press('ArrowDown');
  await expect(page.getByTestId('brain-order')).toHaveText('Design,Research,Build,Ship'); // moved live

  await page.keyboard.press('Escape');
  await expect(page.getByTestId('brain-order')).toHaveText('Research,Design,Build,Ship'); // restored
  await expect(research).not.toHaveAttribute('data-keyboard-grabbed', /.*/);
  await expect(page.getByTestId('brain-reorder')).toHaveText('none'); // no commit fired
});

test('Sortable (handle): only the handle starts a pointer drag; the row body does not', async ({ page }) => {
  await expect(page.getByTestId('handle-order')).toHaveText('Draft,Review,Merge');

  // Pressing the row body (right side, away from the left grip) must NOT start a drag.
  const draft = page.locator('[data-navius-sortable-id="Draft"]');
  const bb = (await draft.boundingBox())!;
  await page.mouse.move(bb.x + bb.width - 8, bb.y + bb.height / 2);
  await page.mouse.down();
  await page.mouse.move(bb.x + bb.width - 8, bb.y + bb.height * 2.5, { steps: 8 });
  await page.mouse.up();
  await expect(page.getByTestId('handle-order')).toHaveText('Draft,Review,Merge'); // unchanged

  // Dragging by the grip reorders.
  const handle = draft.locator('[data-navius-sortable-handle]');
  const merge = page.locator('[data-navius-sortable-id="Merge"]');
  const hb = (await handle.boundingBox())!;
  const mb = (await merge.boundingBox())!;
  await page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2);
  await page.mouse.down();
  await page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2 + 6);
  await page.mouse.move(mb.x + mb.width / 2, mb.y + mb.height + 12, { steps: 10 });
  await page.mouse.up();
  await expect(page.getByTestId('handle-order')).toHaveText('Review,Merge,Draft');
});

test('Sortable (AutoAnimate pairing): keyboard reorder composes with the FLIP list', async ({ page }) => {
  const one = page.locator('[data-navius-sortable-id="One"]');
  await one.focus();
  await page.keyboard.press('Space');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Space');
  await expect(page.getByTestId('anim-order')).toHaveText('Two,One,Three,Four');
});
