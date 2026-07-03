using Microsoft.AspNetCore.Components.Forms;

namespace Navius.Primitives.Components.FileUpload;

/// <summary>Why a selected/dropped file was refused before reaching the file list.</summary>
public enum FileRejectionReason
{
    /// <summary>The file exceeded <c>MaxSize</c>.</summary>
    TooLarge,

    /// <summary>Adding the file would exceed <c>MaxFiles</c>.</summary>
    TooMany,

    /// <summary>The file did not match the <c>Accept</c> filter.</summary>
    WrongType,
}

/// <summary>A single rejected file and the reason it was refused.</summary>
public sealed record NaviusFileRejection(IBrowserFile File, FileRejectionReason Reason);
