using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ColorPicker;

/// <summary>Base for ColorPicker parts that re-render when the color changes.</summary>
public abstract class ColorPickerPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected ColorPickerContext Context { get; set; } = default!;

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

        OnDispose();
        GC.SuppressFinalize(this);
    }

    protected virtual void OnDispose()
    {
    }
}
