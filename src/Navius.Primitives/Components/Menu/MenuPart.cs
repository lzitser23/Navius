using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Menu;

/// <summary>Base for menu parts that re-render when the menu opens/closes.</summary>
public abstract class MenuPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected MenuContext Context { get; set; } = default!;

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
