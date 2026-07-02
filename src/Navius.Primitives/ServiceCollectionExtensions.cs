using Navius.Primitives.Components.Toast;
using Navius.Primitives.Portal;
using Microsoft.Extensions.DependencyInjection;

namespace Navius.Primitives;

/// <summary>DI registration for Navius services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Navius's services (the portal registry and the toast manager). Call
    /// once at startup, and mount a single <c>&lt;NaviusPortalOutlet /&gt;</c> and/or a
    /// <c>&lt;NaviusToastProvider&gt;</c> + <c>&lt;NaviusToastViewport /&gt;</c> near the app root.
    /// </summary>
    public static IServiceCollection AddNavius(this IServiceCollection services)
    {
        services.AddScoped<PortalService>();
        services.AddScoped<ToastManager>();
        return services;
    }
}
