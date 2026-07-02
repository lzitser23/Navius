# 0007 — Re-target the reference contract from Radix to Base UI

Status: accepted
Date: 2026-06-27

## Context

Navius's brain was built to mirror **Radix UI**: a single `data-state` token, the
`Content`/`Overlay` anatomy, `ForceMount`, and `asChild`/`Slot`. The owner's actual
React stack, however, is **Base UI** (the MUI/ex-Radix successor) via "shadcn/ui on
base-ui". Base UI is a different contract, and the goal is now **1:1 parity with
Base UI** (base-ui.com) so the Blazor primitives behave like the Base UI components
used in React. A full audit (`docs/base-ui-parity.md`) catalogued 37 Base UI
components vs Navius (29 present-but-divergent, 9 partial, 5 missing) and ~386 work
items, dominated by contract differences rather than missing behaviour.

## Decision

**Base UI replaces Radix as the reference spec.** Concretely:

1. **Discrete data attributes.** Replace the single `data-state` with Base UI's
   presence-booleans: `data-open`/`data-closed`, `data-checked`/`data-unchecked`/
   `data-indeterminate`, `data-pressed`, `data-panel-open`, `data-popup-open`, the
   field states (`data-dirty`/`data-touched`/`data-filled`/`data-focused`/`data-valid`/
   `data-invalid`), etc. Boolean attrs use the `"" when true / null when false` idiom.
2. **Enter/exit via `data-starting-style` / `data-ending-style`.** A node animates in
   while it carries `data-starting-style` (removed one frame after mount) and out while
   it carries `data-ending-style` (kept until the transition finishes, then unmounted).
   C# owns those attributes; the engine provides the two pieces of timing C# can't do
   synchronously — `nextFrame()` and `waitForAnimations(el)`. `ForceMount` → `keepMounted`
   (plus `hiddenUntilFound`).
3. **Base UI anatomy.** Floating components split `Content` into
   `Portal → Positioner → Popup` (+ `Backdrop`/`Arrow`/`Viewport`); `Overlay` → `Backdrop`;
   `Content` → `Popup`/`Panel`. CSS vars move to Base UI's un-prefixed names
   (`--anchor-width`, `--available-height`, `--collapsible-panel-height`, …).
4. **Naming:** adopt Base UI component + part names (`DropdownMenu`→`Menu`,
   `HoverCard`→`PreviewCard`, `Content`→`Popup`/`Panel`, `Overlay`→`Backdrop`,
   `Form`→`Field`), but **keep the `Navius*`/`Zits*` type prefix** (Blazor needs it to
   avoid flat-namespace collisions). No alias shims — nothing is published, so in-repo
   references (playground, helm, docs) are updated directly. The helm keeps shadcn part
   names where they differ (e.g. `ZitsCollapsibleContent` wraps `NaviusCollapsiblePanel`).
5. **Keep the 7 Navius extras** with no Base UI equivalent (AspectRatio, AccessibleIcon,
   VisuallyHidden, Label, PasswordToggleField, Slot, DataGrid) as a documented superset.
6. **Composition is the one permanent deviation.** Base UI's `render`-prop element form
   (clone + merge props + forward ref) is not achievable in Blazor — `RenderFragment` is
   opaque (see ADR-0003). The supported approximation is the **inverted contract**: a
   primitive emits a merged attribute dict (+ state) that the consumer splats onto its own
   root via `@attributes`. `useRender`/`mergeProps`/`preventBaseUIHandler` and cross-boundary
   ref-merge are documented as not reproducible.

Execution is phased (Wave 0 contract foundation → A leaves → B forms → C overlays →
D menus → E compound rewrites); see `docs/base-ui-parity.md`. **Collapsible is the
Wave 0 pilot** proving the discrete-attr + presence engine end-to-end.

## Consequences

- High blast radius: every primitive's data-attr emission changes; all helm Tailwind
  variants that key off `data-[state=…]` and the Playwright assertions migrate in
  lockstep. `CONTEXT.md` and `README.md` need a rewrite away from Radix framing (the
  "append-only test hook" invariant is relaxed for this re-target — parts are renamed,
  e.g. `data-navius-collapsible-content` → `…-panel`).
- The engine gains a small generic presence/timing surface (`nextFrame`,
  `waitForAnimations`, `observeBeforeMatch`) reused by every animated component.
- Eight net-new primitives (Autocomplete, Button, Checkbox Group, Drawer, Fieldset,
  Input, Meter, Number Field) must be authored.
- The CSP-unsafe `eval()` sites (NavigationMenu ×3, helm Command) are removed as part of
  Wave 0, matching Base UI's no-`eval` posture and adding a `NaviusCspProvider`.
- Radix parity (ADR-0001/0002 still describe the brain/helm split + engine rationale,
  which are unchanged) is superseded as the *reference*; this ADR is the new north star.
