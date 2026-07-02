# CONTEXT — Navius & zits/ui

Domain map and conventions for this repository. Read this before changing code; it
explains the language, the layers, and the invariants that the tests and the
component contracts depend on.

## What this is

A two-layer Blazor UI system:

- **Navius** — the **brain**: a headless, accessible primitive library (no styling).
  Each primitive owns ARIA roles, keyboard interaction, focus management, and a
  `data-*` state contract. This is the hard, stable half.
- **zits/ui** — the **helm**: the styled layer (Tailwind + a token theme) built on
  top of the brain, distributed copy-paste so consumers own their markup.

The split mirrors the React ecosystem's "headless primitives + styled components"
model, ported to Blazor. Attribution for the prior art lives in the README.

## The three layers (precise)

| Layer | Project | Role |
|---|---|---|
| **brain** | `src/Navius.Primitives` | Headless primitives (`Navius*` components). Owns behaviour + accessibility. Razor Class Library, packable. |
| **engine** | `src/Navius.Primitives/wwwroot/navius-interop.js` (+ `Interop/NaviusJsInterop.cs`) | The DOM-touching behaviour C# can't do synchronously. Driven from C# over JS interop. |
| **helm** | `../zits-helm/src/Zits.Ui` (sibling repo, ADR-0008) | Styled `Zits*` components wrapping the brain with Tailwind + the OKLCH token theme. |

Plus, in this repo: `playground/Navius.Playground` (WASM demo + showcase of brain AND
helm) and `tests/e2e` (Playwright, browser-verifies behaviour). The `navius` CLI and
the copy-paste registry live with the helm in the sibling `zits-helm` repo.

## Why an engine exists (the central constraint)

Blazor cannot synchronously touch the DOM: WASM has no DOM and Server marshals interop
async over SignalR. So a correct focus-trap, scroll-lock, anchored positioning,
roving-tabindex, content teleport, or pointer-drag **cannot be pure C#** — that
behaviour must live in JavaScript and be driven from C#. `navius-interop.js` is that
engine. It is additive and backward-compatible by contract: existing exported
functions keep their signatures; new capability arrives as optional options or new
functions.

Engine capabilities (current): focus-trap (with `initialFocus`/`autoFocus`/skip-restore
release), scroll-lock (ref-counted), anchored positioner (flip/clamp/sticky/arrow +
CSS vars + `data-side`/`data-align`), dismissable layer, roving focus
(`loop`/`autoFocus`/`dataHighlight`/`dir` + a nested-layer guard), teleport-to-body
(focus-preserving), size observer, constraint-validation reader, form submit/reset
listeners, long-press, drag tracker (with commit), scroll-area observer + thumb drag
(RTL-aware), toast interactions (swipe/pause/hotkey), carousel controller, sheet
swipe-to-dismiss, NavigationMenu viewport mirror + indicator positioning,
hidden-until-found (beforematch) observer, and small DOM readers.

> ⚠️ **Validate engine JS as an ES module**, not a script: `node --input-type=module
> --check < navius-interop.js`. Plain `node --check` parses loose-mode and misses
> ESM-only errors (e.g. a `const` shadowing a function parameter) that break the
> browser's `import()` and silently disable ALL interop.

## Component conventions (invariants — don't break these)

- **Headless seam.** Every primitive forwards unmatched attributes via
  `[Parameter(CaptureUnmatchedValues = true)] IDictionary<string,object>? Attributes`
  splatted with `@attributes`. All visible classes come from the consumer.
- **Controlled + uncontrolled.** Controlled = a `Value`/`Open` parameter + a matching
  `ValueChanged`/`OpenChanged` `EventCallback` (for `@bind-`). Uncontrolled =
  `DefaultValue`/`DefaultOpen`. Controlled-ness is determined by whether the parameter
  was set (tracked in `SetParametersAsync`), NOT by `EventCallback.HasDelegate`.
- **State contract (Base UI, per ADR-0007).** Reproduce Base UI's discrete
  boolean-presence attributes — `data-open`/`data-closed`, `data-checked`/
  `data-unchecked`/`data-indeterminate`, `data-pressed`, `data-popup-open`,
  `data-starting-style`/`data-ending-style`, the field states `data-valid`/
  `data-invalid`/`data-dirty`/`data-touched`/`data-filled`/`data-focused` — plus
  `data-side`, `data-align`, `data-orientation`, `data-disabled`, and
  `data-highlighted`. Styling and tests key off these. **No rendered `data-state`**
  (that was the legacy Radix contract, fully retired by the re-target).
- **Test hooks.** New parts expose a `data-navius-<component>-<part>` attribute. The
  Playwright suite selects on these — never rename or remove an existing one; only add.
- **Shared infra** (`src/Navius.Primitives/Common`): `NaviusBubbleInput` (visually
  hidden native input for real form submission), the cancelable event-arg types
  (`NaviusEscapeKeyDownEventArgs`, `…PointerDownOutside…`, `…OpenAutoFocus…`, etc.;
  each has `DefaultPrevented` + `PreventDefault()`), consumed as
  `EventCallback<...>` content/item callbacks invoked BEFORE the default action.
- **Direction.** `NaviusDirectionProvider` is DOM-transparent and cascades
  `[CascadingValue(Name="NaviusDirection")]`; direction-aware parts read
  `[CascadingParameter(Name="NaviusDirection")]` and thread `dir` into the engine.

## The helm seam (zits/ui)

Styled wrappers merge classes with `Cn`:
- `Cn.Class(base, variant, Cn.UserClass(Attributes))` for an explicit
  `class="@..."` placed AFTER `@attributes` (last attribute wins — no duplicate-key
  throw).
- `Cn.Merge(Attributes, base)` returns a splat dictionary with `class` pre-merged,
  for forwarding everything (params + merged class) onto an inner brain component in
  one `@attributes="Cn.Merge(...)"`.
- **Razor gotcha:** a double-quoted string literal cannot appear inside
  `@attributes="Cn.Merge(Attributes, "...")"` — it breaks the attribute delimiter.
  Put the base classes in a `@code` `const`.

Theme: `../zits-helm/src/Zits.Ui/wwwroot/zits-ui.css` holds the OKLCH token palette (`:root` +
`.dark`) and a Tailwind v4 `@theme inline` mapping. Production builds with the Tailwind
v4 standalone CLI (no Node); the playground uses the Play CDN mapped to the same tokens
as a dev shortcut.

## Build / run / test (environment-specific)

- **Requires the .NET 8 SDK** (plain `dotnet` on PATH).
- Build: `dotnet build playground/Navius.Playground/Navius.Playground.csproj` (builds
  the whole graph: brain + sibling helm + showcase — requires the `zits-helm` sibling
  checkout, ADR-0008).
- Run: `dotnet run --project playground/Navius.Playground --urls http://localhost:5247`.
  **Kill port 5247 before rebuilding** (the dev server locks bin output). Routes: `/`,
  `/wave1`–`/wave3`, `/ui` and `/fidelity` (the helm showcase).
- Test: `cd tests/e2e; DOTNET_EXE=<sdk dotnet> CI=1 npx playwright test`. The
  `webServer` auto-launches the app. Note: the background-task exit code can read 0
  even when Playwright failed — always grep the `N passed / N failed` summary line.

## See also

- `docs/adr/` — the why behind the load-bearing decisions.
- `README.md` — overview + prior-art acknowledgements.
- `docs/base-ui-parity.md` — the Base UI parity dossier; current status lives in its
  STATUS markers.
