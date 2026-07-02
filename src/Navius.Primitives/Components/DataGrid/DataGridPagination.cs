namespace Navius.Primitives.Components.DataGrid;

/// <summary>
/// Pagination state: the zero-based page index and the page size. A record so the
/// root can compare for no-op pushes and copy with <c>with</c>. Defaults to page 0,
/// size 10 (a sensible default).
/// </summary>
public sealed record DataGridPagination(int PageIndex, int PageSize)
{
    /// <summary>Page 0, page size 10.</summary>
    public static readonly DataGridPagination Default = new(0, 10);
}
