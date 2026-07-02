using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.PasswordToggleField;

/// <summary>
/// Base for password toggle field parts that must re-render when the revealed
/// state changes (input switches type, toggle switches label/pressed state).
/// </summary>
public abstract class PasswordToggleFieldPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected PasswordToggleFieldContext Context { get; set; } = default!;

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
