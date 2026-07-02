using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Popover;

/// <summary>
/// Base for popover parts that must re-render when the popover opens or closes.
/// (Mirrors <c>DialogPart</c> — once a third overlay primitive lands, these
/// should consolidate onto a shared disclosure base.)
/// </summary>
public abstract class PopoverPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected PopoverContext Context { get; set; } = default!;

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
