# 3. `asChild` / Slot is an approximation, not full parity

Status: accepted

## Context

The prior-art React primitives offer an `asChild` prop (a Slot) that merges a
component's behaviour/props onto an arbitrary consumer-provided child element instead of
rendering a wrapper. This relies on React refs and cloning an opaque element.

In Blazor, a `RenderFragment` is opaque: a parent cannot introspect it, cannot read a
child's `ElementReference` before it renders, and cannot inject attributes/handlers onto
an arbitrary element inside it. There is no general equivalent of cloning a child with
merged props.

## Decision

Ship a best-effort `NaviusSlot` + `SlotMerge` that forwards props/handlers/class/style
onto a typed child the consumer opts into, and accept that arbitrary-element `asChild`
is **not** generally achievable. Every primitive still exposes the full `@attributes`
seam, which covers the common case (styling/attributes on the rendered element).

## Consequences

- This is the one honest deviation from 1:1 parity, and it is a platform limit, not a
  defect — documented as such rather than faked.
- Components that lean on `asChild` for nesting (e.g. a trigger that should *be* the
  consumer's button) instead render their own element; consumers style that element via
  the attribute seam rather than swapping it out.
