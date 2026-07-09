import { test, expect } from '@playwright/test';

// Roving-focus keyboard activation — regression cover for the accessibility-audit fixes:
//  - Space activates native-button roving items (ToggleGroup, Toolbar) again (the shared
//    roving handler no longer blanket-preventDefaults Space on native controls).
//  - div-based menu items still activate on Space/Enter (preventDefault stays for them).
//  - Menubar ArrowUp on a closed trigger opens focused on the LAST item.
//  - Toolbar no longer steals focus on mount.
//  - NavigationMenu (native button + its own C# activation) stays a single toggle.

test('ToggleGroup: Space toggles a native-button item (single-select swap)', async ({ page }) => {
  await page.goto('/wave1');
  const items = page.locator('[data-navius-togglegroup-item]');
  await expect(items.nth(1)).toHaveAttribute('data-pressed', ''); // "center" default

  await items.nth(0).focus();
  await page.keyboard.press(' ');
  await expect(items.nth(0)).toHaveAttribute('data-pressed', ''); // Space activated it
  await expect(items.nth(1)).not.toHaveAttribute('data-pressed', /.*/); // single-select swapped
});

test('Toolbar: Space activates a toggle item', async ({ page }) => {
  await page.goto('/wave2');
  // Only the ToggleGroup items expose aria-pressed among the toolbar items.
  const toggle = page.locator('[data-navius-toolbar-item][aria-pressed]').first();
  await expect(toggle).toHaveAttribute('aria-pressed', 'false');

  await toggle.focus();
  await page.keyboard.press(' ');
  await expect(toggle).toHaveAttribute('aria-pressed', 'true');
  await expect(toggle).toHaveAttribute('data-pressed', '');
});

test('Toolbar: does not steal focus on mount', async ({ page }) => {
  await page.goto('/wave2');
  const firstItem = page.locator('[data-navius-toolbar-item]').first();
  await expect(firstItem).toBeVisible();
  // AutoFocus:false — no toolbar item should be focused just from rendering the page.
  await expect(firstItem).not.toBeFocused();
  const toolbarHasFocus = await page.evaluate(() =>
    !!document.activeElement?.closest('[data-navius-toolbar]'),
  );
  expect(toolbarHasFocus).toBe(false);
});

test('Menu item (div-based): still activates + closes on Enter and Space', async ({ page }) => {
  await page.goto('/wave2');
  const fileTrigger = page.locator('[data-navius-menubar-trigger]').nth(0);
  const popup = page.locator('[data-navius-menubar-popup]');

  // Enter activates the focused first item (menu closes when the item does not preventDefault).
  await fileTrigger.click();
  await expect(popup).toBeVisible();
  await expect(popup.locator('[role="menuitem"]').first()).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(popup).toBeHidden();

  // Space activates the same way (div item keeps its scroll-suppressing preventDefault).
  await fileTrigger.click();
  await expect(popup).toBeVisible();
  await expect(popup.locator('[role="menuitem"]').first()).toBeFocused();
  await page.keyboard.press(' ');
  await expect(popup).toBeHidden();
});

test('Menubar: ArrowUp on a closed trigger opens focused on the LAST item', async ({ page }) => {
  await page.goto('/wave2');
  const fileTrigger = page.locator('[data-navius-menubar-trigger]').nth(0);
  const popup = page.locator('[data-navius-menubar-popup]');

  await fileTrigger.focus();
  await page.keyboard.press('ArrowUp');
  await expect(popup).toBeVisible();

  const items = popup.locator('[role="menuitem"]');
  await expect(items.last()).toBeFocused();
  await expect(items.first()).not.toBeFocused();
});

test('Menubar: ArrowDown on a closed trigger opens focused on the FIRST item', async ({ page }) => {
  await page.goto('/wave2');
  const fileTrigger = page.locator('[data-navius-menubar-trigger]').nth(0);
  const popup = page.locator('[data-navius-menubar-popup]');

  await fileTrigger.focus();
  await page.keyboard.press('ArrowDown');
  await expect(popup).toBeVisible();
  await expect(popup.locator('[role="menuitem"]').first()).toBeFocused();
});

test('Menubar: Space on the trigger opens the menu exactly once', async ({ page }) => {
  // The trigger is a native <button>; after the roving handler stops blanket-preventing Space,
  // the trigger's own @onkeydown:preventDefault must still suppress the synthesized native click
  // so Space opens (not open-then-close).
  await page.goto('/wave2');
  const fileTrigger = page.locator('[data-navius-menubar-trigger]').nth(0);
  const popup = page.locator('[data-navius-menubar-popup]');

  await fileTrigger.focus();
  await page.keyboard.press(' ');
  await expect(popup).toBeVisible();
  await expect(fileTrigger).toHaveAttribute('aria-expanded', 'true');
});

test('NavigationMenu: Space stays a single toggle (native button + C# activation)', async ({ page }) => {
  await page.goto('/wave3');
  const trigger = page.locator('[data-navius-navigationmenu-trigger]').first();

  await trigger.focus(); // focus opens the menu
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');

  // Space is a single toggle -> closes. A double-fire (native click + C# handler) would be a
  // no-op and leave it open, so this guards the fix that makes the trigger self-preventDefault.
  await page.keyboard.press(' ');
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');
});
