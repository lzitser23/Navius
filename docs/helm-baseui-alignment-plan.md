# Helm (zits/ui) — Base UI alignment plan

> **STATUS: COMPLETE (2026-07-01).** The Wave E helm sets shipped alongside their brain
> components (Autocomplete/Combobox/Toast), the deferred structural splits landed (NavMenu
> included — the last `data-state` is gone), and **both docs sites were ported to the new
> contract + de-branded** (navius-docs `feaebfb` 118→0 errors, zits-ui `7f0a3c0` 3→0;
> Acknowledgements reframed to Base UI). Live detail: the parity dossier §5.
> Remaining deferred (additive): RTL-aware positioner math, submenu
> `Sub*→Submenu*`, the `Viewport` part, nested-menu attrs, Select `Multiple`. Workstreams
> below are kept as the historical plan of record.
>
> **Post-split note (2026-07-02, ADR-0008):** the helm now lives in the sibling
> `zits-helm` repo (`src/Zits.Ui` there); `helm/Zits.Ui` paths herein are pre-split.

> **Goal:** finish aligning the styled helm (`helm/Zits.Ui`) and the two docs sites to the
> Base UI re-target, and remove the remaining Radix/shadcn *branding* (the styled-component
> design language stays — only the brand name-drops and the stale Radix data-contract go).
>
> This is a plan, not a spec. It points at the authoritative sources rather than repeating
> them: decisions in **`docs/adr/0007-retarget-reference-radix-to-base-ui.md`**, and the full
> per-component delta + roadmap in **`docs/base-ui-parity.md`** (+ `docs/base-ui-parity-deltas.json`).

## Where the helm stands

The brain has been re-targeted to Base UI through Waves 0/A/B/C and the Wave D contract
re-base (see the parity dossier STATUS markers). The helm wraps the brain with the familiar
styled-layer part names (`ZitsXxxContent` wraps the brain `Positioner`+`Popup`, etc.) — that
ergonomic naming is a deliberate, kept convention, **not** a Radix/shadcn dependency. Most of
the helm's Tailwind variants have moved from the Radix `data-[state=…]` token to Base UI's
discrete `data-[open]`/`data-[closed]`/`data-[checked]`/`data-[popup-open]`/`data-[pressed]`/
`data-[selected]` booleans in lockstep with the brain (overlays in Wave C, menus in Wave D,
the Toggle/Toolbar stragglers in this pass).

## Workstream 1 — finish the helm discrete-attr contract

Residual helm Tailwind variants still keyed on the old `data-[state=…]` / `data-[motion]`
token, and the action for each. (Audit: `grep -rn 'data-\[state=\|data-\[motion' helm/Zits.Ui/Components`.)

| Helm component | Residual hook | Action |
|---|---|---|
| `ZitsToolbarToggleItem`, `ZitsToggleGroupItem` (comment) | `data-[state=on]` | **DONE this pass** — migrated to `data-[pressed]` (brain `NaviusToolbarToggleItem` now emits `data-pressed`). |
| `ZitsCombobox` | `data-[state=open\|closed]` | Wave E — migrate with the Combobox compound rewrite (brain `NaviusCombobox` still emits `data-state` too). |
| `ZitsToast` | `data-[state=open\|closed]`, `data-[swipe=…]`, `--navius-toast-swipe-move-x` | Wave E — migrate with the Toast manager unification (discrete `data-open`/`data-closed` + `data-swiping` + renamed swipe vars). |
| `ZitsNavigationMenuContent` | `data-[motion^=…]` | Deferred with the brain — `data-motion` → `data-activation-direction` (parity dossier, NavMenu row). |
| `ZitsNavigationMenuIndicator` | `data-[state=visible\|hidden]` | Deferred with the brain — NavMenu `Indicator` → `Arrow` (Base UI removed the Indicator). |
| `ZitsSidebar*` (`Inset`/`MenuButton`/`MenuAction`) | `data-[state=open]`, `data-[state=collapsed]` | Composite fidelity component. The `data-[state=collapsed]`/`[expanded]` hooks are the Sidebar's own state (keep, or rename to a neutral token). **DONE (2026-07-02)** — the `data-[state=open]` menu hooks (pointing at an embedded `ZitsMenu`, whose trigger emits `data-popup-open`) migrated to `data-[popup-open]`. |
| `ZitsTableRow` | `data-[state=selected]` | DataTable row-selection state — helm-internal, no brain primitive; leave or rename to a neutral `data-[selected]` when DataTable is reviewed. |

## Workstream 2 — follow the brain's deferred structural splits

Wave D landed the **discrete-attr contract** across the whole menu family but only split
**Menu** into the consumer-facing `Portal → Positioner → Popup` anatomy (the reference). When
ContextMenu / Menubar / Select / NavigationMenu get the same structural split (they follow the
Menu template — `OverlayAnchoredPopupBase` + the `OverlayPositionerBase` flag-setter + roving
focus), their `ZitsXxxContent` wrappers must be rewired from wrapping the monolithic `Content`
to wrapping `Portal → Positioner → Popup` (mirror `ZitsMenuContent` / `ZitsPopoverContent`).
Same for the submenu `Sub*` → `Submenu*` rename. Track against the parity dossier Wave D
deferred list.

## Workstream 3 — Wave E helm sets

Autocomplete (new compound), Combobox (decompose to compound), and Toast (manager
unification) are XL brain rewrites (parity dossier Wave E). Each needs its styled helm set
authored/rewired afterward, on the discrete contract from the start. ZitsCombobox/ZitsToast
above fold into this.

## Workstream 4 — de-brand the docs sites

The two sibling docs repos are the remaining branding surface (they were
deliberately styled after the Radix/shadcn docs sites):

- **`../navius-docs`** (brain reference docs):
  - Rename the `DropdownMenu` page + demo to **Menu** (`DropdownMenuPage.razor` →
    `MenuPage.razor`, `DropdownMenuDemo.razor` → `MenuDemo.razor`, route
    `/docs/components/dropdown-menu` → `/docs/components/menu`, NavMenu link + SearchPalette entry,
    `@using …Components.DropdownMenu` → `…Components.Menu`). This was intentionally **not** done
    in the in-repo rename commit (separate repo).
  - Update component demos to the new brain anatomy/attrs (`Overlay`→`Backdrop`,
    `Content`→`Popup`/`Positioner`, `data-state`→discrete) so the live WASM demos compile and
    match the re-targeted brain.
  - Neutralize "Radix"/"radix-ui.com-style" framing → reference **Base UI** (base-ui.com) as the
    contract; keep any attribution in a single acknowledgements section (mirror the root README policy).
- **`../zits-ui`** (helm docs):
  - Same `DropdownMenu` → `Menu` page/demo/route/search rename.
  - Update demos to the migrated helm (`ZitsMenu*`, discrete `data-[…]`).
  - Neutralize "shadcn"/"ui.shadcn.com-style" branding → keep the styled-component design language,
    move the name-drop into acknowledgements only.

(Each docs repo references the brain by path into `..\navius\…`, so renaming the brain namespace
`Components.DropdownMenu` → `Components.Menu` already breaks their build until these edits land —
do them before running either docs site.)

## Workstream 5 — verification & sequencing

Recommended order: **1 → 2/3 (per component, with the brain split) → 4**. Each step keeps the
loop green:

1. Build: `dotnet build playground/Navius.Playground/Navius.Playground.csproj` (0 errors).
2. ESM-check the engine after any edit: `node --input-type=module --check < src/Navius.Primitives/wwwroot/navius-interop.js`.
3. Full E2E: `cd tests/e2e && DOTNET_EXE=<sdk> CI=1 npx playwright test` — grep the `N passed` line; kill port 5247 first.
4. Eyeball the `/ui` and `/fidelity` helm routes for the styled-state hooks that Playwright
   doesn't assert (the `data-[pressed]`/`data-[open]` *styling*, not just the attribute).

## Out of scope here (tracked elsewhere)

The permanent `render`-prop/Slot deviation (ADR-0003), the Navius-superset extras kept over
Base UI (cancelable layer events, the 7 extra components), and the additive brain deferrals
(Viewport everywhere, nested-dialog/menu attrs, Drawer snap-points) live in the parity
dossier — not repeated here.
