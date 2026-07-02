# 1. Split behaviour (brain) from styling (helm)

Status: accepted

## Context

Blazor UI options cluster at two extremes: monolithic component kits that own both
behaviour and a fixed look (hard to restyle, ship pre-built CSS), and thin "looks-like"
copies that skip real accessibility. The durable, reusable half of any UI component is
its *behaviour* — ARIA roles, keyboard model, focus management, controlled/uncontrolled
state. The *styling* is what every team wants to own.

## Decision

Two layers with a hard boundary:

- **brain** (`Navius.Primitives`): headless primitives. No visible classes. Each owns
  accessibility + a `data-*` state contract and forwards all unmatched attributes.
- **helm** (`Zits.Ui`): styled components that wrap the brain with Tailwind + a token
  theme, distributed as copy-paste source the consumer owns.

The brain is versioned and depended-on; the helm is copied in and edited freely.

## Consequences

- Restyling never touches behaviour; accessibility is fixed once, centrally.
- The brain must be genuinely complete (every part/prop/keyboard/ARIA/`data-*`), since
  the helm can only style what the brain exposes — this drove the parity audit.
- Two seams to learn (`@attributes` splat in the brain; `Cn.Merge`/`Cn.Class` in the
  helm). Documented in CONTEXT.md.
