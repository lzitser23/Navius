# Navius → Base UI 1:1 Parity Dossier & Phased Roadmap

> Reference spec: **base-ui.com** (Base UI for React). Target: full re-target of the Navius Blazor primitive library (`src/Navius.Primitives`) plus its styled helm layer (`helm/Zits.Ui`) from the legacy Radix contract to Base UI 1:1.
>
> **Post-split note (2026-07-02, ADR-0008):** the helm now lives in the sibling `zits-helm` repo (`src/Zits.Ui` there); `helm/Zits.Ui` paths in this dossier are pre-split. Commit hashes cited in STATUS lines refer to the pre-publication development history.

---

## 1. Executive Summary

This is a **contract re-target**, not a feature rebuild. Navius already ships functional, keyboard-accurate headless primitives for the overwhelming majority of the surface; the divergence is overwhelmingly *shape* (anatomy/part names), *contract* (data-attribute model), and *eight genuinely new components*.

- **What "1:1 with Base UI" means here:** (a) swap the single multi-valued `data-state` token for Base UI's **discrete boolean-presence attributes** (`data-open`/`data-closed`, `data-checked`/`data-unchecked`, `data-highlighted`, `data-pressed`, `data-popup-open`, etc.); (b) adopt the **`data-starting-style`/`data-ending-style` transition model** with wait-for-animation unmount, replacing tailwindcss-animate keyframes + `ForceMount`; (c) split monolithic popup `Content` parts into Base UI's **`Portal` → `Positioner` → `Popup` (+ `Backdrop`/`Arrow`/`Viewport`)** anatomy; (d) adopt Base UI **prop naming/value models** (array-value Accordion, `multiple` boolean, `keepMounted`, `loopFocus`, `OnOpenChange` with event details); (e) rename CSS vars from `--navius-popper-*`/`--navius-*-content-*` to Base UI's un-prefixed `--anchor-width`/`--available-height`/`--transform-origin`; (f) adopt the **discrete field-state contract** (`data-dirty`/`data-touched`/`data-filled`/`data-focused`/`data-valid`/`data-invalid`/`data-disabled`) on every form part.
- **Eight net-new components** must be authored: **Autocomplete, Button, Checkbox Group, Drawer (as a real brain primitive), Fieldset, Input, Meter, Number Field.** Five are fully absent (Checkbox Group, Fieldset, Input, Meter, Number Field); three exist only as a monolith/helm wrapper that must be promoted to a headless primitive (Autocomplete, Button, Drawer).
- **Foundational "Wave 0"** is four cross-cutting contract changes (composition/render, data-attr+animation, forms/validity, utilities CSP+direction) that touch nearly every component before per-component work is sane.
- **Naming decisions are required up front:** rename `DropdownMenu` → `Menu`, `HoverCard` → `Preview Card`; rename Navius's `Form.*` namespace to `Field.*`; decide the fate of the 7 Navius extras with no Base UI equivalent (AspectRatio, AccessibleIcon, VisuallyHidden, Label, PasswordToggleField, Slot, DataGrid).

### Headline counts

**Component status (37 total):**

| Status | Count | Components |
|---|---|---|
| Missing (build from scratch) | 5 | Checkbox Group, Fieldset, Input, Meter, Number Field |
| Partial (decompose/restructure) | 9 | Autocomplete, Button, Combobox, Drawer, Field, Navigation Menu, Progress, Slider, Toast |
| Present – needs rename / contract-only | 23 | Accordion, Alert Dialog, Avatar, Checkbox, Collapsible, Context Menu, Dialog, Form, Menu, Menubar, OTP Field, Popover, Preview Card, Radio, Scroll Area, Select, Separator, Switch, Tabs, Toggle, Toggle Group, Toolbar, Tooltip |
| Fully present (no work) | 0 | — |

**Work items by severity (385 total, including 4 cross-cutting contracts):**

| Severity | Count |
|---|---|
| Blocker | 83 |
| Major | 186 |
| Minor | 116 |
| **Total** | **385** |

**Effort distribution (per-component `effortEstimate`):** XL = 9 (Autocomplete, Combobox, Context Menu, Drawer, Menu, Menubar, Navigation Menu, Number Field, Select, Toast — note 10, see below), L = 11, M = 9, S = 3. The single largest cost centers are the popup-family Positioner/Popup split and the systemic `data-state` → discrete-boolean migration (~83 `.razor` sites, ~26 `DataState =>` getters, 52 helm Tailwind variants).

---

## 2. Cross-Cutting Contract Changes (Wave 0 — the foundation)

Four global contracts touch essentially every component. They must land *before* per-component re-targeting or the same work is redone 37 times.

### 2.1 Composition / render-prop contract

**Base UI model:** one `render` prop on every part — element form (`render={<el/>}`, clone + merge props + forward ref) and function form (`render={(props,state)=>...}` exposing component `state`, e.g. `state.checked`). Userland reproduces via `useRender` (`defaultTagName`, `stateAttributesMapping`, ref-merge). Merge semantics (`mergeProps`): rightmost-wins scalars/style; `className` concatenated rightmost-first (`'b a'`); handlers chained rightmost-first with `event.preventBaseUIHandler()`; ref **not** merged (rightmost only).

**Navius today:** two seams — (1) the everyday `[Parameter(CaptureUnmatchedValues=true)] Attributes` splat onto a *fixed* root element; (2) `NaviusSlot` + `SlotMerge` (ADR-0003), which renders no DOM, merges props, and hands a `RenderFragment<IReadOnlyDictionary<string,object>>` to the consumer to splat. `NaviusSlot` is wired only into `NaviusToastClose`. No state channel, no state→data-attr mapping, no ref merge.

**Blazor constraint / approximation:** `RenderFragment` is opaque — no clone-with-merged-props, no cross-boundary ref merge, no `preventBaseUIHandler`. Approximation: **invert the contract** — Navius passes a merged attribute dict *out* and the consumer splats it onto their own chosen root (covering render-as-different-element). Document ref-merge and handler-cancellation as platform deviations.

**Blast radius:** ADR-0003 follow-up; `NaviusSlot`/`SlotMerge` promoted to a public `useRender`-equivalent; every primitive that needs polymorphic tags. **Key items:** add a typed **state channel** to `NaviusSlot.ChildContent` (the `Switch.Thumb` checked/unchecked case is impossible today); add **state→`data-*` mapping**; reconcile `SlotMerge` ordering with `mergeProps` (className/handler order is load-bearing for the Tailwind cascade); publish the helper; define an optional cancelable args record to approximate `preventBaseUIHandler`.

### 2.2 Data attributes + animation

**Base UI model:** discrete boolean-presence attributes (no `data-state`). Enter/exit is a **transition** model: `data-starting-style` on the first committed visible frame, `data-ending-style` while hiding, interpolated by a normal CSS `transition` (cancellable mid-flight). `data-instant` skips animation. Popups **unmount when closed by default** — Base UI awaits `element.getAnimations()`/`finished` before removing the node; `keepMounted` (on Portal) keeps it. Un-prefixed CSS vars: `--available-height`, `--anchor-width`, `--transform-origin`.

**Navius today:** Radix contract — a single `data-state` from ~26 `DataState =>` getters (`open/closed`, `checked/unchecked/indeterminate`, `on/off`, `visible/hidden`, `loading/complete`, `active/disabled`); `@if (Open || ForceMount)` render gate; `ForceMount` (38 files) keeps the node with `data-state="closed"` + `hidden`. Animation via tailwindcss-animate keyframes (`data-[state=open]:animate-in` etc., 52 occurrences across 34 helm components). Collapse sizes from `createSizeObserver` → `--navius-{prefix}-content-height/width`; positioner writes `--navius-popper-*`.

**Blazor constraint / approximation:** No built-in presence primitive. Keep `@if (Open || keepMounted)` but on close emit `data-ending-style` and **defer unmount via a JS `getAnimations()` await** (the `NaviusJsInterop` + `OnAfterRenderAsync` seam). `data-starting-style` requires a JS rAF round-trip (paint-then-mutate). Discrete attrs in Razor are trivial: `data-open="@(Open ? "" : null)"`.

**Blast radius:** ~83 `.razor` emission sites, ~26 getters, 38 `ForceMount` files, 52 helm Tailwind variants across 34 components, 11 Playwright `toHaveAttribute('data-state', …)` assertions (navius.spec.ts, wave1.spec.ts, ui.spec.ts). Build the **per-component mapping table first** (this is the migration spec). This is the single highest-effort contract (two XL items).

### 2.3 Forms / Field validity

**Base UI model:** three primitives. `Field.Root` (`<div>`) + `Label`/`Control`/`Description`/`Error`/`Validity`/`Item`; every part carries `data-disabled/valid/invalid/dirty/touched/filled/focused`, and `Field.Error` adds `data-starting-style`/`data-ending-style`. `Field.Root.validationMode` (`onSubmit` default | `onBlur` | `onChange`), `validate`, `validationDebounceTime`, `invalid`, `actionsRef.validate()`. `Form` takes an **`errors` object keyed by field name** + `onClearErrors`; entries auto-clear when the bound field changes. `Fieldset.Root` (`<fieldset>`) + `Fieldset.Legend` (`<div>`) with a `disabled` state.

**Navius today:** Radix `react-form` model under one `Form` namespace — `NaviusForm` + `NaviusFormField` (+ `Label`/`Control`/`Message`/`ValidityState`/`Submit`). Field state is **only** `data-valid`/`data-invalid`; no dirty/touched/filled/focused/disabled tracking. Validity merges consumer `Validity` record + native `ValidityState` (JS `createConstraintValidation`). No `validationMode`, no `validate`, no debounce. Server errors are a per-field `ServerInvalid` bool, not a form-level errors-by-name map.

**Blazor constraint / approximation:** Focus/blur/dirty/filled must come from **JS** — extend the constraint-validation engine to emit `focusin`/`focusout`/`input` + initial-value capture, mirrored into new `FieldContext` setters. `validationDebounceTime` via `Timer`/`CancellationToken`. `transitionStatus` has no native analog — approximate with the existing `data-starting-style`/`data-ending-style` pattern. `errors:string[]` becomes a real `FieldContext` list.

**Blast radius:** `FieldContext`, `NaviusJsInterop` constraint engine, the entire `Form/` folder, and the discrete field-state contract consumed by Checkbox, Switch, Radio, Select, OTP, Slider, Input, Number Field, Autocomplete, Combobox. **New `Fieldset` component** lives here. This contract is the prerequisite for the forms wave.

### 2.4 Utilities — CSP + Direction

**Base UI model:** `CSPProvider` applies a per-request `nonce` to injected inline `<style>`/`<script>` elements (props `nonce`, `disableStyleElements`); does **not** cover inline `style="…"` attributes; Base UI never uses `eval`/`new Function`. `DirectionProvider` (`direction="ltr|rtl"`, default `'ltr'`) cascades reading direction only (incl. `useDirection()` for portals) and does not set CSS itself.

**Navius today:** `NaviusDirectionProvider` is already a faithful match (DOM-transparent, cascades `Dir`). **No CSP utility exists.** Worse, four hot paths call `JS.InvokeAsync("eval", …)` with interpolated source — the exact `unsafe-eval` smell Base UI avoids: NavigationMenu `Viewport` (MirrorScript, line 70), NavigationMenu `Indicator` (PositionScript, line 67), NavigationMenu `Content` (FocusFirstScript, line 230), helm `ZitsCommand` (scrollIntoView, lines 200–206).

**Blazor constraint / approximation:** Blazor doesn't render its own `<style>`/`<script>`; DOM work goes through the pre-bundled `navius-interop.js` static asset (already nonce-free, CSP-clean once `eval` is gone). Two-pronged parity: (a) **eliminate the 4 `eval` sites** by moving snippets into named module exports (`createViewportMirror`, `setIndicatorMetrics`, `focusFirstDescendant`, `scrollIntoViewById`) called via typed args; (b) add a DOM-transparent **`NaviusCspProvider`** (cascades `Nonce` + `DisableStyleElements`) mirroring `NaviusDirectionProvider`.

**Blast radius:** 4 blocker `eval` removals (a security item, not just parity), new CSP provider + ambient service, typed-args injection-surface closure. `DirectionProvider` needs only naming/doc parity (`Dir` → `direction`/`TextDirection`).

---

## 3. Per-Component Delta Table

> Status order: **missing → partial → present-needs-rename.** Anatomy renames and data-attr changes abbreviated; see the JSON source for full per-part matrices.

### Missing (build from scratch)

| Component | Navius equiv | Key anatomy / renames | Data-attr changes | Effort |
|---|---|---|---|---|
| **Checkbox Group** | none (only standalone Checkbox) | NEW `NaviusCheckboxGroup` (`<div role=group>`) + `CheckboxGroupContext`; make `NaviusCheckbox` group-aware; add `parent` checkbox | ADD `data-disabled` + field states on Root; child migrates to discrete `data-checked`/`data-unchecked`/`data-indeterminate` | L |
| **Fieldset** | none | NEW `NaviusFieldset` (`<fieldset>`) + `NaviusFieldsetLegend` (`<div>`, **not** `<legend>`) + `FieldsetContext` | ADD `data-disabled` on Root + Legend; **no** `data-state` | M |
| **Input** | helm `ZitsInput` only (passthrough) | NEW `NaviusInput` primitive (native `<input>`, controlled Value/DefaultValue) | ADD 7 discrete booleans: `data-disabled/valid/invalid/dirty/touched/filled/focused` | L |
| **Meter** | none (Progress is template) | NEW 5-part family `Root/Track/Indicator/Value/Label` + `MeterContext`; `role="meter"` | **NONE** — Base UI Meter has no `data-*`; do NOT carry over Progress `data-state` | M |
| **Number Field** | none (Radix never shipped) | NEW 7-part `Root/Group/Input/Increment/Decrement/ScrubArea/ScrubAreaCursor` + `NumberFieldContext` | ADD field-state booleans + `data-scrubbing` (discrete, no `data-state`) | XL |

### Partial (decompose / restructure)

| Component | Navius equiv | Key anatomy / renames | Data-attr changes | Effort |
|---|---|---|---|---|
| **Autocomplete** | monolithic `NaviusCombobox` (closest) | Decompose into ~19-part compound (Root/Value/Input/Trigger/Icon/Clear/Status/Portal/Backdrop/Positioner/Popup/Arrow/List/Row/Empty/Collection/Group/GroupLabel/Item/Separator) + `AutocompleteContext` | `data-state`→`data-open`/`data-closed`; `data-active`→`data-highlighted`; ADD `data-selected`/`data-empty`/`data-side`/`data-align`/starting-ending + field states | XL |
| **Button** | helm `ZitsButton` only | NEW headless `NaviusButton` primitive; re-base `ZitsButton` (keep Variant/Size in helm) | ADD `data-disabled` (only data-attr); props `focusableWhenDisabled`, `nativeButton`, render swap | M |
| **Combobox** | monolithic single-file (no Context) | Decompose into 26-part compound; NEW `ComboboxContext`; add Chips/multi-select, Group/Row/Collection | `data-state`→discrete; `data-active`→`data-highlighted`; ADD `data-popup-open/pressed/selected/disabled/empty/placeholder/side/align/multiple/readonly` | XL |
| **Drawer** | helm-only (Dialog + `createSheetSwipe`) | Promote to **real primitive**; add `Viewport`/`Popup`/`Content`/`Close` + Provider/Indent/SwipeArea/VirtualKeyboardProvider; Overlay→Backdrop | `data-state`→discrete; ADD `data-swiping`/`data-nested-drawer-open` + starting-ending; rename `data-vaul-drawer-direction` | XL |
| **Field** | `Form/` parts | Rename `Form.*`→`Field.*`; `Message`→`Error` (`<span>`→`<div>`); ADD `Description`(`<p>`), `Item`(`<div>`) | ADD full discrete set on every part; `Error` adds starting-ending; drop `data-match` | L |
| **Navigation Menu** | full Radix port (9 parts) | Nest `Portal>Positioner>Popup(+Arrow)`, Viewport inside Popup; ADD Backdrop/Icon; **remove Indicator** (use Arrow) | `data-state`→discrete; `data-motion`→`data-activation-direction`; ADD `data-popup-open/pressed/side` + starting-ending | XL |
| **Progress** | Root + Indicator only | ADD `Track`, `Value` (render-fn), `Label` parts | `data-state`(loading/complete/indeterminate)→discrete `data-complete`/`data-indeterminate`/`data-progressing` on all parts | M |
| **Slider** | 4-part (Root/Track/Range/Thumb) | ADD `Control`, `Value`(`<output>`), `Label`; rename `Range`→`Indicator`; adopt nested `<input type=range>` Thumb | ADD `data-dragging`/`data-index` + field states (discrete booleans; current `DataState` unused) | L |
| **Toast** | two disjoint systems (parts + `ToastService`) | Unify into `Provider`+`Manager`+`Portal/Viewport/Positioner/Root/Content/Title/Description/Action/Close/Arrow` | `data-state`→`data-open`/`data-closed`; ADD `data-swiping/expanded/limited/type/behind` + starting-ending; rename swipe CSS vars | XL |

### Present — needs rename / contract-only

| Component | Navius equiv | Key anatomy / renames | Data-attr changes | Effort |
|---|---|---|---|---|
| **Accordion** | full | `Content`→`Panel`; re-model Root to array Value/DefaultValue + `Multiple` | `data-state`→`data-open` (Item/Header/Panel), **`data-panel-open`** (Trigger); ADD `data-index`, starting-ending on Panel; drop `data-orientation` on Item/Header/Trigger | L |
| **Alert Dialog** | full | `Overlay`→`Backdrop`, `Content`→`Popup`; ADD `Viewport`, `Close`; reconcile Action/Cancel | `data-state`→`data-open`/`data-closed`; Trigger→`data-popup-open`; ADD starting-ending, `data-nested`/`data-nested-dialog-open` | L |
| **Avatar** | full | none (anatomy 1:1) | **REMOVE** non-spec `data-state` on Image/Fallback; ADD `data-starting-style`/`data-ending-style` on Image only | S |
| **Checkbox** | full | none (Root+Indicator 1:1) | `data-state`→discrete `data-checked`/`data-unchecked`/`data-indeterminate`; ADD `data-readonly/required` + field states | L |
| **Collapsible** | full | `Content`→`Panel` | Panel `data-state`→`data-open`/`data-closed` + starting-ending; Trigger→`data-panel-open`; drop `data-state` from root `<div>` | M |
| **Context Menu** | 20 part files | `Content`→`Popup` + NEW `Positioner`; ADD `Backdrop`/`LinkItem`; split ItemIndicator | `data-state`→discrete everywhere; Trigger `data-popup-open`+`data-pressed`; ADD starting-ending/`data-instant` | XL |
| **Dialog** | full | `Overlay`→`Backdrop`, `Content`→`Popup`; ADD `Viewport` | `data-state`→`data-open`/`data-closed`; ADD starting-ending, `data-nested-dialog-open`, `data-popup-open` on Trigger | L |
| **Form** | `Form/` bundle | Split Field into its own component; root-level `errors` dict | none on `<form>`; field-level discrete booleans (overlaps §2.3) | L |
| **Menu** (`DropdownMenu`) | 16 parts | **Rename `DropdownMenu`→`Menu`**; `Content`→`Positioner`+`Popup`; `Label`→`GroupLabel`; `Sub*`→`Submenu*`; ADD Backdrop/LinkItem | `data-state`→discrete; ADD `data-pressed`/`data-popup-open`/starting-ending; rename CSS vars; drop `data-orientation` | XL |
| **Menubar** | 17 parts | Split `Content`→`Positioner`+`Popup`; ADD Backdrop/Viewport/LinkItem; `Sub*`→`Submenu*` | `data-state`→discrete; ADD `data-has-submenu-open`/`data-modal` on root, `data-popup-open`/`data-pressed` | XL |
| **OTP Field** | Root + Input + HiddenInput | ADD `Separator` part; fold HiddenInput into Root | `data-state`(empty/filled)→`data-filled`; ADD full field-state set on Root+Input; drop `data-navius-*` markers | L |
| **Popover** | 7 Radix parts | Split `Content`→`Positioner`+`Popup`; ADD Backdrop/Title/Description/Viewport; fold `Anchor` into `Positioner.anchor` | `data-state`→discrete; Trigger `data-popup-open`/`data-pressed`; ADD starting-ending/`data-anchor-hidden` | XL |
| **Preview Card** (`HoverCard`) | `HoverCard` (5 parts) | **Rename `HoverCard`→`PreviewCard`**; split `Content`→`Positioner`+`Popup`; ADD Backdrop/Viewport | `data-state`→discrete; Trigger `data-popup-open`; ADD starting-ending/`data-anchor-hidden`/Arrow `data-side` | XL |
| **Radio** | `RadioGroup`/Item/Indicator | `Item`→`Radio.Root`, `Indicator`→`Radio.Indicator` | `data-state`(checked/unchecked)→discrete; ADD field states + Indicator starting-ending | M |
| **Scroll Area** | 5 parts (Content baked in Viewport) | Promote inner content to standalone `Content` part | `data-state`(visible/hidden)→`data-hovering`/`data-scrolling`; ADD overflow booleans; remove `data-type`/`data-orientation` on Root | L |
| **Select** | 15 parts | Split `Content`→`Portal`+`Positioner`+`Popup`; ADD Backdrop; `Viewport`→`List`; `ScrollUp/DownButton`→`*Arrow`; `Label`→`GroupLabel` + new field `Label` | `data-state`→discrete; Item `data-checked`; Trigger `data-popup-open`/field states; ADD starting-ending/`data-side`/`data-anchor-hidden` | XL |
| **Separator** | single file | none (near-1:1) | KEEP `data-orientation`; **remove `Decorative` prop** (no `data-state` involved) | S |
| **Switch** | Root + Thumb | none (anatomy 1:1) | `data-state`(checked/unchecked)→discrete on both parts; ADD `data-readonly/required` + field states | M |
| **Tabs** | 4 parts | `Trigger`→`Tab`, `Content`→`Panel`; ADD **`Indicator`** part (+ `--active-tab-*` vars, JS rect) | Tab `data-state`→`data-active`; Panel→`data-hidden`+`data-index`+starting-ending; ADD `data-activation-direction` on all | L |
| **Toggle** | single button | none (single-part 1:1) | `data-state`(on/off)→`data-pressed` (presence-only) | S |
| **Toggle Group** | root + item | none | Item `data-state`→`data-pressed`; ADD `data-multiple` on root; drop item `data-orientation` | M |
| **Toolbar** | 6 parts | ADD `Input` + generic `Group` parts; reconcile ToggleGroup/ToggleItem | ADD `data-disabled` on root, `data-focusable` on Button/Input; ToggleItem `data-state`→`data-pressed` | L |
| **Tooltip** | Provider/Root/Trigger/Content/Arrow | Split `Content`→`Portal`+`Positioner`+`Popup`; ADD `Viewport` | `data-state`(closed/delayed-open/instant-open)→`data-open`/`data-closed`/`data-instant`/starting-ending; Trigger `data-popup-open` | L |

---

## 4. The 8 Net-New Components

Each must be authored to match its Base UI spec. Five are fully absent; three are promotions of existing monolith/helm code into the headless brain.

- **Autocomplete** — Base UI deliberately splits Autocomplete (free-text input that filters a list; value is the typed string) from Combobox (value selection). The existing `NaviusCombobox` is *really* an Autocomplete by this taxonomy. Build a dedicated `NaviusAutocomplete` compound (~19 parts) with an `AutocompleteContext` cascade: Root + Input + Positioner + Popup + List + Item + Empty + Status + Clear + Trigger + Icon + Group/GroupLabel/Collection. The positioning/dismiss JS interop is reusable, lowering cost; the work is decomposition, discrete data-attrs, controlled+uncontrolled open/value, generic `Items<T>` + pluggable `Filter`, `Mode`/`AutoHighlight`, and Home/End/PageUp/PageDown nav. Effectively a ground-up compound, not a rename.
- **Button** — Base UI Button is a single headless root whose entire contract is one `data-disabled` attribute plus `focusableWhenDisabled`, `nativeButton`, and `render`. Create `NaviusButton` providing `data-disabled`, `focusableWhenDisabled` (drop native `disabled`, set `aria-disabled`+`tabindex`, suppress activation while focusable), `nativeButton=false` (role=button + Enter-keydown/Space-keyup activation with scroll `preventDefault`), and an AsChild/render element swap. Re-base helm `ZitsButton`, keeping Variant/Size as helm-only styling (no Base UI analogue).
- **Checkbox Group** — A single `<div role="group">` Root coordinating child `Checkbox.Root` by name via `value`/`defaultValue`/`onValueChange`/`allValues`/`disabled`/`name`, plus a `parent` select-all checkbox with automatic indeterminate roll-up. Build `NaviusCheckboxGroup` + `CheckboxGroupContext`, make `NaviusCheckbox` group-aware (derive checked state from the group's `value[]`, route toggles through the group), and migrate the child to discrete `data-checked`/`data-unchecked`/`data-indeterminate`. No CSS vars, no group-specific keyboard contract (Space/Tab inherited from the checkbox).
- **Drawer (brain)** — Base UI Drawer is an *extension of Dialog* with a far richer anatomy (Backdrop/Viewport/Popup/Content/Close + Provider/IndentBackground/Indent + SwipeArea + VirtualKeyboardProvider), discrete data-attrs (`data-open`/`data-closed`/`data-swiping`/`data-nested-drawer-open`/starting-ending), **snap points**, and a large `--drawer-*` CSS-var surface. Today Drawer is helm-only (`NaviusDialog` + `createSheetSwipe` on an inner `_panel`). Promote it to a real `Navius.Primitives` primitive so the data contract and swipe/snap engine live in the brain like every other component; the heavy lifts are the snap-point engine, the CSS-var surface, the indent system, and nested-drawer stacking.
- **Fieldset** — Base UI's simplest component: `Fieldset.Root` (native `<fieldset>`) + `Fieldset.Legend` (a `<div>`, **not** native `<legend>`, for positioning freedom), one `Disabled` state, no value/open lifecycle, no CSS vars, no keyboard. Build the two parts + `FieldsetContext` carrying `Disabled`, cascade disabled to the Legend and descendant fields, and emit a discrete `data-disabled` (never `data-state`). Wire `FieldsetContext.Disabled` into the Field context so contained fields reflect disabled.
- **Input** — A single-part, field-aware native `<input>` with controlled/uncontrolled value (`Value`/`DefaultValue`/`OnValueChange`) and the 7 discrete field-state booleans wired into `Field.Root`. Today only a styled helm passthrough exists. Build the primitive (native input + binding + 7 attrs + Field-context consumption), track focused/filled/dirty/touched locally for standalone use, then rewire `ZitsInput` to consume it. Main external blocker: the Field primitive (§2.3) must exist first; keyboard parity is free via native input.
- **Meter** — A static, non-interactive readout: `Root` (`role="meter"`, `aria-valuenow/min/max/valuetext`) + `Track` + `Indicator` + `Value` (render-fn over `(formattedValue, value)`) + `Label`, modeled on `NaviusProgress`. Critical divergences from Progress: `role="meter"` not `progressbar`; value is **required/determinate** (no indeterminate path); NEW `Min` prop with min-aware fraction math `(value-min)/(max-min)`; Intl-style `Format`+`Locale` via .NET `CultureInfo`/`NumberFormatInfo`. Base UI Meter documents **no `data-*`, no CSS vars, no keyboard** — do not carry over Progress's `data-state`.
- **Number Field** — Radix never shipped this, so it is a ground-up build of 7 parts (`Root/Group/Input/Increment/Decrement/ScrubArea/ScrubAreaCursor`) over a `NumberFieldContext` handling controlled/uncontrolled value, `min`/`max`/`step` (incl. literal `'any'`)/`smallStep`/`largeStep`/`snapOnStep`, locale+Intl formatting/parsing, clamping, and a change-reason model (`OnValueChange`/`OnValueCommitted`). Data contract is discrete field-state booleans + `data-scrubbing`. The heavy/distinct work: scrub-area pointer-drag with Pointer Lock, button press-and-hold auto-repeat, the full keyboard set (Alt/Shift/Page/Home/End), and Field/Form validation coupling.

---

## 5. Phased Roadmap

Waves are ordered by **dependency then risk**: contracts first, then low-risk leaves that validate the new engine, then the forms foundation (which gates many inputs), then the shared Positioner/Popup machinery and the popup families that depend on it.

### Wave 0 — Contract foundation (blocking everything)
**In it:** the four §2 contracts — (a) composition/render + `NaviusSlot` state channel; (b) the discrete-attr engine + `data-starting-style`/`data-ending-style` + `getAnimations()` teardown + `ForceMount`→`keepMounted` + CSS-var rename in `createPositioner`/`createSizeObserver`; (c) `FieldContext` interaction-state tracking + JS focus/blur/input bridge; (d) the 4 `eval` removals + `NaviusCspProvider` + DirectionProvider doc parity.
**Why first:** every per-component task emits discrete attrs, animates via starting/ending-style, and (for inputs) reads field state. Building the per-component `data-state` → discrete mapping table is the gating deliverable.
**Effort:** very high (two XL items in §2.2 plus three blocker field-state items in §2.3). The `eval` removals are also a security fix.
**Verification:** migrate one pilot component (e.g. Collapsible) end-to-end; update its Playwright assertions from `toHaveAttribute('data-state', …)` to discrete `toHaveAttribute('data-open','')` / `not.toHaveAttribute(...)`; confirm exit animation plays before unmount in headless Chromium; confirm zero `JS.Invoke("eval", …)` call sites remain and the suite passes under a strict CSP header (no `unsafe-eval`/`unsafe-inline`).

### Wave A — Leaf primitives & quick wins (low risk, scale the engine)
**In it:** Avatar (S), Separator (S), Toggle (S), Toggle Group (M), **Button** (new, M), **Meter** (new, M), Progress (M), Collapsible (M), Accordion (L), Tabs (L), Scroll Area (L), OTP Field (L).
**Why this order:** these are single/few-part, **no positioner**, so they exercise the discrete-attr + starting/ending-style migration broadly without the Positioner/Popup split. Tabs and Scroll Area add small JS-rect/observer surfaces (Indicator `--active-tab-*`, overflow vars). Quick `S` wins (Avatar/Separator/Toggle) de-risk the pipeline first.
**Effort:** moderate; mostly mechanical attribute swaps + a few new parts (Track/Value/Label, Tabs Indicator, ScrollArea Content).
**Verification:** per-component Playwright specs asserting discrete attrs, presence/absence on toggle, Indicator CSS-var emission (Tabs), overflow attrs (ScrollArea), and that Meter emits **no** `data-state` and `role="meter"`.

### Wave B — Forms foundation (gates the inputs)
**In it:** **Field** (rename `Form.*`→`Field.*`, L), **Fieldset** (new, M), **Form** (errors dict, L), **Input** (new, L), Checkbox (L), **Checkbox Group** (new, L), Radio (M), Switch (M), Slider (L), **Number Field** (new, XL).
**Why this order:** all depend on Wave 0's field-state contract; Field/Fieldset/Form/Input must land before the controls that consume `data-valid/invalid/dirty/touched/filled/focused`. Checkbox precedes Checkbox Group; Input precedes Number Field.
**Effort:** high (Number Field is XL; Slider's nested `<input type=range>` and Checkbox Group's parent roll-up are large).
**Verification:** Playwright field-state matrices (focus→`data-focused`, blur→`data-touched`, edit→`data-dirty`, value→`data-filled`), validation-mode timing (`onBlur`/`onChange`/`onSubmit`), Number Field keyboard/scrub, Checkbox Group parent indeterminate roll-up, and form `errors`-by-name auto-clear on edit.

### Wave C — Positioner/Popup machinery + dialog/overlay family
**STATUS: COMPLETE (2026-06-30) — build 0 errors, 60/60 Playwright green.** Shared machinery in `Components/Overlays/` (`IOverlayContext`/`IAnchoredOverlayContext` + `OverlayPresence`→`OverlayPopupBase`→`OverlayAnchoredPopupBase` + `OverlayPositionerBase`). All six migrated; HoverCard renamed to PreviewCard; Drawer promoted to a real `Components/Drawer/` primitive (modal sheet + `createSheetSwipe` drag-to-dismiss). Engine dual-emits the un-prefixed CSS vars + `data-anchor-hidden` + `data-swiping`. In Blazor the `Portal`/`Positioner` parts are flag-setters and the `Popup` owns the lifecycle (one component owns mount/unmount, so exit animations are correct) — consumer-facing parts stay 1:1 with Base UI. Deferred (additive): `Viewport` everywhere; nested-dialog attrs/`--nested-dialogs`; Drawer snap-points/SwipeArea/Indent/Provider/virtual-keyboard.
**In it:** build the shared `Positioner`/`Popup`/`Backdrop`/`Arrow`/`Viewport` scaffolding, then Popover (XL), Tooltip (L), Preview Card (XL, **rename `HoverCard`**), Dialog (L), Alert Dialog (L), **Drawer** (promote to primitive, XL).
**Why this order:** the `Content`→`Positioner`+`Popup` split is the single most-repeated structural change; establishing the shared parts + CSS-var emission (`--anchor-*`/`--available-*`/`--transform-origin`) once lets the dialog/overlay family (single-trigger, no roving) adopt it before the more complex menu family. The positioning engine already computes side/align, so this is mostly re-homing logic onto new part boundaries.
**Effort:** high; Drawer's snap/swipe engine and nested stacking dominate.
**Verification:** Playwright positioning specs (`data-side`/`data-align`/`data-anchor-hidden`, CSS-var presence), open/close starting-ending transitions, focus trap (Dialog/AlertDialog), nested-dialog `data-nested-dialog-open` + `--nested-dialogs`, Drawer swipe `data-swiping` + snap points.

### Wave D — Menu family (highest complexity, reuse Wave C machinery)
**STATUS: CONTRACT RE-BASE COMPLETE (2026-07-01) — build 0 errors, 60/60 Playwright green.** `DropdownMenu`→`Menu` renamed end-to-end (folder/namespace/types/helm `Zits*`/`data-navius-*` markers/playground/tests/README). **Menu** is fully migrated as the reference: `Content`→`Positioner`+`Popup` on the shared `OverlayAnchoredPopupBase` + roving focus (a menu roves, it does not focus-trap), `Portal` is now a flag-setter, discrete attrs (Trigger `data-popup-open`; Popup `data-open`/`data-closed`+starting/ending; items `data-checked`/`data-unchecked`/`data-indeterminate`), `Label`→`GroupLabel`. Context Menu / Menubar / Select / Navigation Menu received the **discrete-attr contract** (same Trigger/popup/item mapping; Select `Item`→`data-selected`, Trigger→`data-popup-open`). The legacy `--navius-popper-*` aliases were **retired** — `createPositioner` now emits only `--anchor-*`/`--available-*`/`--transform-origin`. **STRUCTURAL SPLIT now done for 4 of 5** (build 0 errors, 60/60 green): **Menubar** + **Select** (`Content`→`Positioner`+`Popup` on `OverlayAnchoredPopupBase`; Select non-modal, `role=listbox`) and **ContextMenu** (cursor-anchor variant — the shared `OverlayPopupBase` gained a `DismissReference` virtual, default-preserving, so a click anywhere outside the popup dismisses even over a large trigger area; the Popup registers its 0×0 cursor-anchor before `base.EngageAsync`). **STRUCTURAL SPLIT now COMPLETE for all 5** — **NavMenu** rewritten (`35319e8`): the bespoke dual-mode viewport (standalone anchored popups OR a shared morphing Viewport), `Indicator` DELETED (→ `Arrow`), retiring the **last rendered `data-state` in the library**; `data-motion`→`data-activation-direction`; nested `Portal>Positioner>Popup(+Arrow)`; adversarial review found + fixed 4 lifecycle bugs (keyboard-into-panel, shared-morph dismiss rebuild, arrow slot-clobber, un-teleport DOM leak). **Deferred minors landed this session:** Select `OpenFocusMode` first/last (ArrowUp-on-closed lands on the LAST option, via a roving `InitialFocus`) + Menubar `Modal=false` override (`NaviusMenubar.Modal`, default true). **Post-re-target follow-up (session 2) — LANDED:** **RTL-aware positioner math** (`50db3a0` — `createPositioner` mirrors logical `align` on the horizontal axis for `dir=rtl` by reading the anchor's computed direction; LTR path byte-for-byte unchanged; + an RTL Popover positioning test) and **Select `Multiple`** (`95f94cb` — multi-value set + joined-label summary + per-value hidden form mirror, mirroring the Combobox value model; single path byte-for-byte unchanged; + a multi test). **Still deferred (all reasoned DELIBERATE deferrals, not blockers):** submenu `Sub*`→`Submenu*` rename + Positioner/Popup split (a *structural rewrite* of 3 submenus that still hand-roll their own positioner/dismiss/roving off the shared machinery, over **zero-coverage** behavior — a blind rewrite of fiddly untested logic for a cosmetic *internal* naming gap; the helm API stays `Sub*` regardless — too risky without first landing submenu demos+specs); Select `AlignItemWithTrigger` (needs a genuinely new engine positioning *mode* — an inner-selected-item offset `createPositioner` can't express — and is mutually exclusive with `Multiple`); the `Viewport` animated-content-swap part on the dialog/overlay family (niche polish); nested-dialog/menu attrs (`data-nested`/`data-nested-dialog-open`/`--nested-dialogs` — needs a new nesting-detection mechanism + demo/test); and RTL *side*-flipping for submenus (the *align* mirror is done; flipping physical side left↔right for RTL submenus is component-level, separate).
**In it:** Menu (**rename `DropdownMenu`→`Menu`**, XL), Context Menu (XL), Menubar (XL), Navigation Menu (XL), Select (XL).
**Why this order:** all reuse the Positioner/Popup parts from Wave C and add roving focus, submenus, checkbox/radio items, and `LinkItem`. They share one Radix-shaped template, so Menu becomes the reference and Context Menu/Menubar/NavMenu/Select follow. NavMenu additionally consumes Wave 0's `eval` removals.
**Effort:** highest in the project (five XL); the Positioner/Popup split + discrete-boolean migration touches every file in each folder (Menu ~24 files).
**Verification:** roving-focus + type-ahead + Home/End/ArrowLeft-closes-submenu Playwright specs; discrete item attrs (`data-highlighted`, `data-checked`/`data-unchecked`, `data-popup-open`); Select `Multiple` + `AlignItemWithTrigger`; Menubar open-state model decision (central vs per-`Menu.Root`).

### Wave E — Remaining compound input/overlay (XL rewrites)
**STATUS: COMPLETE (2026-07-01) — build 0 errors, full Playwright suite green.** All three near-total rewrites shipped, each built + adversarially reviewed (multi-lens Workflow → verify) + fixed + committed per-step: **Autocomplete** (`73619db`) — the old monolithic Combobox decomposed into the ~19-part `NaviusAutocomplete` compound (virtual focus via `aria-activedescendant`, `MoveFocusInside=false`, non-modal, on the shared `OverlayAnchoredPopupBase`); the review's engine fixes (a `ResizeObserver` re-place in `createPositioner` + an optional secondary dismiss reference) benefit every anchored overlay. **Combobox** (`9436d98`) — a NEW value-selection `NaviusCombobox` compound (26 parts, chips/multi-select, value tracked separately from the filter text); review caught a critical chip stale-target (frozen `IsFixed` context + keyless loop) + the `@bind-InputValue` desync, both fixed. **Toast** (`2f189eb`) — the two disjoint systems (parts + `ToastService`) unified into an injectable `ToastManager` + parts, Root moved onto `OverlayPresence`, C#-computed stacking vars (`--toast-index`/`-offset-y`/`-height`), `data-swiping`/`data-expanded`/`data-limited`/`data-type`, F6 hotkey, swipe var renamed to `--toast-swipe-movement-*`; review found + fixed 6 lifecycle/timer bugs (demotion-keeps-timer, Promise-never-dismisses, Priority-ignored, height-0-when-limited, never-engaged leak, UpdateKey no-op). The discrete-attribute contract re-base is now complete across the **entire** library.

**In it:** Autocomplete (new compound, XL), Combobox (XL), Toast (XL unify Manager, XL).
**Why last:** these are near-total rewrites (monolith→compound, two-system→unified Manager) that benefit from every prior wave — the field-state contract (B), the Positioner/Popup parts (C), the discrete-attr + animation engine (0). Autocomplete and Combobox reuse the menu/positioner patterns from D; Toast reuses the discrete-attr + starting/ending model and adds the stacking layout.
**Effort:** high; Toast's stacking model + Manager unification and the Combobox 26-part decomposition are the largest single items here.
**Verification:** Autocomplete/Combobox compound specs (controlled open/value, generic items, multi-select chips with Backspace-removes-last, grouping); Toast stacking CSS vars (`--toast-index`/`--toast-offset-y`/`--toast-height`), `limit`/queueing, `data-expanded`/`data-limited`, F6 viewport hotkey, and Manager `add`/`close`/`update`/`promise`.

---

## 6. Risks & Open Questions

**Naming (decide before Wave A; renames ripple through helm + tests + docs):**
- **`DropdownMenu` → `Menu`** and **`HoverCard` → `PreviewCard`** to match Base UI's component names — or keep the Navius names as documented aliases? A strict 1:1 argues for the rename; an alias preserves consumer code. Recommend rename with a one-release alias shim.
- Rename Navius's `Form.*` namespace to **`Field.*`** and split Field into its own component folder (Base UI separates `Form` from `Field`). Navius currently fuses them.
- Per-part renames: `Content`→`Popup`/`Panel`, `Overlay`→`Backdrop`, `Sub*`→`Submenu*`, `Label`→`GroupLabel`, split `ItemIndicator`→`CheckboxItemIndicator`/`RadioItemIndicator`, `ScrollUp/DownButton`→`*Arrow`. Decide whether to keep `Navius*` prefixes on every part (Base UI uses bare `Accordion.Item`); the prefix is a Blazor-friendly convention but diverges cosmetically.

**The 7 Navius extras with no Base UI equivalent** (`AspectRatio`, `AccessibleIcon`, `VisuallyHidden`, `Label`, `PasswordToggleField`, `Slot`, `DataGrid`): keep as a documented **Navius superset** or drop for strict purity? Recommendation: keep `VisuallyHidden`/`AccessibleIcon`/`AspectRatio`/`Label`/`Slot` (broadly useful, low maintenance), flag `PasswordToggleField` and `DataGrid` for a separate decision (DataGrid has no Base UI analogue and is large). Several components also carry per-component extras with no Base UI home — Accordion `Collapsible` + Header `Level`; Dialog/Popover/Tooltip/Select/Menu Radix-style cancelable layer events (`OnEscapeKeyDown`/`OnPointerDownOutside`/`OnInteractOutside`/`OnCloseAutoFocus`); Toggle Group `RovingFocus`; Slider/Toolbar `Inverted`/`Dir`; OTP `Orientation`/`Placeholder`/`AutoFocus`; Form `ServerInvalid`/`ForceMatch`/`MatchFn`. Each needs a keep-vs-drop ruling; Base UI folds the cancelable events into `actionsRef`/`finalFocus`/`onOpenChange` event-details.

**The render-prop Blazor limit (ADR-0003):** `RenderFragment` is opaque — Base UI's element-form `render={<el/>}` (clone + merge + ref-forward) is **not generally achievable**, ref-merging across the boundary is impossible, and `event.preventBaseUIHandler()` has no DOM-level equivalent. The supported approximation is the **inverted contract** (Navius emits a merged attribute dict + state; the consumer splats onto their own root via `@attributes`). This must be documented as a deliberate, permanent deviation — it is the one place Navius cannot be byte-for-byte 1:1.

**Other open questions:**
- **Animation teardown semantics:** Base UI gets exit animations "for free" on unmount via `getAnimations()`; Navius must wire an explicit JS await into `OnAfterRenderAsync`. Edge cases (rapid open/close, reduced-motion, server prerender) need test coverage — `data-starting-style` requires a JS rAF round-trip that has no synchronous Blazor analog.
- **CSS-var naming:** rename `--navius-popper-*`/`--navius-*-content-*` to Base UI's un-prefixed names, **or** keep navius-prefixed aliases? Un-prefixed is strict 1:1 but risks collisions in consumer stylesheets; recommend Base UI names with the navius names retained as documented aliases for one release.
- **Menubar open-state model:** Base UI Menubar is a *stateless* container with open state on each `Menu.Root`; Navius centralizes it in `MenubarContext.OpenValue` via `@bind-Value`. Moving to per-`Menu.Root` state is the path to strict parity but is a behavioral break — decide vs. documenting the central model as an intentional deviation.
- **Default behavior breaks to flag loudly:** `loopFocus` default flips `false`→`true` (Menu/Menubar/Toggle Group/Toolbar); NavMenu delays `200/300`→`50/50`; Preview Card delays move Root→Trigger and `700`→`600`; Accordion single-mode collapse semantics differ from Navius `Collapsible=false`. These are silent runtime changes consumers will notice.