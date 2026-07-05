# 9. Runtime theming engine: generated OKLCH token CSS, compositional ramps, scoped

Status: accepted (2026-07-04)

## Context

The helm has one fixed OKLCH token theme (ADR-0005): `:root` + `.dark` in
`zits-ui.css`, wired to Tailwind v4 via `@theme inline`. Consumers can hand-edit
those tokens, but there is no runtime story: no light/dark/system switch that
survives a reload, no way to recolour or restyle at runtime, and no scoped theming.

NeoUI, the nearest Blazor competitor, ships "style variants" as per-component
Tailwind class maps baked into a NuGet package. That model does not fit a
copy-paste registry (ADR-0004) where the consumer owns the component source:
restyling a component is editing your own file, so a shipped per-component class map
would be dead weight that fights the code the consumer already owns.

We also want to keep the no-Node-at-runtime posture. `Navius.Motion` already proved
the pattern: a C# `CssGen` console app generates a deterministic, culture
independent, unit-tested static stylesheet that is committed and served as a web
asset. Theming should reuse that machinery rather than compute anything in the
browser or at build time via Node.

## Decision

Ship an opt-in runtime theming engine layered on the existing tokens.

1. **Generated token CSS, exactly like the motion stylesheet.** A `Zits.Ui.CssGen`
   console app emits `zits-theme.css` (single `StringBuilder`, fixed block order,
   InvariantCulture, LF, UTF-8 no BOM, a GENERATED banner with the regen command).
   The output is committed and drift-guarded by a unit test, so there is no Node and
   no runtime computation.

2. **Compositional ramp architecture, not a combinatorial explosion.** Six
   dimensions (mode, base gray, primary, radius, font, style recipe) are switched at
   runtime via `data-zits-*` attributes on any element. The dimensions do not
   multiply: base blocks emit only a gray ramp, style blocks map surface tokens from
   that ramp, primary is orthogonal, and radius and font are independent. Because CSS
   custom properties resolve at the element that uses them, scoped containers compose
   these dimensions freely from a linear, fixed-order stylesheet.

3. **Token recipes instead of per-component class maps.** Our style dimension
   ("standard", "tinted", "soft", "contrast") remaps the shared design tokens, so a
   restyle reaches every component that reads the tokens without shipping a variant
   table per component. This keeps the code-ownership story intact: the consumer's
   copied components are untouched, only the tokens they already consume change.

4. **Scoped theming.** `ZitsThemeScope` renders a plain container carrying its own
   `data-zits-*` attributes, including a forced light or dark mode that ignores the
   page mode. Any subtree can therefore carry a full independent theme, which the
   per-component-class-map model cannot express.

5. **CSS export (code ownership).** `ThemeStylesheet.GenerateCss(theme)` resolves any
   selection to a self-contained, fully resolved `:root {} .dark {}` block compatible
   with the existing `@theme inline` mapping. The theme editor offers this as a
   copy-your-theme-CSS export so a consumer can bake a chosen theme into their own
   `zits-ui.css` and drop the engine entirely.

6. **Persistence and no-flash restore.** `ZitsThemeService` (scoped DI) persists the
   selection to `localStorage` as one JSON blob and mirrors it to the DOM through a
   small JS module; a classic pre-paint init script restores full JSON selections
   before first paint, with `system` mode resolved from `prefers-color-scheme`.
   Legacy bare `'dark'`/`'light'` values restore only the class so existing apps that
   already used the `zits-theme` key keep their original token theme until they opt
   into the full engine state.

The engine is distributed as a single registry item (`navius add theme`) and is
entirely opt-in: a consumer who never adds it keeps the fixed ADR-0005 theme.

## Consequences

- The positioning against NeoUI is now explicit and defensible: we deliberately do
  not ship per-component style variants, and we add two things it lacks, scoped
  theming and CSS export.
- The generated stylesheet is a second committed static asset governed by the same
  drift test discipline as the motion stylesheet; regenerate with
  `dotnet run --project src/Zits.Ui.CssGen` after any generator change.
- The engine reuses the existing chart and font tokens, so `zits-ui.css` gains the
  shadcn default chart tokens and font-family variables it was missing.
- The playground `/theme` route, the zits-ui homepage token editor, and
  `/docs/theme-engine` exercise the engine; the Playwright suite gains scoped-theming,
  persistence, and dimension switching coverage.
