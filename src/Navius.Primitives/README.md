# Navius.Primitives

Headless Blazor primitives for accessible UI behavior. The package contains the engine layer for overlays, menus, dialogs, fields, date/time controls, sliders, sortable lists, toast state, keyboard shortcuts, and shared interop.

## Install

```bash
dotnet add package Navius.Primitives --prerelease
```

Register the services once in your app:

```csharp
builder.Services.AddNavius();
```

Static web assets are served from the package under `_content/Navius.Primitives/`.

## Related packages

- `Navius.Motion` adds the standalone motion engine and generated stylesheet.
- `Zits.Ui` is the styled component layer built on these primitives.
- The `navius` CLI copies styled source from the zits/ui registry into an application.
