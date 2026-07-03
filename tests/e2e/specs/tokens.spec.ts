import { test, expect } from '@playwright/test';

// Token inputs (MaskedInput, CurrencyInput, TagInput, SplitButton, MultiSelect) on /tokens.
test.beforeEach(async ({ page }) => {
  await page.goto('/tokens');
  await expect(page.locator('[data-navius-masked-input]')).toBeVisible({ timeout: 60_000 });
});

test('MaskedInput: masks digits into the pattern and rejects non-digits', async ({ page }) => {
  const input = page.locator('[data-navius-masked-input]');

  await input.click();
  // delay lets each async caret round-trip settle so fast keystrokes never race the mask.
  await page.keyboard.type('1234567890abc', { delay: 40 }); // letters are rejected by the digit tokens
  await expect(input).toHaveValue('(123) 456-7890');
  await expect(page.locator('[data-mask-value]')).toHaveText('(123) 456-7890');
});

test('MaskedInput: keeps the caret stable on a mid-string edit', async ({ page }) => {
  const input = page.locator('[data-navius-masked-input]');

  await input.click();
  await page.keyboard.type('1234', { delay: 40 });
  await expect(input).toHaveValue('(123) 4');

  // Drop the caret between "1" and "2" (index 2 of "(123) 4") and insert a digit there.
  await input.evaluate((el) => (el as HTMLInputElement).setSelectionRange(2, 2));
  await page.keyboard.type('9');

  // The digit lands mid-string and the caret follows it to index 3; it does NOT jump to the end
  // (the end would be index 8; a naive re-render resets the caret there, this asserts the fix).
  await expect(input).toHaveValue('(192) 34');
  await expect
    .poll(() => input.evaluate((el) => (el as HTMLInputElement).selectionStart))
    .toBe(3);
});

test('CurrencyInput: formats per culture (symbol, grouping, decimals) and clamps fractions on blur', async ({ page }) => {
  const usd = page.locator('[data-currency="usd"]');
  const eur = page.locator('[data-currency="eur"]');

  await usd.click();
  await page.keyboard.type('1234567', { delay: 40 });
  await expect(usd).toHaveValue('$1,234,567'); // prefix symbol, comma grouping
  await expect(page.locator('[data-usd-value]')).toHaveText('1234567'); // decimal? truth
  await usd.blur();
  await expect(usd).toHaveValue('$1,234,567.00'); // padded to the culture's fraction digits

  await eur.click();
  await page.keyboard.type('1234567', { delay: 40 });
  await expect(eur).toHaveValue('1.234.567 €'); // suffix symbol, dot grouping
  await eur.blur();
  await expect(eur).toHaveValue('1.234.567,00 €'); // comma decimal separator
});

test('TagInput: commit via Enter/comma; empty-Backspace highlights then removes', async ({ page }) => {
  const field = page.locator('[data-navius-tag-input-field]');
  const chips = page.locator('[data-navius-tag]');

  await expect(chips).toHaveCount(1); // seeded "design"

  await field.click();
  await page.keyboard.type('react', { delay: 30 });
  await page.keyboard.press('Enter');
  await expect(chips).toHaveCount(2);
  await expect(chips.nth(1)).toContainText('react');

  await page.keyboard.type('vue,', { delay: 30 }); // the comma commits the chip and clears the field
  await expect(chips).toHaveCount(3);
  await expect(chips.nth(2)).toContainText('vue');
  await expect(field).toHaveValue('');

  // Empty-field Backspace: first press highlights the last chip, second removes it.
  await page.keyboard.press('Backspace');
  await expect(chips.nth(2)).toHaveAttribute('data-highlighted', '');
  await page.keyboard.press('Backspace');
  await expect(chips).toHaveCount(2);
  await expect(page.locator('[data-tags-value]')).toHaveText('design,react');
});

test('SplitButton: action fires; trigger opens the menu; a menu item fires and closes it', async ({ page }) => {
  const action = page.getByRole('button', { name: 'Save', exact: true });
  const trigger = page.getByRole('button', { name: 'More save options' });
  const last = page.locator('[data-split-last]');

  await action.click();
  await expect(last).toHaveText('save');

  await expect(trigger).toHaveAttribute('aria-expanded', 'false');
  await trigger.click();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');

  const saveAs = page.getByRole('menuitem', { name: 'Save as...' });
  await expect(saveAs).toBeVisible();
  await saveAs.click();
  await expect(last).toHaveText('saveas');
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');
});

test('MultiSelect: chips collapse past MaxDisplayChips to a "+N more" badge; removing updates', async ({ page }) => {
  const input = page.locator('[data-navius-combobox-input]');
  const chips = page.locator('[data-navius-combobox-chip]');
  const multi = page.locator('[data-section="multi"]');

  await input.click();
  await page.getByRole('option', { name: 'React' }).click();
  await page.getByRole('option', { name: 'Vue' }).click();
  await page.getByRole('option', { name: 'Svelte' }).click();
  await page.keyboard.press('Escape');

  // MaxDisplayChips=2 → two chips + a "+1 more" overflow badge; the selection is still all three.
  await expect(chips).toHaveCount(2);
  await expect(multi).toContainText('+1 more');
  await expect(page.locator('[data-multi-value]')).toHaveText('React,Vue,Svelte');

  // Remove the first chip → 2 selected, the overflow badge disappears.
  await chips.first().locator('[data-navius-combobox-chip-remove]').click();
  await expect(chips).toHaveCount(2);
  await expect(multi).not.toContainText('+1 more');
  await expect(page.locator('[data-multi-value]')).toHaveText('Vue,Svelte');
});
