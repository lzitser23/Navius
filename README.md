<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/navius-mark-dark.svg">
  <img src="assets/navius-mark-light.svg" alt="The navius mark: an N drawn as dots on a 4x4 lattice" width="88" height="88">
</picture>

# navius

**Headless UI primitives for Blazor: ARIA roles, keyboard maps, and focus management built in. The brain, not the paint.**

[Overview](#overview) ·
[Documentation](#documentation) ·
[Features](#features) ·
[Installation](#installation) ·
[Quick start](#quick-start) ·
[Packages](#packages) ·
[Stack](#stack) ·
[Development](#development) ·
[Architecture](#architecture) ·
[Acknowledgments](#acknowledgments)

![License: MIT](https://img.shields.io/badge/license-MIT-171614)
![.NET 8](https://img.shields.io/badge/.NET-8.0-171614)
![Blazor](https://img.shields.io/badge/Blazor-WASM%20%2B%20Server-737270)
![Contract](https://img.shields.io/badge/contract-Base%20UI-737270)
![Tests](https://img.shields.io/badge/e2e-183%20Playwright-737270)

</div>

---

## Overview

navius is an unstyled primitive library for Blazor: the headless "brain"
that owns behaviour so a styled layer does not have to re-solve it. Each primitive
implements the anatomy, ARIA roles, keyboard maps, and discrete `data-*` contracts
of the [Base UI](https://base-ui.com) pattern it mirrors 1:1, expressed natively in Razor.

The bet: the styled-component look for Blazor is commoditized, but nobody ships the
part that makes it actually work. In Blazor, a correct focus trap, scroll lock, or
anchored popover **cannot be pure C#** (WebAssembly has no DOM; Server marshals interop
async over SignalR), so that behaviour lives in a small JavaScript engine driven from
C#. Every other Blazor component kit either skips this behaviour or hides it. navius
builds it in the open.

> Named after **Attus Navius**, the Roman augur who sliced a whetstone with a razor
> before King Tarquin. (Razor, meet .NET **Razor**.)

---

## Documentation

- **[naviusui.dev](https://naviusui.dev)**: component documentation site, per-component
  anatomy, API reference, `data-*` attribute contracts, keyboard tables, and live
  demos.
- **[docs.naviusui.dev](https://docs.naviusui.dev)**: agent-first documentation, one
  structured markdown manifest per component (frontmatter contract, API/data/keyboard
  tables, ARIA mechanism), served as raw markdown with an `llms.txt` index, for AI
  agents and tooling that consume the library.

---

## Features

- **58 primitive, utility, and infrastructure families** under
  `src/Navius.Primitives/Components`, including product-facing primitives plus
  providers such as `CspProvider`, `DirectionProvider`, `Slot`, and shared overlay
  bases. The user-facing primitives align to the Base UI contract: discrete
  boolean-presence attributes (`data-open` / `data-closed`, `data-checked`,
  `data-popup-open`, field states), the `Portal -> Positioner -> Popup` overlay
  anatomy, and the `data-starting-style` / `data-ending-style` presence model for
  CSS-driven enter and exit animation.
- **A real engine, driven from C#.** `navius-interop.js` is a dependency-free ES module
  with 37 exports: focus trap, ref-counted scroll lock, anchored positioning with
  flip, clamp, RTL-aware alignment and auto-update (no Floating-UI), dismissable
  layers, roving focus with type-ahead, presence timing, focus-preserving teleport,
  toast interactions, and more.
- **ARIA roles, keyboard maps, and focus management, owned by the primitives.**
  Dismissal semantics work the same way: delete every class and the behaviour still
  works, and the 183-test Playwright suite plus the axe-core WCAG gate verify it on
  every push. The one deliberate exception is `NaviusDataGrid`, a headless state
  engine that renders no markup of its own and delegates all table semantics,
  including `aria-sort`, to the consumer or styled layer.
- **Zero CSS shipped.** Every primitive forwards unmatched attributes via the
  `@attributes` splat; all visible classes live in your markup. Style with Tailwind,
  plain CSS, or nothing.
- **Forms that actually submit.** Hidden native inputs mirror component state into
  real form posts; constraint validation surfaces as discrete
  `data-valid` / `data-invalid` / `data-dirty` / `data-touched` field states.
- **A native motion engine (`Navius.Motion`).** Springs are solved closed-form in C#,
  compiled to CSS `linear()` easings, and rendered on the compositor: presence
  presets for every overlay, press and hover gestures, micro interactions,
  FLIP auto-animate, in-view reveals with stagger, an imperative sequence builder,
  and experimental same-document page transitions. Standalone package, zero
  references to the primitives, zero JavaScript animation library on the wire.
- **Browser-verified.** 183 Playwright tests drive a real headless Chromium against
  the playground: focus trapping, scroll lock, positioning, dismissal, roving focus,
  exit animation, portal teleport. CI runs the full suite.
- **CSP-clean.** No `eval` anywhere; a DOM-transparent `NaviusCspProvider` carries the
  nonce.

---

## Installation

Preview packages are published to NuGet as `0.3.0-preview.3`. Install the brain with:

```bash
dotnet add package Navius.Primitives --prerelease
```

Register the services once in your app:

```csharp
builder.Services.AddNavius();
```

Motion is a separate package:

```bash
dotnet add package Navius.Motion --prerelease
```

```html
<link href="_content/Navius.Motion/navius-motion.css" rel="stylesheet" />
```

For repository development and playground integration, keep using sibling project
references. Clone with the verified remote and keep the styled-layer repo checked
out next to this one when building the playground:

```bash
git clone https://github.com/lzitser23/Navius.git navius
git clone https://github.com/lzitser23/Zits-helm.git zits-helm
```

```
<parent>/
  navius/       this repo
  zits-helm/    github.com/lzitser23/Zits-helm
```

---

## Packages

| Package | Role |
| --- | --- |
| `Navius.Primitives` | Headless Blazor primitives plus the `navius-interop.js` static web asset. |
| `Navius.Motion` | Standalone motion engine, generated CSS, spring solver, presence presets, gestures, and FLIP helpers. |
| `Zits.Ui` | Styled component reference layer in the sibling `zits-helm` repo. |
| `navius` | Dotnet tool that copies zits/ui source from the bundled registry into your app. |

Published packages include `.nupkg` and `.snupkg` artifacts with MIT license metadata,
package readmes, repository URLs, SourceLink, symbols, and package validation.

---

## Quick start

Register the services, mount a portal outlet near your app root, then compose
primitives from their parts and bring your own classes:

```razor
@using Navius.Primitives.Components.Dialog

<NaviusDialog>
    <NaviusDialogTrigger class="...">Open</NaviusDialogTrigger>
    <NaviusDialogBackdrop class="fixed inset-0 ..." />
    <NaviusDialogPopup class="fixed ...">
        <NaviusDialogTitle>Title</NaviusDialogTitle>
        <NaviusDialogDescription>...</NaviusDialogDescription>
        <NaviusDialogClose class="...">Done</NaviusDialogClose>
    </NaviusDialogPopup>
</NaviusDialog>
```

```csharp
builder.Services.AddNavius();
```

All overlays support controlled (`@bind-Open`) and uncontrolled (`DefaultOpen`) usage,
expose the discrete presence contract for styling and animation, and restore focus on
close. See the playground routes (`/`, `/wave1` to `/wave3`, `/ui`, `/fidelity`, plus
`/dates`, `/pickers`, `/sort`, `/tree`, `/tokens`, `/services`, `/extras`,
`/uncontrolled`, `/motion`, `/charts`, `/chat`, `/theme`) for every component in
motion.

---

## Stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 8 |
| UI | Blazor (WebAssembly and Server) |
| Engine | Hand-rolled dependency-free ES module, 37 exports, additive contract |
| Motion | `Navius.Motion`: C#-compiled springs, generated CSS `linear()` easings, standalone, 84 unit tests |
| Reference contract | [Base UI](https://base-ui.com), mirrored 1:1 |
| Tests | Playwright (headless Chromium) against the playground |
| Styling | None shipped. The playground demos use Tailwind (Play CDN) |

---

## Development

```bash
git clone https://github.com/lzitser23/Navius.git navius
cd navius

# build the whole graph (brain + sibling helm + showcase)
dotnet build playground/Navius.Playground/Navius.Playground.csproj

# run the playground (no launchSettings; pass the port)
dotnet run --project playground/Navius.Playground --urls http://localhost:5247

# e2e suite (auto-launches the app via Playwright's webServer)
cd tests/e2e
npm ci
npx playwright install chromium
npm test
```

After editing the engine, validate it as an ES module (plain `node --check` misses
ESM-only errors):

```bash
node --input-type=module --check < src/Navius.Primitives/wwwroot/navius-interop.js
```

Conventions, invariants, and the test-hook contract live in
[CONTEXT.md](CONTEXT.md).

### Project Structure

```text
navius/
|-- assets/        # brand marks used by the README and docs
|-- docs/          # ADRs and the Base UI parity dossier
|-- playground/    # Blazor WebAssembly showcase for the brain and sibling helm
|-- src/           # packable libraries: Navius.Primitives and Navius.Motion
`-- tests/         # xUnit motion tests and Playwright e2e tests
```

---

## Architecture

Three layers with a hard boundary, spread across four sibling repos:

| Layer | Where | Role |
| --- | --- | --- |
| **brain** | `src/Navius.Primitives` (this repo) | Headless `Navius*` primitives. Behaviour, ARIA wiring, focus management, state contracts. |
| **engine** | `src/Navius.Primitives/wwwroot/navius-interop.js` | The DOM-touching behaviour C# cannot do synchronously, driven over JS interop. |
| **helm** | [`Zits-helm`](https://github.com/lzitser23/Zits-helm) | zits/ui: styled `Zits*` components on the brain, plus the `navius` CLI and copy-paste registry. |

Companion docs/showcase sites are maintained in private sibling repositories; the
built documentation is published at [naviusui.dev](https://naviusui.dev) (component
docs and live demos) and [docs.naviusui.dev](https://docs.naviusui.dev) (agent-first
markdown manifests for the primitives).
The open-source technology surface is this repository plus
[`Zits-helm`](https://github.com/lzitser23/Zits-helm).

The why behind the load-bearing decisions is recorded in
[`docs/adr/`](docs/adr/), and the full Base UI parity dossier with per-component
status in [`docs/base-ui-parity.md`](docs/base-ui-parity.md).

```
src/Navius.Primitives/          # the brain + engine (Razor Class Library)
playground/Navius.Playground/   # Blazor WASM demo of everything (brain + helm)
tests/e2e/                      # Playwright suite driving the playground
docs/                           # ADRs + the Base UI parity dossier
assets/                         # brand mark (dot-lattice N)
```

---

## Acknowledgments

navius and zits/ui stand on the shoulders of the projects that defined this model:

- **[Base UI](https://base-ui.com)**: the headless-primitive API, anatomy, and discrete
  `data-*` and keyboard contracts the brain mirrors 1:1 for Blazor.
- **[shadcn/ui](https://ui.shadcn.com)**: the copy-paste distribution model and the
  styled-component design language that zits/ui draws on.
- **[Radix UI](https://www.radix-ui.com/primitives)**: the original headless-primitive
  model navius was first built against, before the re-target to Base UI.
- **[spartan/ui](https://github.com/spartan-ng/spartan)**: the brain/helm split for
  Angular, the architectural template here.
- **[TailwindMerge.NET](https://github.com/desmondinho/tailwind-merge-dotnet)** and
  **[TailwindVariants.NET](https://github.com/Denny09310/tailwind-variants-dotnet)**:
  the C# class-merge and variant tooling the helm builds on.

(These names are intentionally confined to this section; the rest of the codebase
refers to the behaviour contract neutrally.)

---

## License

MIT. See [LICENSE](LICENSE).
