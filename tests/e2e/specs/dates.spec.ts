import { test, expect } from '@playwright/test';

// Date / Time family, exercised on the /dates page. Covers the segmented spinbutton
// keyboard model + ARIA, the TimePicker column listboxes, range selection, and that
// controlled (@bind) and uncontrolled (DefaultValue) both work through the wrappers.
test.beforeEach(async ({ page }) => {
  await page.goto('/dates');
  await expect(page.locator('[data-demo=date-controlled] [data-segment=month]')).toBeVisible({ timeout: 60_000 });
});

test('DateInput segment: APG spinbutton ARIA', async ({ page }) => {
  const month = page.locator('[data-demo=date-controlled] [data-segment=month]');
  await expect(month).toHaveAttribute('role', 'spinbutton');
  await expect(month).toHaveAttribute('aria-valuemin', '1');
  await expect(month).toHaveAttribute('aria-valuemax', '12');
  await expect(month).toHaveAttribute('aria-valuenow', '7'); // 2026-07-04
  // aria-valuetext gives the human month name (culture-specific), just not "Empty".
  await expect(month).not.toHaveAttribute('aria-valuetext', 'Empty');

  const year = page.locator('[data-demo=date-controlled] [data-segment=year]');
  await expect(year).toHaveAttribute('aria-valuenow', '2026');
  await expect(year).toHaveAttribute('aria-valuemax', '9999');
});

test('DateInput segment: arrows step + wrap, controlled value round-trips', async ({ page }) => {
  const month = page.locator('[data-demo=date-controlled] [data-segment=month]');
  const value = page.locator('[data-demo=date-controlled] [data-date-value]');

  await month.focus();
  await page.keyboard.press('ArrowUp'); // 7 -> 8
  await expect(month).toHaveAttribute('aria-valuenow', '8');
  await expect(value).toHaveText('2026-08-04'); // @bind round-tripped

  await page.keyboard.press('ArrowDown'); // 8 -> 7
  await expect(month).toHaveAttribute('aria-valuenow', '7');

  await page.keyboard.press('Home'); // -> 1
  await expect(month).toHaveAttribute('aria-valuenow', '1');
  await page.keyboard.press('ArrowDown'); // wraps 1 -> 12
  await expect(month).toHaveAttribute('aria-valuenow', '12');
});

test('DateInput segment: typing digits fills + auto-advances', async ({ page }) => {
  const month = page.locator('[data-demo=date-controlled] [data-segment=month]');
  await month.focus();
  await page.keyboard.press('1'); // buffer "1"
  await expect(month).toHaveAttribute('aria-valuenow', '1');
  await page.keyboard.press('2'); // "12" -> full, auto-advance off this segment
  await expect(month).toHaveAttribute('aria-valuenow', '12');
  await expect(month).not.toBeFocused(); // advanced to the next segment
});

test('DateInput: uncontrolled DefaultValue seeds and edits without a bound value', async ({ page }) => {
  const year = page.locator('[data-demo=date-uncontrolled] [data-segment=year]');
  const echo = page.locator('[data-demo=date-uncontrolled] [data-date-echo]');

  await expect(year).toHaveAttribute('aria-valuenow', '2024'); // DefaultValue 2024-01-15 seeded
  await expect(echo).toHaveText('null'); // parent never set Value; no edit yet

  await year.focus();
  await page.keyboard.press('ArrowUp'); // 2024 -> 2025
  await expect(year).toHaveAttribute('aria-valuenow', '2025');
  await expect(echo).toHaveText('2025-01-15'); // uncontrolled edit still fired ValueChanged
});

test('DateInput: styled wrapper (ZitsDateInput) is controllable', async ({ page }) => {
  const month = page.locator('[data-demo=date-styled] [data-segment=month]');
  await expect(month).toHaveAttribute('aria-valuenow', '3'); // 2026-03-20
  await month.focus();
  await page.keyboard.press('ArrowUp');
  await expect(page.locator('[data-demo=date-styled] [data-date-styled-value]')).toHaveText('2026-04-20');
});

test('TimeInput: 12-hour AM/PM segment toggles', async ({ page }) => {
  const dp = page.locator('[data-demo=time-12] [data-segment=dayPeriod]');
  await expect(dp).toHaveAttribute('role', 'spinbutton');
  await expect(dp).toHaveAttribute('aria-valuetext', 'PM'); // 14:15:45

  await dp.focus();
  await page.keyboard.press('a'); // -> AM
  await expect(dp).toHaveAttribute('aria-valuetext', 'AM');
  await expect(page.locator('[data-demo=time-12] [data-time12-value]')).toHaveText('02:15:45');
});

test('TimeInput: 24-hour hour segment ranges 0..23', async ({ page }) => {
  const hour = page.locator('[data-demo=time-24] [data-segment=hour]');
  await expect(hour).toHaveAttribute('aria-valuemin', '0');
  await expect(hour).toHaveAttribute('aria-valuemax', '23');
  await expect(hour).toHaveAttribute('aria-valuenow', '9'); // 09:30
});

test('TimePicker: trigger opens a dialog popup of listbox columns; selecting sets the value', async ({ page }) => {
  const trigger = page.locator('[data-demo=time-picker] [data-navius-time-picker-trigger]');
  await expect(trigger).toHaveAttribute('aria-haspopup', 'dialog');
  await expect(trigger).toHaveAttribute('aria-expanded', 'false');

  await trigger.click();
  const popup = page.locator('[data-navius-time-picker-popup]');
  await expect(popup).toBeVisible();
  await expect(trigger).toHaveAttribute('aria-expanded', 'true');

  const hourCol = page.locator('[data-navius-time-picker-column][data-unit=hour]');
  await expect(hourCol).toHaveAttribute('role', 'listbox');
  const opt13 = hourCol.locator('[data-navius-time-picker-option][data-value="13"]');
  await expect(opt13).toHaveAttribute('role', 'option');
  await opt13.click();

  await expect(page.locator('[data-demo=time-picker] [data-picktime-value]')).toHaveText('13:00');
  await expect(opt13).toHaveAttribute('data-selected', '');
});

test('RangeCalendar: two clicks set an ordered range with endpoint markers', async ({ page }) => {
  const cal = page.locator('[data-demo=range-calendar]');
  await cal.locator('[data-day="2026-06-10"]').click();
  await cal.locator('[data-day="2026-06-14"]').click();

  await expect(cal.locator('[data-day="2026-06-10"]')).toHaveAttribute('data-range-start', 'true');
  await expect(cal.locator('[data-day="2026-06-14"]')).toHaveAttribute('data-range-end', 'true');
  await expect(cal.locator('[data-day="2026-06-12"]')).toHaveAttribute('data-range-middle', 'true');
  await expect(page.locator('[data-demo=range-calendar] [data-cal-range-value]')).toHaveText('2026-06-10 → 2026-06-14');
});

test('DateRangePicker: group + two endpoint inputs; popover calendar picks a range and closes', async ({ page }) => {
  const rp = page.locator('[data-demo=range-picker]');
  const trigger = rp.locator('[data-navius-date-range-picker-trigger]');
  await expect(trigger).toHaveAttribute('aria-haspopup', 'dialog');

  // Both segmented endpoints render; the start reflects the initial range (2026-06-10..14).
  await expect(rp.locator('[data-navius-date-range-picker-input][data-part=start]')).toBeVisible();
  await expect(rp.locator('[data-navius-date-range-picker-input][data-part=end]')).toBeVisible();
  await expect(rp.locator('[data-part=start] [data-segment=month]')).toHaveAttribute('aria-valuenow', '6');

  await trigger.click();
  const popup = page.locator('[data-navius-date-range-picker-popup]');
  await expect(popup).toBeVisible();
  await expect(popup.locator('[data-day="2026-06-10"]')).toHaveAttribute('data-range-start', 'true');

  // Pick a fresh range; selecting the end completes it and dismisses the popover.
  await popup.locator('[data-day="2026-06-12"]').click();
  await popup.locator('[data-day="2026-06-16"]').click();
  await expect(rp.locator('[data-range-value]')).toHaveText('2026-06-12 → 2026-06-16');
  await expect(popup).toBeHidden();
});
