# Changelog

All notable changes to the packable libraries (`Navius.Primitives`, `Navius.Motion`)
are recorded here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

While the project is pre-1.0 (`0.x` previews), breaking changes may land in any
preview and are not signalled by semver alone.

> Note: `Navius.Primitives` `0.3.0-preview.2` through `-preview.4` were published to
> nuget.org without matching git tags, GitHub releases, or changelog entries. This file
> backfills those from the commit history. Creating the missing tags/releases on the
> remote for the already-shipped previews is a repo-owner action (see the audit PR).

## Navius.Primitives

### 0.3.0-preview.7
- Fix Sortable swallowing clicks on controls inside a row — the last of the
  pointer-capture family fixed in preview.6. The engine captured the pointer on
  `pointerdown`, so a row's own edit/delete button never fired, and its
  `preventDefault` also stopped the control taking focus. The drag now engages once
  the press has travelled past a threshold, which additionally means a plain click no
  longer emits a spurious `OnDragStart`/`OnCancel` pair.
- `data-dragging` and `OnDragStart` now land on the first real movement rather than on
  press. Consumers styling `data-dragging` see it a few pixels later; nothing else in
  the contract changes.

### 0.3.0-preview.6
- Fix Drawer swallowing every click inside the sheet. The swipe engine took pointer
  capture on `pointerdown`, which retargets the compatibility mouse events — and
  `click` with them — to the panel, so buttons, links and fields inside a drawer never
  fired, `NaviusDrawerClose` included. Both the drawer and the carousel now claim the
  pointer only once the press has travelled past a threshold, so a tap keeps its click
  and a drag still engages from anywhere on the sheet or slide.
- Fix a press inside a drawer permanently disabling its open/close animation: the tap
  path never released the inline `transition: none` taken at `pointerdown`.
- Fix a dismissed sheet leaking its drag transform into the next open when the popup
  stays mounted.

### 0.3.0-preview.5
- Fix Menu and ContextMenu radio groups marking themselves controlled on a null `Value`,
  which ignored `DefaultValue` and dropped selections.
- Emit `tabindex="0"` from the ScrollArea viewport so keyboard scrolling works without
  the consumer hand-adding it.
- Gate AlertDialog `aria-labelledby` / `aria-describedby` on the Title/Description parts
  actually being mounted, so a titleless dialog no longer advertises a dangling IDREF.

### 0.3.0-preview.4
- Fix controlled-state detection to track whether the parameter was set, not
  `ValueChanged.HasDelegate`, so a `@bind`-less consumer no longer freezes selection.
- Codify the nullable refinement of the controlled-ness invariant (a null `Value` does
  not mark a group controlled).
- Persist the Select value-to-label cache across item unmount so the trigger label
  survives virtualization / remounts.

### 0.3.0-preview.3
- Add the axe-core WCAG CI gate, screen-reader runbooks, and the playground labels it
  surfaced.
- Fix accessible-name and ARIA-state wiring gaps across primitives.
- Fix keyboard activation across the roving-focus primitives.
- Reword published accessibility claims into verified-fact statements.

### 0.3.0-preview.2
- Fix vendored-mode JavaScript loading and close vendoring gaps (registry CLI copies
  `navius-interop.js` into the app's own `wwwroot`).
- Check out the sibling helm repo in the publish workflow so the playground consumer
  build resolves its `ProjectReference`.

### 0.3.0-preview.1
- Initial public preview of the Navius headless primitive layer. Tagged
  `v0.3.0-preview.1`, released 2026-07-06.

## Navius.Motion

### 0.3.0-preview.1
- Initial public preview of the standalone motion engine and generated CSS. Unchanged
  since the first preview; it is versioned independently of `Navius.Primitives`.
