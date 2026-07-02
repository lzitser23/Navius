import { test, expect, Page } from '@playwright/test';

// Each test starts fresh; wait for the Blazor WASM app to hydrate (the first
// trigger becoming visible is our "ready" signal).
test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('[data-navius-dialog-trigger]')).toBeVisible({ timeout: 60_000 });
});

const isActiveInside = (page: Page, selector: string) =>
  page.evaluate(
    (sel) => document.querySelector(sel)?.contains(document.activeElement) ?? false,
    selector
  );

test.describe('Dialog (modal: focus-trap + scroll-lock)', () => {
  test('opens, traps focus, locks scroll, restores on Esc', async ({ page }) => {
    const trigger = page.locator('[data-navius-dialog-trigger]');
    const content = page.locator('[data-navius-dialog-popup]');

    await trigger.click();
    await expect(content).toBeVisible();
    await expect(content).toHaveAttribute('role', 'dialog');
    await expect(content).toHaveAttribute('aria-modal', 'true');

    // aria-labelledby wires to the mounted Title (re-rendered after the child registers)
    const labelledBy = await content.getAttribute('aria-labelledby');
    expect(labelledBy).toBeTruthy();
    await expect(page.locator('[data-navius-dialog-title]')).toHaveAttribute('id', labelledBy!);

    // focus moved into the dialog
    expect(await isActiveInside(page, '[data-navius-dialog-popup]')).toBe(true);

    // body scroll locked
    await expect(page.locator('body')).toHaveCSS('overflow', 'hidden');

    // focus stays trapped after several Tabs
    for (let i = 0; i < 6; i++) await page.keyboard.press('Tab');
    expect(await isActiveInside(page, '[data-navius-dialog-popup]')).toBe(true);

    // Esc closes, focus returns to trigger, scroll unlocked
    await page.keyboard.press('Escape');
    await expect(content).toBeHidden();
    await expect(trigger).toBeFocused();
    await expect(page.locator('body')).toHaveCSS('overflow', 'visible');
  });

  test('backdrop click closes', async ({ page }) => {
    await page.locator('[data-navius-dialog-trigger]').click();
    await expect(page.locator('[data-navius-dialog-popup]')).toBeVisible();
    // click the overlay in a corner, away from the centered content
    await page.locator('[data-navius-dialog-backdrop]').click({ position: { x: 5, y: 5 } });
    await expect(page.locator('[data-navius-dialog-popup]')).toBeHidden();
  });
});

test.describe('Popover (anchored + dismissable)', () => {
  test('opens anchored, dismisses on outside click and Esc', async ({ page }) => {
    const trigger = page.locator('[data-navius-popover-trigger]');
    // Base UI splits Content -> Positioner (placement) + Popup (panel).
    const positioner = page.locator('[data-navius-popover-positioner]');
    const popup = page.locator('[data-navius-popover-popup]');

    await trigger.click();
    await expect(popup).toBeVisible();
    // Base UI: the trigger carries discrete data-popup-open (present only when open).
    await expect(trigger).toHaveAttribute('data-popup-open', '');
    // the engine positions it: fixed + a computed transform + a resolved side
    await expect(positioner).toHaveCSS('position', 'fixed');
    await expect(positioner).toHaveAttribute('data-side', /top|bottom|left|right/);
    // the popup carries discrete data-open (not the legacy data-state token)
    await expect(popup).toHaveAttribute('data-open', '');
    await expect(popup).not.toHaveAttribute('data-state', /.*/);

    // outside click closes
    await page.mouse.click(5, 5);
    await expect(popup).toBeHidden();
    await expect(trigger).not.toHaveAttribute('data-popup-open', '');

    // reopen, Esc closes
    await trigger.click();
    await expect(popup).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(popup).toBeHidden();
  });
});

test.describe('Tooltip (hover/focus intent + positioner)', () => {
  test('shows on hover and wires aria-describedby', async ({ page }) => {
    const trigger = page.locator('[data-navius-tooltip-trigger]');
    // Base UI splits Content -> Positioner + Popup; role=tooltip lives on the Popup.
    const popup = page.locator('[data-navius-tooltip-popup]');

    await trigger.hover();
    await expect(popup).toBeVisible();
    await expect(popup).toHaveAttribute('role', 'tooltip');
    // discrete presence attrs replace the data-state token
    await expect(popup).toHaveAttribute('data-open', '');
    await expect(popup).not.toHaveAttribute('data-state', /.*/);
    await expect(trigger).toHaveAttribute('data-popup-open', '');

    const contentId = await popup.getAttribute('id');
    await expect(trigger).toHaveAttribute('aria-describedby', contentId!);

    // moving away hides it
    await page.mouse.move(0, 0);
    await expect(popup).toBeHidden();
  });

  test('shows on keyboard focus immediately (data-instant)', async ({ page }) => {
    await page.locator('[data-navius-tooltip-trigger]').focus();
    const popup = page.locator('[data-navius-tooltip-popup]');
    await expect(popup).toBeVisible();
    // focus opens instantly -> Base UI data-instant
    await expect(popup).toHaveAttribute('data-instant', '');
  });
});

test.describe('Menu (roving tabindex)', () => {
  test('arrow keys move focus and skip the disabled item', async ({ page }) => {
    const content = page.locator('[data-navius-menu-popup]');
    await page.locator('[data-navius-menu-trigger]').click();
    await expect(content).toBeVisible();
    await expect(content).toHaveAttribute('role', 'menu');

    // first item is focused on open
    await expect(page.getByRole('menuitem', { name: 'Profile' })).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('menuitem', { name: 'Billing' })).toBeFocused();

    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('menuitem', { name: 'Settings' })).toBeFocused();

    // ArrowDown skips the disabled item and lands on "Log out"
    await page.keyboard.press('ArrowDown');
    await expect(page.getByRole('menuitem', { name: 'Log out' })).toBeFocused();

    // Home jumps back to the first
    await page.keyboard.press('Home');
    await expect(page.getByRole('menuitem', { name: 'Profile' })).toBeFocused();
  });

  test('selecting an item closes the menu and restores focus', async ({ page }) => {
    const trigger = page.locator('[data-navius-menu-trigger]');
    await trigger.click();
    await expect(page.locator('[data-navius-menu-popup]')).toBeVisible();
    await page.getByRole('menuitem', { name: 'Billing' }).click();
    await expect(page.locator('[data-navius-menu-popup]')).toBeHidden();
    await expect(trigger).toBeFocused();
  });
});

test.describe('Switch & Checkbox (pure-C# form primitives)', () => {
  test('switch toggles via click and Space', async ({ page }) => {
    const sw = page.locator('[data-navius-switch]');
    await expect(sw).toHaveAttribute('aria-checked', 'false');

    await sw.click();
    await expect(sw).toHaveAttribute('aria-checked', 'true');
    await expect(sw).toHaveAttribute('data-checked', ''); // Base UI discrete

    await sw.focus();
    await page.keyboard.press('Space');
    await expect(sw).toHaveAttribute('aria-checked', 'false');
    await expect(sw).toHaveAttribute('data-unchecked', '');
  });

  test('checkbox honours DefaultChecked and toggles off', async ({ page }) => {
    const cb = page.locator('[data-navius-checkbox]');
    await expect(cb).toHaveAttribute('aria-checked', 'true'); // DefaultChecked="true"
    await expect(cb).toHaveAttribute('data-checked', '');
    await cb.click();
    await expect(cb).toHaveAttribute('aria-checked', 'false');
    await expect(cb).toHaveAttribute('data-unchecked', '');
    await expect(cb).not.toHaveAttribute('data-checked', /.*/);
  });
});

test.describe('Portal (teleport)', () => {
  test('renders the panel OUTSIDE the overflow-hidden box in the real DOM', async ({ page }) => {
    const panel = page.getByText('Teleported via', { exact: false });
    await expect(panel).toBeVisible();

    // The panel is authored inside the clipped box but must render at the outlet.
    const escapedClip = await page.evaluate(() => {
      const box = Array.from(document.querySelectorAll('div')).find((d) =>
        d.className.includes('overflow-hidden')
      );
      const teleported = Array.from(document.querySelectorAll('div')).find((d) =>
        (d.textContent || '').includes('Teleported via')
      );
      if (!box || !teleported) return false;
      return !box.contains(teleported); // true == it escaped the clip
    });
    expect(escapedClip).toBe(true);
  });
});

test.describe('Autocomplete (filter + virtual focus)', () => {
  test('filters, ArrowDown moves aria-activedescendant, Enter selects', async ({ page }) => {
    const input = page.locator('[data-navius-autocomplete-input]');
    const list = page.locator('[data-navius-autocomplete-list]');

    await input.click();
    await expect(list).toBeVisible();
    await expect(input).toHaveAttribute('role', 'combobox');
    await expect(input).toHaveAttribute('aria-expanded', 'true');

    // type to filter — Apple, Apricot, Grape contain "ap"
    await input.fill('ap');
    await expect(page.locator('[data-navius-autocomplete-item]')).toHaveCount(3);

    // VIRTUAL focus: focus stays in the input, not on an option
    await expect(input).toBeFocused();

    // ArrowDown advances the active descendant and marks it data-highlighted
    await page.keyboard.press('ArrowDown');
    const activeId = await input.getAttribute('aria-activedescendant');
    expect(activeId).toBeTruthy();
    await expect(page.locator(`#${activeId}`)).toHaveAttribute('data-highlighted', '');

    // Enter selects -> value fills the input, list closes
    await page.keyboard.press('Enter');
    await expect(list).toBeHidden();
    const chosen = await input.inputValue();
    expect(['Apple', 'Apricot', 'Grape']).toContain(chosen);

    // reopen: the chosen row is marked data-selected
    await input.click();
    await expect(list).toBeVisible();
    await expect(page.locator('[data-navius-autocomplete-item][data-selected]')).toHaveCount(1);
  });

  test('outside click and Escape close the listbox', async ({ page }) => {
    const input = page.locator('[data-navius-autocomplete-input]');
    const list = page.locator('[data-navius-autocomplete-list]');

    await input.click();
    await expect(list).toBeVisible();
    await page.mouse.click(5, 5);
    await expect(list).toBeHidden();

    await input.click();
    await expect(list).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(list).toBeHidden();
  });
});

test.describe('Combobox (value select + multi chips)', () => {
  const list = (page: Page) => page.locator('[data-navius-combobox-list]');

  test('single-select: filter narrows, ArrowDown+Enter selects (data-selected + indicator), closes with the label', async ({ page }) => {
    const input = page.locator('[data-testid="combobox-single"] [data-navius-combobox-input]');

    await input.click();
    await expect(list(page)).toBeVisible();
    await expect(input).toHaveAttribute('role', 'combobox');
    await expect(input).toHaveAttribute('aria-expanded', 'true');

    // 12 countries; typing "aus" narrows to Australia, Austria — the filter drives the list
    await expect(page.locator('[data-navius-combobox-item]')).toHaveCount(12);
    await input.fill('aus');
    await expect(page.locator('[data-navius-combobox-item]')).toHaveCount(2);

    // VIRTUAL focus: focus stays in the input, not on an option
    await expect(input).toBeFocused();

    // ArrowDown advances the active descendant and marks it data-highlighted
    await page.keyboard.press('ArrowDown');
    const activeId = await input.getAttribute('aria-activedescendant');
    expect(activeId).toBeTruthy();
    await expect(page.locator(`#${activeId}`)).toHaveAttribute('data-highlighted', '');

    // Enter selects -> single-select closes and the input shows the committed LABEL
    await page.keyboard.press('Enter');
    await expect(list(page)).toBeHidden();
    const chosen = await input.inputValue();
    expect(['Australia', 'Austria']).toContain(chosen);

    // reopen: exactly one row is data-selected and its ItemIndicator is mounted
    await input.click();
    await expect(list(page)).toBeVisible();
    const selected = page.locator('[data-navius-combobox-item][data-selected]');
    await expect(selected).toHaveCount(1);
    await expect(selected.locator('[data-navius-combobox-item-indicator]')).toBeVisible();
  });

  test('single-select: outside/Escape close; the committed value is separate from the filter text', async ({ page }) => {
    const input = page.locator('[data-testid="combobox-single"] [data-navius-combobox-input]');

    // commit a value
    await input.click();
    await input.fill('Canada');
    await expect(page.locator('[data-navius-combobox-item]')).toHaveCount(1);
    await page.locator('[data-navius-combobox-item]').first().click();
    await expect(list(page)).toBeHidden();
    await expect(input).toHaveValue('Canada');

    // type junk, then close WITHOUT selecting: committed value unchanged + input reverts
    await input.click();
    await expect(list(page)).toBeVisible();
    await input.fill('zzzz');
    await expect(page.locator('[data-navius-combobox-empty]')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(list(page)).toBeHidden();
    await expect(input).toHaveValue('Canada'); // reverted; the filter text never became the value

    // outside pointer-down also closes
    await input.click();
    await expect(list(page)).toBeVisible();
    await page.mouse.click(5, 5);
    await expect(list(page)).toBeHidden();
    await expect(input).toHaveValue('Canada');
  });

  test('multi-select: toggling adds a chip and keeps the popup open; Backspace removes the last; Clear empties', async ({ page }) => {
    const root = page.locator('[data-testid="combobox-multi"]');
    const input = root.locator('[data-navius-combobox-input]');
    const chips = root.locator('[data-navius-combobox-chip]');

    await input.click();
    await expect(list(page)).toBeVisible();

    // select Brazil -> a chip appears, the popup STAYS open, the filter is cleared
    await input.fill('Bra');
    await page.locator('[data-navius-combobox-item]', { hasText: 'Brazil' }).click();
    await expect(list(page)).toBeVisible();
    await expect(input).toHaveValue('');
    await expect(chips).toHaveCount(1);
    await expect(chips.first()).toContainText('Brazil');

    // select a second value -> two chips; both rows carry data-selected under the empty filter
    await input.fill('Chi');
    await page.locator('[data-navius-combobox-item]', { hasText: 'Chile' }).click();
    await expect(chips).toHaveCount(2);
    await expect(page.locator('[data-navius-combobox-item][data-selected]')).toHaveCount(2);

    // Backspace on the empty input removes the LAST chip (Chile)
    await input.press('Backspace');
    await expect(chips).toHaveCount(1);
    await expect(chips.first()).toContainText('Brazil');

    // Clear empties the whole selection
    await root.locator('[data-navius-combobox-clear]').click();
    await expect(chips).toHaveCount(0);
  });

  test('multi-select: removing a NON-tail chip via its × removes the right value and keeps the rest removable', async ({ page }) => {
    const root = page.locator('[data-testid="combobox-multi"]');
    const input = root.locator('[data-navius-combobox-input]');
    const chips = root.locator('[data-navius-combobox-chip]');

    await input.click();
    await page.locator('[data-navius-combobox-item]', { hasText: 'Brazil' }).click();
    await page.locator('[data-navius-combobox-item]', { hasText: 'Chile' }).click();
    await page.locator('[data-navius-combobox-item]', { hasText: 'Canada' }).click();
    await expect(chips).toHaveCount(3);

    // remove the FIRST (non-tail) chip, Brazil, via its own × button
    await chips.filter({ hasText: 'Brazil' }).locator('[data-navius-combobox-chip-remove]').click();
    await expect(chips).toHaveCount(2);
    await expect(root.locator('[data-navius-combobox-chip]', { hasText: 'Brazil' })).toHaveCount(0);

    // the surviving chips still target their OWN values (regression guard for stale chip context):
    // removing the one labelled "Canada" must leave exactly "Chile"
    await chips.filter({ hasText: 'Canada' }).locator('[data-navius-combobox-chip-remove]').click();
    await expect(chips).toHaveCount(1);
    await expect(chips.first()).toContainText('Chile');
  });
});

test.describe('Tabs', () => {
  test('selects on click and ArrowRight wraps, wiring aria-selected + panel', async ({ page }) => {
    const account = page.getByRole('tab', { name: 'Account' });
    const password = page.getByRole('tab', { name: 'Password' });

    await expect(account).toHaveAttribute('aria-selected', 'true');
    await expect(account).toHaveAttribute('data-active', '');
    await expect(account).not.toHaveAttribute('data-state', /.*/);
    await expect(page.getByRole('tabpanel')).toContainText('Account settings');

    await password.click();
    await expect(password).toHaveAttribute('aria-selected', 'true');
    await expect(account).toHaveAttribute('aria-selected', 'false');
    await expect(page.getByRole('tabpanel')).toContainText('password');

    // arrow keys: from the last tab, ArrowRight wraps to the first and selects it
    await password.focus();
    await page.keyboard.press('ArrowRight');
    await expect(account).toBeFocused();
    await expect(account).toHaveAttribute('aria-selected', 'true');
  });
});

test.describe('Accordion (single)', () => {
  test('default item open; opening another closes the first', async ({ page }) => {
    const t1 = page.getByRole('button', { name: 'Is it accessible?' });
    const t2 = page.getByRole('button', { name: 'Is it styled?' });

    await expect(t1).toHaveAttribute('aria-expanded', 'true');
    await expect(t2).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByText('ARIA wired')).toBeVisible();

    await t2.click();
    await expect(t2).toHaveAttribute('aria-expanded', 'true');
    await expect(t1).toHaveAttribute('aria-expanded', 'false'); // single-type closes the other
    await expect(page.getByText('you bring the styles')).toBeVisible();
    await expect(page.getByText('ARIA wired')).toBeHidden();
  });
});

test.describe('Select (listbox)', () => {
  test('opens, selects an option, reflects the value, reopens on the selection', async ({ page }) => {
    // Two select triggers share the page now; scope to the single-select (non-multiple) one.
    const trigger = page.locator('[data-navius-select-trigger]:not([data-multiple])');
    const content = page.locator('[data-navius-select-popup]:not([data-multiple])');
    const value = trigger.locator('[data-navius-select-value]');

    await expect(value).toContainText('Select a fruit');

    await trigger.click();
    await expect(content).toBeVisible();
    await expect(content).toHaveAttribute('role', 'listbox');

    await content.getByRole('option', { name: 'Cherry' }).click();
    await expect(content).toBeHidden();
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await expect(value).toContainText('Cherry');
    await expect(trigger).toBeFocused();

    // reopen: roving focus lands on the selected option, which is aria-selected
    await trigger.click();
    const cherry = content.getByRole('option', { name: 'Cherry' });
    await expect(cherry).toHaveAttribute('aria-selected', 'true');
    await expect(cherry).toBeFocused();
  });

  test('closed-trigger ArrowUp opens the listbox on the LAST option; ArrowDown on the first', async ({ page }) => {
    const trigger = page.locator('[data-navius-select-trigger]:not([data-multiple])');
    const content = page.locator('[data-navius-select-popup]:not([data-multiple])');
    const options = content.getByRole('option');

    // ArrowUp on the closed trigger opens + lands focus on the LAST option (not first/selected)
    await trigger.focus();
    await page.keyboard.press('ArrowUp');
    await expect(content).toBeVisible();
    await expect(options.last()).toBeFocused();

    // Esc closes; ArrowDown then opens landing on the FIRST option
    await page.keyboard.press('Escape');
    await expect(content).toBeHidden();
    await trigger.focus();
    await page.keyboard.press('ArrowDown');
    await expect(content).toBeVisible();
    await expect(options.first()).toBeFocused();
  });

  test('multi: toggling options adds/removes data-selected, keeps the popup open + keyboard-navigable', async ({ page }) => {
    // Two select triggers now share the page: scope to the multi one via data-multiple.
    const trigger = page.locator('[data-navius-select-trigger][data-multiple]');
    const popup = page.locator('[data-navius-select-popup][data-multiple]');
    const value = trigger.locator('[data-navius-select-value]');

    await trigger.click();
    await expect(popup).toBeVisible();
    await expect(popup).toHaveAttribute('aria-multiselectable', 'true');

    const apple = popup.getByRole('option', { name: 'Apple' });
    const banana = popup.getByRole('option', { name: 'Banana' });

    await apple.click();
    await expect(apple).toHaveAttribute('data-selected', '');
    await expect(popup).toBeVisible(); // stays open on select

    await banana.click();
    await expect(banana).toHaveAttribute('data-selected', '');
    await expect(value).toContainText('Apple'); // summary reflects the set
    await expect(value).toContainText('Banana');

    // Roving focus survived the toggle re-renders: keyboard still navigates the open popup.
    // _fruits = [Apple, Apricot, Banana, ...], so ArrowUp from Banana lands on Apricot.
    const apricot = popup.getByRole('option', { name: 'Apricot', exact: true });
    await banana.focus();
    await page.keyboard.press('ArrowUp');
    await expect(apricot).toBeFocused();

    await apple.click(); // toggle off
    await expect(apple).not.toHaveAttribute('data-selected', '');
    await expect(popup).toBeVisible();
  });
});

test.describe('Toast (unified manager + stack)', () => {
  test('shows on demand and dismisses', async ({ page }) => {
    const toast = page.locator('[data-navius-toast]');
    await expect(toast).toHaveCount(0);

    await page.getByRole('button', { name: 'Show toast', exact: true }).click();
    await expect(toast).toHaveCount(1);
    await expect(toast).toContainText('Saved your changes');

    // Unified Root exposes the discrete Base UI open attr + C#-computed stacking var.
    await expect(toast).toHaveAttribute('data-open', '');
    const contentStyle = await page.locator('[data-navius-toast-content]').getAttribute('style');
    expect(contentStyle).toContain('--toast-index');

    // Close animates the toast out, then the Manager removes it (deferred unmount).
    await page.locator('[data-navius-toast-close]').click();
    await expect(toast).toHaveCount(0);
  });

  test('limit queues extra toasts (data-limited); type surfaces as data-type', async ({ page }) => {
    const toast = page.locator('[data-navius-toast]');

    // Provider Limit=1: three adds -> all rendered, only the frontmost stays visible.
    await page.getByRole('button', { name: 'Show 3 toasts' }).click();
    await expect(toast).toHaveCount(3);
    await expect(page.locator('[data-navius-toast]:not([data-limited])')).toHaveCount(1);
    await expect(page.locator('[data-navius-toast][data-limited]')).toHaveCount(2);

    // Manager-driven visual type -> data-type on the visible toast.
    await page.getByRole('button', { name: 'Show success' }).click();
    await expect(page.locator('[data-navius-toast][data-type="success"]')).toHaveCount(1);
  });

  test('demotion keeps the queued toast alive (stale timer must not remove it)', async ({ page }) => {
    const toast = page.locator('[data-navius-toast]');
    const savedToast = toast.filter({ hasText: 'Saved your changes' });

    // Limit=1. Show A ("Saved your changes", 5s timer starts).
    await page.getByRole('button', { name: 'Show toast', exact: true }).click();
    await expect(savedToast).toHaveCount(1);
    await expect(savedToast).not.toHaveAttribute('data-limited', '');

    // Show B ("Success!") within A's 5s window -> A is demoted to the queue (data-limited),
    // and exactly one toast stays visible.
    await page.getByRole('button', { name: 'Show success' }).click();
    await expect(page.locator('[data-navius-toast]:not([data-limited])')).toHaveCount(1);
    await expect(savedToast).toHaveAttribute('data-limited', '');

    // Pre-fix bug: A's timer kept running through the demotion and auto-removed A at ~5s while
    // it was still queued (so it never re-appeared). Wait past that deadline; the queued toast
    // must survive (the fix stops the timer on demotion) and eventually be shown once B closes.
    await page.waitForTimeout(5500);
    await expect(savedToast).toHaveCount(1);
  });

  test('viewport expands on hover and is focused by the F6 hotkey', async ({ page }) => {
    const viewport = page.locator('[data-navius-toast-viewport]');

    // F6 focuses the viewport from anywhere (Base UI hotkey; changed from F8).
    await page.keyboard.press('F6');
    await expect(viewport).toBeFocused();

    // Hovering a toast fans the stack out (data-expanded on the viewport).
    await page.getByRole('button', { name: 'Show toast', exact: true }).click();
    await page.locator('[data-navius-toast]').first().hover();
    await expect(viewport).toHaveAttribute('data-expanded', '');
  });
});
