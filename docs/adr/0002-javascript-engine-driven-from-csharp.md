# 2. A hand-rolled JS engine, driven from C#

Status: accepted

## Context

Correct UI primitives need synchronous DOM work: trap focus, lock scroll, measure and
anchor a floating element, move a roving tabindex, teleport a node to `document.body`,
track a pointer drag. Blazor cannot do this synchronously — WASM has no DOM, and Server
marshals interop asynchronously over SignalR. Pure-C# attempts are either wrong or
race-prone.

## Decision

Put the DOM-touching behaviour in one JavaScript module (`navius-interop.js`) driven
from C# wrappers (`NaviusJsInterop`). Hand-roll the anchored-positioning logic
(flip/clamp/sticky/arrow + CSS vars) rather than depend on Floating-UI, to keep the
engine dependency-free and small.

Contract: the engine is **additive and backward-compatible**. Existing exports keep
their names and signatures; new capability is an optional option or a new function, so
the published behaviour (and the E2E suite) never regresses.

## Consequences

- One file concentrates the hard, platform-specific code; primitives stay declarative.
- Engine edits are high-blast-radius. They must be validated as an ES module
  (`node --input-type=module --check`) — loose-mode `node --check` misses ESM-only
  errors that break `import()` and silently disable all interop (this bit us once).
- No Floating-UI means we own the positioning maths (and its edge cases), but also no
  external version churn or bundle cost.
