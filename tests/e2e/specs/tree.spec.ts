import { test, expect, Locator } from '@playwright/test';

// TreeView (WAI-APG) exercised on the /tree page: roles/levels, roving tabindex, arrow
// expand/collapse semantics, Home/End, type-ahead, single- and multi-select.
test.beforeEach(async ({ page }) => {
  await page.goto('/tree');
  await expect(page.locator('[data-navius-tree]').first()).toBeVisible({ timeout: 60_000 });
});

// The role="treeitem" whose own trigger label is exactly `name`, inside `root`.
const node = (root: Locator, name: string): Locator =>
  root
    .locator('[data-navius-tree-item-trigger]', { hasText: new RegExp(`^${name}$`) })
    .locator('xpath=ancestor::*[@data-navius-tree-item][1]');

// Focus a treeitem and wait for the roving tab stop to land on it (the focus->active sync).
async function focusNode(item: Locator) {
  await item.focus();
  await expect(item).toHaveAttribute('tabindex', '0');
}

test('structure: role=tree, treeitem roles + aria-level/setsize/posinset; parents expandable, leaves not', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  await expect(tree.getByRole('tree')).toBeVisible();
  // Single-select tree is not multi-selectable.
  await expect(tree.locator('[data-navius-tree]')).not.toHaveAttribute('aria-multiselectable', /.*/);

  const fruits = node(tree, 'Fruits');
  await expect(fruits).toHaveAttribute('role', 'treeitem');
  await expect(fruits).toHaveAttribute('aria-level', '1');
  await expect(fruits).toHaveAttribute('aria-setsize', '3'); // Fruits / Vegetables / Grains
  await expect(fruits).toHaveAttribute('aria-posinset', '1');
  await expect(fruits).toHaveAttribute('aria-expanded', 'true'); // seeded expanded

  const vegetables = node(tree, 'Vegetables');
  await expect(vegetables).toHaveAttribute('aria-posinset', '2');
  await expect(vegetables).toHaveAttribute('aria-expanded', 'false'); // collapsed parent

  // A leaf child carries level 2 + a position, and NEVER aria-expanded.
  const apple = node(tree, 'Apple');
  await expect(apple).toHaveAttribute('aria-level', '2');
  await expect(apple).toHaveAttribute('aria-setsize', '3'); // Apple / Apricot / Banana
  await expect(apple).toHaveAttribute('aria-posinset', '1');
  await expect(apple).not.toHaveAttribute('aria-expanded', /.*/);
});

test('roving tabindex: exactly one node is tabbable and it follows focus', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  // First node is the initial tab stop.
  await expect(node(tree, 'Fruits')).toHaveAttribute('tabindex', '0');
  await expect(node(tree, 'Apple')).toHaveAttribute('tabindex', '-1');

  await focusNode(node(tree, 'Vegetables'));
  await expect(node(tree, 'Fruits')).toHaveAttribute('tabindex', '-1');
  await expect(node(tree, 'Vegetables')).toHaveAttribute('tabindex', '0');
});

test('Right expands a collapsed parent then steps into it; Left collapses / steps to parent', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  const vegetables = node(tree, 'Vegetables');
  const carrot = node(tree, 'Carrot');

  await expect(carrot).toHaveCount(0); // collapsed: child not in the DOM
  await focusNode(vegetables);

  await page.keyboard.press('ArrowRight'); // collapsed parent -> expand in place
  await expect(vegetables).toHaveAttribute('aria-expanded', 'true');
  await expect(node(tree, 'Carrot')).toBeVisible();

  await page.keyboard.press('ArrowRight'); // expanded parent -> move to first child
  await expect(node(tree, 'Carrot')).toBeFocused();

  await page.keyboard.press('ArrowLeft'); // leaf -> move to parent
  await expect(vegetables).toBeFocused();

  await page.keyboard.press('ArrowLeft'); // expanded parent -> collapse
  await expect(vegetables).toHaveAttribute('aria-expanded', 'false');
  await expect(node(tree, 'Carrot')).toHaveCount(0);
});

test('Down/Up walk visible nodes; Home/End jump to first/last', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  await focusNode(node(tree, 'Fruits')); // expanded, so children are visible

  await page.keyboard.press('ArrowDown');
  await expect(node(tree, 'Apple')).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(node(tree, 'Apricot')).toBeFocused();
  await page.keyboard.press('ArrowUp');
  await expect(node(tree, 'Apple')).toBeFocused();

  await page.keyboard.press('End');
  await expect(node(tree, 'Grains')).toBeFocused(); // last visible (collapsed)
  await page.keyboard.press('Home');
  await expect(node(tree, 'Fruits')).toBeFocused();
});

test('type-ahead moves focus to the next visible node whose label matches', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  await focusNode(node(tree, 'Fruits'));

  await page.keyboard.press('v'); // -> Vegetables
  await expect(node(tree, 'Vegetables')).toBeFocused();

  await page.waitForTimeout(600); // let the type-ahead buffer (500ms) reset before a new search
  await page.keyboard.press('g'); // -> Grains
  await expect(node(tree, 'Grains')).toBeFocused();
});

test('single-select: click selects a leaf; Enter selects the focused node; aria-selected + readout follow', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  const readout = tree.locator('[data-single-selected]');
  const apple = node(tree, 'Apple');
  const banana = node(tree, 'Banana');

  await apple.locator('[data-navius-tree-item-trigger]').click();
  await expect(readout).toHaveText('apple');
  await expect(apple).toHaveAttribute('aria-selected', 'true');
  await expect(apple).toHaveAttribute('data-selected', '');

  await focusNode(banana);
  await page.keyboard.press('Enter');
  await expect(readout).toHaveText('banana');
  await expect(banana).toHaveAttribute('aria-selected', 'true');
  await expect(apple).toHaveAttribute('aria-selected', 'false'); // single: previous cleared
});

test('disabled node: aria-disabled and skipped by keyboard navigation', async ({ page }) => {
  const tree = page.locator('[data-testid="tree"]');
  await focusNode(node(tree, 'Grains'));
  await page.keyboard.press('ArrowRight'); // expand Grains -> Rice + Wheat(disabled)

  const wheat = node(tree, 'Wheat');
  await expect(wheat).toHaveAttribute('aria-disabled', 'true');

  const rice = node(tree, 'Rice');
  await page.keyboard.press('ArrowRight'); // into first child -> Rice
  await expect(rice).toBeFocused();
  await page.keyboard.press('ArrowDown'); // Wheat is disabled -> skipped -> stays on Rice
  await expect(rice).toBeFocused();
});

test('multi-select: Space toggles nodes; Ctrl+A selects all visible', async ({ page }) => {
  const tree = page.locator('[data-testid="tree-multi"]');
  const readout = tree.locator('[data-multi-selected]');
  const apple = node(tree, 'Apple');
  const banana = node(tree, 'Banana');

  await expect(tree.locator('[data-navius-tree]')).toHaveAttribute('aria-multiselectable', 'true');

  await focusNode(apple);
  await page.keyboard.press(' ');
  await expect(apple).toHaveAttribute('aria-selected', 'true');
  await expect(readout).toContainText('apple');

  await focusNode(banana);
  await page.keyboard.press(' ');
  await expect(banana).toHaveAttribute('aria-selected', 'true');
  await expect(readout).toContainText('apple');
  await expect(readout).toContainText('banana');

  await page.keyboard.press(' '); // toggle Banana back off
  await expect(banana).toHaveAttribute('aria-selected', 'false');
  await expect(readout).not.toContainText('banana');

  await page.keyboard.press('Control+a'); // select every visible node
  await expect(node(tree, 'Fruits')).toHaveAttribute('aria-selected', 'true');
  await expect(node(tree, 'Vegetables')).toHaveAttribute('aria-selected', 'true');
  await expect(banana).toHaveAttribute('aria-selected', 'true');
});

test('data-driven: Items + ItemTemplate wire roles/levels; only nodes with children are expandable', async ({ page }) => {
  const tree = page.locator('[data-testid="tree-data"]');

  const src = node(tree, 'src');
  await expect(src).toHaveAttribute('aria-level', '1');
  await expect(src).toHaveAttribute('aria-expanded', 'true'); // seeded expanded

  const readme = node(tree, 'README.md');
  await expect(readme).toHaveAttribute('aria-level', '1');
  await expect(readme).not.toHaveAttribute('aria-expanded', /.*/); // leaf

  const components = node(tree, 'components');
  await expect(components).toHaveAttribute('aria-level', '2');
  await expect(components).toHaveAttribute('aria-expanded', 'false'); // collapsed parent
  await expect(node(tree, 'Button.razor')).toHaveCount(0);

  await focusNode(components);
  await page.keyboard.press('ArrowRight'); // expand components
  const button = node(tree, 'Button.razor');
  await expect(button).toBeVisible();
  await expect(button).toHaveAttribute('aria-level', '3');
});
