using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Tabs;

/// <summary>Base for tab parts that re-render when the selection changes.</summary>
public abstract class TabsPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected TabsContext Context { get; set; } = default!;

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
