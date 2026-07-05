# 4. Distribute the helm as copy-paste source, not a dependency

Status: accepted

## Context

The styled layer is the part teams want to own and tweak. Shipping it as a compiled
NuGet dependency makes overrides awkward and re-imposes a fixed look. The brain, by
contrast, is behaviour that should stay versioned and shared.

## Decision

- The **brain** ships as a packable Razor Class Library (a normal dependency).
- The **helm** ships as **source** through a JSON registry (`registry/registry.json`,
  modelled on a `registry-item` schema) and the `navius` CLI (`tools/Navius.Cli`):
  `navius add <component> --to <dir> --namespace <ns>` copies a component's source plus
  its `registryDependencies` into the consumer's project and rewrites the namespace.
- `Cn` (the `cn()` class-merge helper) is a registry lib entry.

The `Zits.Ui` RCL is the reference build + showcase target; the same component source is
what the registry distributes.

## Consequences

- Consumers own and edit helm components; updates are a re-copy, not a version bump.
- Two distribution mechanisms to maintain (NuGet for the brain, registry/CLI for the
  helm), but each matches what that layer needs.
- Anyone can host their own registry — the schema is plain JSON.
- The published `navius` dotnet tool must be self-contained. It bundles the default
  registry and source payload, while `--root` and `--registry` remain the escape hatch
  for local development and custom registries.
