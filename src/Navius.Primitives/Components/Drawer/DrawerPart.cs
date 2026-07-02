using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Drawer;

/// <summary>Base for drawer parts that re-render when the drawer opens or closes.</summary>
public abstract class DrawerPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected DrawerContext Context { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    public void Dispose()
    {
        if (_onChange is not null)
        {
            Context.Changed -= _onChange;
        }
    }
}
