# 6. `zits/ui` is the helm; developed in-repo, distributed as source

Status: accepted; superseded in part by ADR-0008 — the helm now lives in its own
sibling repo (`zits-helm`), the "later packaging step" this ADR anticipated. The
naming, source-distribution, and showcase decisions below still stand.

## Context

The styled layer needs a name and a home. Options: keep it inside the brain repo as a
folder, develop it in a separate repo from day one, or treat it purely as registry
source with no compiled project.

A separate repo from day one is awkward while the brain is unpublished — the helm needs
a project reference to the brain to compile and be showcased, and a cross-repo reference
is friction. A purely-source helm with no project can't be type-checked or demoed.

## Decision

- Name the helm **`zits/ui`** (project `Zits.Ui`, components `Zits*`).
- Develop it **in this repo** as a Razor Class Library under `helm/Zits.Ui` that
  project-references the brain, so it compiles, is showcased at the `/ui` route, and is
  browser-tested alongside the brain.
- It is **distributed as source** (per ADR-0004), not as the compiled RCL — the RCL is
  the reference build + showcase. The same component files are what the registry copies.
- Extracting `zits/ui` into its own public repo is a later packaging step; nothing in the
  design blocks it.

## Consequences

- One repo to build/test the whole stack during development; the brain and helm evolve
  together and can't silently drift.
- The brand name lives in the project/namespace; prior-art attribution stays in the
  README, not scattered through component code.
- When the brain is published, the helm repo split becomes a mechanical move.
