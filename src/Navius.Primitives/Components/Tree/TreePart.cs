using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Tree;

/// <summary>Base for tree parts that must re-render when selection / expansion / active focus changes.</summary>
public abstract class TreePart : ComponentBase, IDisposable
{
    [CascadingParameter] protected TreeContext Tree { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Tree.Changed += _onChange;
    }

    public virtual void Dispose()
    {
        if (_onChange is not null)
        {
            Tree.Changed -= _onChange;
        }
    }
}
