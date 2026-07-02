namespace Navius.Primitives.Components.DataGrid;

/// <summary>
/// The sort state of a single column. The data grid sorts by at most one column;
/// <see cref="None"/> means the column is unsorted.
/// </summary>
public enum SortDirection
{
    None,
    Ascending,
    Descending,
}
