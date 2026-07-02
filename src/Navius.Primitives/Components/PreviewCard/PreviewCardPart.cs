using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.PreviewCard;

/// <summary>Base for preview card parts that re-render when the card opens/closes.</summary>
public abstract class PreviewCardPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected PreviewCardContext Context { get; set; } = default!;

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
