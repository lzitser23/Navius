# 8. Split the helm into its own repo (`zits/helm`), consumed by sibling path reference

Status: accepted (2026-07-02)

## Context

ADR-0006 developed the helm in-repo, noting that "extracting `zits/ui` into its own
public repo is a later packaging step" and that the split "becomes a mechanical move."
With the Base UI re-target complete and verified end-to-end (ADR-0007 + the parity
dossier), the split is due: brain and helm are OSS projects that should take
contributions independently. Executing it required six interdependent decisions
(dependency model, consumer homes, history, naming, hosting, packaging).

## Decision

1. **New repo:** `zits-helm` (branded **`zits/helm`**), a sibling of this repo. The
   name avoids colliding with the existing `zits-ui` *docs* repo. Layout:
   `src/Zits.Ui` (the RCL), `tools/Navius.Cli`, `registry/`.
2. **Dependency model: sibling path reference.** `Zits.Ui` project-references
   `..\..\..\navius\src\Navius.Primitives` — the same convention the two docs repos
   already use — until the brain ships on NuGet. A published-package model stays the
   eventual OSS story; it is gated on the publish go-ahead.
3. **Consumer homes.** The **CLI + registry move with the helm** (they distribute it,
   per ADR-0004; the helm `.razor` files are the registry source). The **playground +
   Playwright e2e suite stay here**: they are the ecosystem's single showcase/test
   surface (brain routes `/`,`/wave1`–`/wave3`; helm routes `/ui`,`/fidelity`), and the
   playground's helm reference repoints to the sibling repo.
4. **History:** every public repo starts from a clean single-commit initial state at
   publication (2026-07-02). The full pre-publication development history — including
   the helm's pre-split archaeology — is archived locally by the maintainer as git
   bundles.
5. **Hosting:** the open-source technology repos publish to GitHub under `lzitser23`
   as `Navius` and `Zits-helm`. The `navius-docs` and `zits-ui` docs/showcase repos
   remain private sibling repositories. The CI workflows in both code repos encode the
   sibling checkout.
6. **Packaging metadata.** Initially deferred with the NuGet decision. On 2026-07-05,
   the preview package surface was locked as explicit `0.3.0-preview.1` metadata for
   `Navius.Primitives`, `Navius.Motion`, `Zits.Ui`, and the `navius` dotnet tool.

## Consequences

- The ecosystem is now **four sibling repos** when working on the full stack:
  `Navius` (brain + engine + playground + e2e), `Zits-helm` (helm + CLI + registry),
  `navius-docs` (private brain docs), and `zits-ui` (private helm docs, repointed to
  `Zits-helm`).
- The registry's source items still use repo-relative paths during development, but the
  packaged `navius` tool now bundles `registry/` plus a `registry-source/` payload so
  `navius list` and `navius add <item>` work after `dotnet tool install`.
- A brain-only checkout still builds `src/Navius.Primitives` standalone; building the
  playground (and running e2e) requires the sibling helm checkout.
- The helm can no longer silently drift against the brain via a shared build — the
  playground + e2e suite here remain the integration gate.
