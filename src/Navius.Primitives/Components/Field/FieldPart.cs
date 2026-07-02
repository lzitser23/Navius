using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Field;

/// <summary>
/// Base for field parts (label, control, description, error, item) that must
/// re-render when the field's validity, interaction state, or active-message set
/// changes. Subscribes to <see cref="FieldContext.Changed"/> on init and detaches
/// on dispose. Exposes the cascaded <see cref="Field"/> and a splat-ready map of
/// the discrete Base UI field-state attributes via <see cref="StateAttributes"/>.
/// </summary>
public abstract class FieldPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected FieldContext Field { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Field.Changed += _onChange;
    }

    /// <summary>The discrete Base UI field-state attributes for the cascaded field.</summary>
    protected IReadOnlyDictionary<string, object> StateAttributes => Field.StateAttributes;

    public virtual void Dispose()
    {
        if (_onChange is not null)
        {
            Field.Changed -= _onChange;
        }
    }
}
