# Navius

> A **Base UI**-style component framework for **Blazor** — built the right way: on a real headless, accessible primitive **engine**, distributed as code you own.

`Navius` is an early-stage, open-source project. The bet (from a full feasibility study of the Blazor UI landscape, mid-2026): the styled-component _look_ for Blazor is already commoditized — but nobody has shipped the thing that makes it actually work, a **Base UI-equivalent headless primitive layer** ([base-ui.com](https://base-ui.com)), paired with the **brain/helm** split that keeps behaviour maintainable while you own the styling.

> Named after **Attus Navius** — the Roman augur who sliced a whetstone with a razor before King Tarquin. (Razor → .NET **Razor**.)

## Architecture

| Layer | What it is | Status |
|---|---|---|
| **Brain** (`Navius.Primitives`) | Headless, accessible primitives — ARIA, keyboard, focus, state. The hard, stable half. Mirrors the Base UI contract. | ✅ re-targeted to Base UI |
| **Engine** (`navius-interop.js` + C# wrappers) | The part C# can't do: focus-trap, scroll-lock, anchored positioning, dismissable layers, roving focus, presence/animation timing, teleport. Driven from C# over JS interop. | ✅ working |
| **Helm** (your `.razor` + Tailwind) | The styled layer you copy in and own. | ✅ via `navius` CLI |

### Why an "engine" at all

Blazor can't synchronously touch the DOM (WASM has no DOM; Server marshals interop async over SignalR). So a correct focus-trap, scroll-lock, roving-tabindex, or popover-positioning **cannot be pure C#** — the behaviour must live in JavaScript and be driven from C#. That JS engine is the layer every other Blazor styled-component kit either skips (so it isn't really accessible) or hides. Navius builds it in the open.

The engine (`navius-interop.js`) exports ~30 primitives; the core set: `createFocusTrap`, `lockScroll`/`unlockScroll`, `createPositioner` (anchored placement + flip + clamp + auto-update, **no Floating-UI dependency**), `createDismissableLayer` (Esc + outside pointer-down), `createRovingFocus` (arrow/Home/End/type-ahead), and the Base UI presence/timing primitives (`nextFrame`, `waitForAnimations`) that drive `data-starting-style`/`data-ending-style` enter/exit animation — plus teleport (`teleportToBody`), toast timing/swipe (`createToastInteractions`/`createToastHotkey`), and the drag/scroll/carousel/long-press/constraint-validation helpers backing the wider component surface.

## Components

| Component | Engine pieces used | Notes |
|---|---|---|
| **Dialog** | focus-trap, scroll-lock | modal; Esc + backdrop dismiss, focus restore, full ARIA |
| **Popover** | positioner, dismissable | non-modal, anchored, flips on collision |
| **Tooltip** | positioner | hover-delay + focus-immediate, `aria-describedby` |
| **Menu** | positioner, dismissable, roving-focus | arrows / Home / End / type-ahead, disabled-item skipping |
| **Switch / Checkbox** | — (pure C#) | `role=switch/checkbox`, controlled + uncontrolled, discrete `data-checked`/`data-unchecked` |
| **Combobox** | positioner, dismissable | filter + `aria-activedescendant` virtual focus, Enter/click select |
| **Select** | positioner, dismissable, roving-focus | `role=listbox/option`, lands on the selected option on open |
| **Tabs** | — (pure C#) | `role=tablist/tab/tabpanel`, roving tabindex, arrow/Home/End |
| **Accordion** | — (pure C#) | single/multiple, `role=region`, `aria-expanded` |
| **Portal** | — | teleport content to a top-level outlet, escaping `overflow:hidden` / transforms |
| **Toast** | service + region | imperative `Toast.Show(...)`, auto-dismiss timer, `aria-live` |

**Base UI surface** — beyond the core list above, Navius mirrors the rest of the Base UI component surface: Separator, Label, AspectRatio, VisuallyHidden, Progress, Meter, Avatar, Toggle, ToggleGroup, Radio, Collapsible, PreviewCard, AlertDialog, Drawer, ContextMenu, Menubar, Toolbar, AccessibleIcon, DirectionProvider, CspProvider, PasswordToggleField, OneTimePasswordField, Button, Field/Fieldset/Form, Input, NumberField, CheckboxGroup, **Autocomplete, Slider, ScrollArea, NavigationMenu, Slot**. The one honest deviation: `Slot`/`asChild` is an approximation — Razor render-fragments are opaque, so props are forwarded onto the consumer's own child rather than merged onto an arbitrary one.

All overlays expose Base UI's **discrete presence contract** — `data-open`/`data-closed` (+ `data-side`/`data-align`) with `data-starting-style`/`data-ending-style` enter/exit transitions — support controlled (`@bind-Open`) and uncontrolled (`DefaultOpen`) usage, and forward all unmatched attributes via `@attributes` — so every visible class lives in **your** markup.

```razor
<NaviusDialog>
    <NaviusDialogTrigger class="...">Open</NaviusDialogTrigger>
    <NaviusDialogBackdrop class="fixed inset-0 ..." />
    <NaviusDialogPopup class="fixed ...">
        <NaviusDialogTitle>Title</NaviusDialogTitle>
        <NaviusDialogDescription>…</NaviusDialogDescription>
        <NaviusDialogClose class="...">Done</NaviusDialogClose>
    </NaviusDialogPopup>
</NaviusDialog>
```

## Run the playground

> Requires the .NET 8 SDK.

```bash
dotnet run --project playground/Navius.Playground
# open http://localhost:5247
```

Dialog, Popover, Tooltip, Menu, Switch, Checkbox, and a Portal demo — all on one page.

## The `navius` CLI (copy-paste distribution)

Components are distributed as **code you own**, not a NuGet dependency. The CLI copies a component's source (plus its `registryDependencies`) into your project. The CLI and its registry live in the sibling **`zits-helm`** repo (they distribute the styled layer — ADR-0008); run them from that repo's root:

```bash
dotnet run --project tools/Navius.Cli -- list
dotnet run --project tools/Navius.Cli -- add dialog --to ./src/MyApp --namespace MyApp.Ui
# → resolves `core`, copies the parts, rewrites the namespace. Now they're yours.
```

The registry is plain JSON (`registry/registry.json` there), modelled on the `registry-item` schema, so anyone can host their own.

## Tests

End-to-end tests drive a real headless browser (Playwright) against the playground and assert the behaviour that can't be unit-tested — focus trapping, scroll lock, anchored positioning, dismissal, roving focus, presence/exit animation, and that the Portal genuinely teleports out of a clipped container. **74 tests, all green.**

```bash
cd tests/e2e
npm install
npx playwright install chromium
npm test            # headless Chromium
```

Playwright's `webServer` auto-launches the Blazor app. If `dotnet` isn't on your PATH, set `DOTNET_EXE` to its full path.

## Project layout

```
src/Navius.Primitives/         the brain + engine (Razor Class Library)
playground/Navius.Playground/  Blazor WASM demo of everything (brain + helm)
tests/e2e/                     Playwright suite driving the playground
```

Sibling repos (checked out side by side — see `docs/adr/0008-helm-repo-split.md`):

```
../zits-helm/     the styled helm (zits/ui) + the `navius` CLI + the registry
../navius-docs/   brain docs site
../zits-ui/       helm docs site
```

## zits/ui — the styled helm

The styled layer lives in the sibling **`zits-helm`** repo (`src/Zits.Ui`): Tailwind
components on the Navius brain with an OKLCH token theme, distributed copy-paste via the
`navius` CLI. See it at the `/ui` and `/fidelity` routes in the playground. Components like
`ZitsButton`, `ZitsDialog`, `ZitsAccordion`, `ZitsTabs`, and `ZitsSwitch` wrap the headless
primitives, so behaviour + accessibility come from the brain and every class is yours.

## Reference & status

Navius mirrors **[Base UI](https://base-ui.com)** as its 1:1 reference contract — discrete
boolean `data-*` attributes, the `data-starting-style`/`data-ending-style` animation model,
and the `Portal → Positioner → Popup` overlay anatomy. The re-target is tracked in
`docs/adr/0007-retarget-reference-radix-to-base-ui.md` and `docs/base-ui-parity.md`: the
re-target is **complete end-to-end** — contract foundation, leaf primitives, forms, the
overlay family, the menu family, and the compound rewrites (Autocomplete, Combobox, Toast
manager, NavigationMenu) — with a small, deliberately-deferred backlog tracked in the
dossier's §5 STATUS markers.

## Acknowledgements

Navius and zits/ui stand on the shoulders of the projects that defined this model:

- **[Base UI](https://base-ui.com)** — the headless-primitive API, anatomy, and discrete
  `data-*`/keyboard contracts the Navius brain mirrors 1:1 for Blazor.
- **[shadcn/ui](https://ui.shadcn.com)** — the copy-paste distribution model and the
  styled-component design language that zits/ui draws on.
- **[Radix UI](https://www.radix-ui.com/primitives)** — the original headless-primitive model
  Navius was first built against, before the re-target to Base UI.
- **[spartan/ui](https://github.com/spartan-ng/spartan)** — the brain/helm split for Angular,
  the architectural template here.
- **[TailwindMerge.NET](https://github.com/desmondinho/tailwind-merge-dotnet)** and
  **[TailwindVariants.NET](https://github.com/Denny09310/tailwind-variants-dotnet)** — the C#
  class-merge / variant tooling the helm builds on.

(These names are intentionally confined to this section — the rest of the codebase refers to
the behaviour contract neutrally.)

## License

MIT — see [LICENSE](LICENSE).
