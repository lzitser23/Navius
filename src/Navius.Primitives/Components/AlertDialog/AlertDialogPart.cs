using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.AlertDialog;

/// <summary>
/// Base for alert-dialog parts that must re-render when the dialog opens or closes
/// (e.g. the trigger, the overlay). Subscribes to the shared
/// <see cref="AlertDialogContext"/> so a state change in one part updates the others
/// without prop-drilling.
/// </summary>
public abstract class AlertDialogPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected AlertDialogContext Context { get; set; } = default!;

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
