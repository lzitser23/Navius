using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Avatar;

public abstract class AvatarPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected AvatarContext Context { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    public void Dispose()
    {
        DisposeCore();
        if (_onChange is not null) Context.Changed -= _onChange;
        GC.SuppressFinalize(this);
    }

    protected virtual void DisposeCore() { }
}
