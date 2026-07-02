# 5. OKLCH token theme; Tailwind v4 standalone for production

Status: accepted

## Context

The helm needs a theme. Two questions: which color model, and how to build Tailwind in
a Blazor (often Node-averse) project. The established styled-layer theme has moved from
HSL channel-triples to full OKLCH color values, which compose better (perceptual
uniformity, native opacity via `color-mix`) and are tweakable in modern theme editors.

## Decision

- Tokens hold **complete OKLCH color values** in `helm/Zits.Ui/wwwroot/zits-ui.css`
  (`:root` + `.dark`), with a Tailwind v4 `@theme inline` block wiring `--color-*` →
  `var(--token)` so utilities (incl. opacity modifiers) resolve natively.
- **Production** builds Tailwind with the **v4 standalone CLI** (or `Tailwind.MSBuild`)
  — no Node required.
- **Dev showcase** keeps the Tailwind Play CDN as a zero-build shortcut, mapping the
  same tokens via the CDN config. Opacity modifiers degrade gracefully there.

## Consequences

- Drop-in theme: the palette ports 1:1 and stays editor-compatible.
- The dev/prod Tailwind paths differ; the CDN is explicitly a dev shortcut, not the
  shipped pipeline. Wiring the standalone CLI into the playground build is the remaining
  polish item.
- Engine-published size/position CSS vars use a `--navius-*` namespace so the theme and
  animations are self-contained.
