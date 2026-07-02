namespace Navius.Primitives.Components.DataGrid;

/// <summary>
/// The grid's single-column sort state: which column key is sorted and in which
/// direction. Value-equality (record) lets the root detect no-op pushes. A
/// <see cref="ColumnKey"/> of null (or <see cref="SortDirection.None"/>) means unsorted.
/// </summary>
public sealed record DataGridSort(string? ColumnKey, SortDirection Direction)
{
    /// <summary>The unsorted state.</summary>
    public static readonly DataGridSort None = new(null, SortDirection.None);
}
