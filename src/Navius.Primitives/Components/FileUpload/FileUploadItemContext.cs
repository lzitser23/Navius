using Microsoft.AspNetCore.Components.Forms;

namespace Navius.Primitives.Components.FileUpload;

/// <summary>Cascaded from a NaviusFileUploadItem to its name/size/delete children.</summary>
public sealed class FileUploadItemContext
{
    private readonly Func<Task> _remove;

    public FileUploadItemContext(IBrowserFile file, bool invalid, Func<Task> remove)
    {
        File = file;
        Invalid = invalid;
        _remove = remove;
    }

    public IBrowserFile File { get; }

    public bool Invalid { get; }

    public Task RemoveAsync() => _remove();
}
