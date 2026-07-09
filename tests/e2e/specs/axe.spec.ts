import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

// Automated WCAG 2.1 A/AA scan over every playground route. This catches
// name/role/state regressions (unlabelled controls, invalid ARIA references,
// contrast) that behavioral specs do not assert. It scans the static state of
// each page; popup/overlay internals are covered by the behavioral specs.
//
// The assertion gates on critical + serious violations. Moderate/minor
// violations are reported to the console so they stay visible without
// blocking CI.

const ROUTES = [
  '/',
  '/charts',
  '/chat',
  '/dates',
  '/extras',
  '/fidelity',
  '/motion',
  '/pickers',
  '/services',
  '/sort',
  '/theme',
  '/tokens',
  '/tree',
  '/ui',
  '/uncontrolled',
  '/wave1',
  '/wave2',
  '/wave3',
];

const BLOCKING_IMPACTS = new Set(['critical', 'serious']);

for (const route of ROUTES) {
  test(`axe: ${route} has no critical or serious WCAG A/AA violations`, async ({ page }) => {
    await page.goto(route);
    // Wait for the Blazor WASM app to hydrate: the page has rendered real
    // content once the body carries more than boilerplate.
    await page.waitForLoadState('networkidle');
    await page.waitForFunction(
      () => (document.body.innerText || '').trim().length > 50,
      undefined,
      { timeout: 60_000 }
    );

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    const blocking = results.violations.filter((v) =>
      BLOCKING_IMPACTS.has(v.impact ?? '')
    );
    const advisory = results.violations.filter(
      (v) => !BLOCKING_IMPACTS.has(v.impact ?? '')
    );

    if (advisory.length) {
      console.log(
        `[axe:${route}] advisory (${advisory.length}): ` +
          advisory.map((v) => `${v.id} x${v.nodes.length}`).join(', ')
      );
    }

    expect(
      blocking.map((v) => ({
        id: v.id,
        impact: v.impact,
        help: v.help,
        nodes: v.nodes.map((n) => n.target.join(' ')).slice(0, 5),
      }))
    ).toEqual([]);
  });
}
