using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.FileUpload;

/// <summary>Base for FileUpload parts that re-render when the file list / drag state changes.</summary>
public abstract class FileUploadPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected FileUploadContext Context { get; set; } = default!;

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
