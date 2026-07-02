using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Avatar;

public enum AvatarLoadStatus { Idle, Loading, Loaded, Error }

public sealed class AvatarContext
{
    public AvatarLoadStatus Status { get; private set; } = AvatarLoadStatus.Idle;

    /// <summary>the spec loading-status string for the current state ('idle'|'loading'|'loaded'|'error').</summary>
    public static string ToStatusString(AvatarLoadStatus status) => status switch
    {
        AvatarLoadStatus.Loading => "loading",
        AvatarLoadStatus.Loaded => "loaded",
        AvatarLoadStatus.Error => "error",
        _ => "idle",
    };

    public event Func<Task>? Changed;

    /// <summary>Raised on every status transition so the owning Image can surface onLoadingStatusChange.</summary>
    public event Func<AvatarLoadStatus, Task>? StatusChanged;

    internal async Task SetStatusAsync(AvatarLoadStatus status)
    {
        if (Status == status) return;
        Status = status;
        if (StatusChanged is not null) await StatusChanged.Invoke(status);
        if (Changed is not null) await Changed.Invoke();
    }

    public Task RequestLoadingAsync() => SetStatusAsync(AvatarLoadStatus.Loading);
    public Task RequestLoadedAsync() => SetStatusAsync(AvatarLoadStatus.Loaded);
    public Task RequestErrorAsync() => SetStatusAsync(AvatarLoadStatus.Error);
}
