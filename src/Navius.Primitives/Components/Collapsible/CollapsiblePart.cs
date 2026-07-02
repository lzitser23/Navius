using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Collapsible;

public abstract class CollapsiblePart : ComponentBase, IDisposable
{
    [CascadingParameter] protected CollapsibleContext Context { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    public void Dispose()
    {
        if (_onChange is not null) Context.Changed -= _onChange;
    }
}
