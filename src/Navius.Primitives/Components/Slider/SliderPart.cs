using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Slider;

/// <summary>
/// Base for slider parts (track, range, thumb) that must re-render when the
/// slider's value changes. Subscribes to <see cref="SliderContext.Changed"/> and
/// marshals a re-render onto the renderer's sync context.
/// </summary>
public abstract class SliderPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected SliderContext Context { get; set; } = default!;

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
