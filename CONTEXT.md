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

OSS boundary: the public technology repos are
`https://github.com/lzitser23/Navius` and
`https://github.com/lzitser23/Zits-helm`. The `navius-docs` and `zits-ui` docs/showcase
repos are private sibling repos and should not be presented as public GitHub surfaces.

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
hidden-until-found (beforematch) observer, message scroller (anchored turns,
streamed-reply follow, prepend scroll preservation, lazy visibility tracking),
sortable drag-reorder, file dropzone, masked-input selection, 2D pointer tracker,
keyboard-shortcut listener, and small DOM readers.

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

## NuGet preview readiness

2026-07-05 release-prep pass moved the packable surface from implicit SDK defaults to
explicit preview metadata:

- Package IDs/versions: `Navius.Primitives`, `Navius.Motion`, `Zits.Ui`, and the
  dotnet tool `navius` pack as `0.3.0-preview.2` (`Navius.Motion` stays at `0.3.0-preview.1`, unchanged this release).
- Package metadata now includes MIT license expressions, package readmes, repository
  URLs, tags, SourceLink, `.snupkg` symbols, and package validation on the libraries.
- `Zits.Ui` still uses a sibling `ProjectReference` during development, but pack output
  resolves the dependency as `Navius.Primitives 0.3.0-preview.2`.
- The installed `navius` tool is self-contained: if no repo-local
  `registry/registry.json` exists, it falls back to the registry and `registry-source`
  payload bundled inside the tool package. `--root` and `--registry` still support
  local/custom registries.
- CI in `navius` now Release-builds, tests, and packs `Navius.Primitives` and
  `Navius.Motion`. CI in `zits-helm` now Release-builds, tests, packs `Zits.Ui` and
  `navius`, then installs the local tool package and smoke-tests `navius list` plus
  `navius add button`.
- Local verification passed: `Navius.Motion.Tests` 84/84, `Zits.Ui.Tests` 11/11, all
  four `.nupkg` and `.snupkg` files packed, and the locally installed `navius` tool
  copied `button` from the bundled registry.

## OSS readiness

2026-07-05 `oss-ready` pass covered both open-source technology repos:
`E:\Lzitser\navius` and `E:\Lzitser\zits-helm`.

- GitHub remotes exist as private repos for now:
  `lzitser23/Navius` and `lzitser23/Zits-helm`, default branch `main`.
- Public package and README links must use those canonical repo names. Do not link the
  private docs repos from package metadata or public README surfaces.
- Root `LICENSE`, `README.md`, `.gitignore`, `.gitattributes`, and CI are present in
  both technology repos.
- Scanner result: no high-confidence working-tree secrets, no history secrets, no
  tracked sensitive/junk filenames, and no tracked files over 5 MB in either repo.
- GitHub Actions reference no repo secrets or variables beyond automatic
  `GITHUB_TOKEN`; no `gh secret set` / `gh variable set` handoff is needed today.
- Remaining scanner warnings are expected public owner/license/repo identity strings
  plus known false positives in generated SVG path data, Node version ranges, and
  prose containing words like `home/end`.
- Do not run fresh-history migration, push to a new public remote, or flip visibility
  without an explicit target `OWNER/REPO` and explicit user confirmation.

## Docs QA notes

2026-07-04 pass covered both sibling docs sites: `../zits-ui` on port 5298 and
`../navius-docs` on port 5299. The browser harness crawled route files plus sidebar
links, then checked desktop `1280x900`, tablet `768x1024`, and mobile `390x844` for
page errors, horizontal overflow, uncontained tables/pre blocks/media, chart SVG escape,
and the Navius release-list flex regression. Final numbers: zits/ui `98` routes +
`97` internal links, Navius docs `70` routes + `69` internal links, `504` viewport page
visits, zero issues.

Fixes from that pass:
- zits/ui chart SVGs must keep the plot wrapper `min-h-0 flex-1` and the SVG `block
  h-full w-full`; otherwise the chart can overflow its card vertically.
- Docs preview frames and docs tables should be `min-w-0` with `overflow-x-auto` at
  the frame/table wrapper, not allowed to widen the page.
- Navius release notes must not apply list-level `[_li]:flex` to prose-heavy list
  items with inline links and code. A global guard in `navius-docs` also neutralizes
  that exact utility combination if it appears again.
- The Navius marketing header keeps its full nav at `lg` and above; showing it at
  `md` made the search/GitHub controls overflow the tablet viewport.
- zits/ui docs links `favicon.png` explicitly so clean browser QA does not report the
  implicit `/favicon.ico` 404.
- 2026-07-05 zits/ui favicon was replaced with a raster mark based on the animated
  stacked-bar logo.

## Build / run / test (environment-specific)

- **Requires the .NET 8 SDK** (plain `dotnet` on PATH).
- Build: `dotnet build playground/Navius.Playground/Navius.Playground.csproj` (builds
  the whole graph: brain + sibling helm + showcase — requires the `zits-helm` sibling
  checkout, ADR-0008).
- Run: `dotnet run --project playground/Navius.Playground --urls http://localhost:5247`.
  **Kill port 5247 before rebuilding** (the dev server locks bin output). Routes: `/`,
  `/wave1`–`/wave3`, `/ui` and `/fidelity` (the helm showcase), plus `/dates`,
  `/pickers`, `/sort`, `/tree`, `/tokens`, `/services`, `/extras`, `/uncontrolled`,
  `/motion`, `/charts`, `/chat`.
- Test: `cd tests/e2e; DOTNET_EXE=<sdk dotnet> CI=1 npx playwright test`. The
  `webServer` auto-launches the app. Note: the background-task exit code can read 0
  even when Playwright failed — always grep the `N passed / N failed` summary line.

## See also

- `docs/adr/` — the why behind the load-bearing decisions.
- `README.md` — overview + prior-art acknowledgements.
- `docs/base-ui-parity.md` — the Base UI parity dossier; current status lives in its
  STATUS markers.
