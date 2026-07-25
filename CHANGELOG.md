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
