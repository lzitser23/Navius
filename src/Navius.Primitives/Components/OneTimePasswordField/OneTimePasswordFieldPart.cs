using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.OneTimePasswordField;

/// <summary>Base for one-time-password-field parts that re-render when the buffer changes.</summary>
public abstract class OneTimePasswordFieldPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected OneTimePasswordFieldContext Context { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    public virtual void Dispose()
    {
        if (_onChange is not null)
        {
            Context.Changed -= _onChange;
        }
    }
}
