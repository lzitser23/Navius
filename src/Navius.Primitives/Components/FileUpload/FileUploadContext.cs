using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Navius.Primitives.Components.FileUpload;

/// <summary>
/// Shared state for a FileUpload. The root owns the file list, limits and reject
/// reasons and pushes them here; parts read the list + drag/invalid state and the
/// polite status message. Actions (handle selection, remove, clear, open the OS
/// dialog) funnel back to root-supplied callbacks. The real hidden
/// <c>&lt;input type="file"&gt;</c> registers its element here so the root can wire
/// the engine's drag/drop relay.
/// </summary>
public sealed class FileUploadContext
{
    private readonly Func<InputFileChangeEventArgs, Task> _handleFiles;
    private readonly Func<IBrowserFile, Task> _removeFile;
    private readonly Func<Task> _clear;
    private readonly Func<Task> _open;

    public FileUploadContext(
        Func<InputFileChangeEventArgs, Task> handleFiles,
        Func<IBrowserFile, Task> removeFile,
        Func<Task> clear,
        Func<Task> open)
    {
        _handleFiles = handleFiles;
        _removeFile = removeFile;
        _clear = clear;
        _open = open;
    }

    public IReadOnlyList<IBrowserFile> Files { get; private set; } = Array.Empty<IBrowserFile>();

    public bool IsDragging { get; private set; }

    /// <summary>True after the most recent selection produced at least one rejection.</summary>
    public bool Invalid { get; private set; }

    public bool Disabled { get; private set; }

    public string? Accept { get; private set; }

    public bool Multiple { get; private set; }

    public bool Directory { get; private set; }

    public string? Capture { get; private set; }

    public string? Name { get; private set; }

    /// <summary>Polite live-region text announcing added/rejected/removed counts.</summary>
    public string StatusMessage { get; private set; } = string.Empty;

    /// <summary>The hidden native input element (for the engine's drop relay + dialog open).</summary>
    public ElementReference? InputElement { get; private set; }

    public event Func<Task>? Changed;

    /// <summary>Raised when the input element registers, so the root can (re)wire the engine.</summary>
    public event Action? ElementsChanged;

    public void Configure(bool disabled, string? accept, bool multiple, bool directory, string? capture, string? name)
    {
        Disabled = disabled;
        Accept = accept;
        Multiple = multiple;
        Directory = directory;
        Capture = capture;
        Name = name;
    }

    /// <summary>Root pushes the authoritative file list + validity + status here.</summary>
    internal void SetState(IReadOnlyList<IBrowserFile> files, bool invalid, string status)
    {
        Files = files;
        Invalid = invalid;
        StatusMessage = status;
    }

    public async Task NotifyChangedAsync()
    {
        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public void SetInputElement(ElementReference element)
    {
        InputElement = element;
        ElementsChanged?.Invoke();
    }

    public async Task SetDraggingAsync(bool dragging)
    {
        if (IsDragging == dragging)
        {
            return;
        }

        IsDragging = dragging;
        await NotifyChangedAsync();
    }

    public Task HandleFilesAsync(InputFileChangeEventArgs e) => _handleFiles(e);

    public Task RemoveFileAsync(IBrowserFile file) => _removeFile(file);

    public Task ClearAsync() => _clear();

    /// <summary>Open the OS file dialog (via the engine's clickToOpen on the hidden input).</summary>
    public Task OpenAsync() => _open();

    /// <summary>Human-readable byte size, e.g. "1.4 KB".</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        var rounded = unit == 0 ? size.ToString("0") : size.ToString("0.#");
        return $"{rounded} {units[unit]}";
    }
}
