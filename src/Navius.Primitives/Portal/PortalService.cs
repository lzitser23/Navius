using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Portal;

/// <summary>
/// Legacy outlet-based portal registry. A fragment registered here by id is
/// rendered by the single <see cref="NaviusPortalOutlet"/> at the outlet's own
/// location in the Blazor tree. NOTE: escape from <c>overflow:hidden</c> /
/// transformed ancestors / z-index stacking contexts is only relative to the
/// outlet's mount point — NOT a true <c>document.body</c> portal. For a real
/// body-level portal use <see cref="NaviusPortal"/>, which physically relocates
/// its node to <c>document.body</c> (or its <c>Container</c>) via the engine and
/// needs no DI registration or outlet. This service is retained only for the
/// outlet API and is no longer used by <see cref="NaviusPortal"/>. Registered via
/// <c>AddNavius()</c> (scoped).
/// </summary>
public sealed class PortalService
{
    private readonly Dictionary<string, RenderFragment> _entries = new();

    public IReadOnlyDictionary<string, RenderFragment> Entries => _entries;

    /// <summary>Raised when an entry is added/updated/removed so the outlet re-renders.</summary>
    public event Action? Changed;

    public void Set(string id, RenderFragment fragment)
    {
        _entries[id] = fragment;
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        if (_entries.Remove(id))
        {
            Changed?.Invoke();
        }
    }
}
