using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.ColorPicker;

/// <summary>
/// Base for the pointer-draggable ColorPicker tracks (the 2D area and the hue /
/// alpha sliders). Wires the engine's 2D pointer tracker to the track element and
/// funnels its normalized {x,y} fractions to <see cref="HandleFractionAsync"/>,
/// which each track maps to its own axis. Keyboard lives in the derived razor
/// (each track's thumb is a role="slider" it hosts directly). Re-renders on color
/// change so thumbs and gradients stay in sync.
/// </summary>
public abstract class ColorPickerTrackPart : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] protected ColorPickerContext Context { get; set; } = default!;

    /// <summary>The draggable track surface; the derived razor binds this via <c>@ref</c>.</summary>
    protected ElementReference TrackElement;

    /// <summary>True while a pointer drag is in progress (drives the thumb's data-dragging).</summary>
    protected bool Dragging;

    private NaviusJsInterop? _interop;
    private PointerTracker2D? _tracker;
    private DotNetObjectReference<ColorPickerTrackPart>? _selfRef;
    private Func<Task>? _onChange;
    private bool _wired;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _wired || Context.Disabled)
        {
            return;
        }

        _wired = true;

        try
        {
            _interop ??= new NaviusJsInterop(JS);
            _selfRef ??= DotNetObjectReference.Create(this);
            _tracker = await _interop.CreatePointerTracker2DAsync(TrackElement, _selfRef);
        }
        catch (JSException)
        {
            // Engine export unavailable; the keyboard path stays fully functional.
        }
    }

    [JSInvokable]
    public async Task OnFraction2D(PointerFraction2D f)
    {
        if (Context.Disabled || Context.ReadOnly)
        {
            return;
        }

        Dragging = true;
        await HandleFractionAsync(f.X, f.Y);
    }

    [JSInvokable]
    public async Task OnCommit2D(PointerFraction2D f)
    {
        if (Context.Disabled || Context.ReadOnly)
        {
            return;
        }

        await HandleFractionAsync(f.X, f.Y);
        Dragging = false;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Map the normalized pointer fraction to this track's channel(s).</summary>
    protected abstract Task HandleFractionAsync(double x, double y);

    public async ValueTask DisposeAsync()
    {
        if (_onChange is not null)
        {
            Context.Changed -= _onChange;
        }

        if (_tracker is not null)
        {
            await _tracker.DisposeAsync();
        }

        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }

        _selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
