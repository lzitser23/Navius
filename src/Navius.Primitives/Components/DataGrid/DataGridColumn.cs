using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.DataGrid;

/// <summary>
/// Describes one column of a <see cref="DataGridContext{TItem}"/>: a stable
/// <see cref="Key"/>, the human <see cref="Header"/>, an <see cref="Accessor"/> that
/// pulls the cell value (used for sorting and the default global filter), and an
/// optional <see cref="CellTemplate"/> the helm renders for custom cells.
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class DataGridColumn<TItem>
{
    /// <summary>Stable identity for this column (used for sort, visibility, and filtering).</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable header text. The helm may fall back to <see cref="Key"/>.</summary>
    public string? Header { get; init; }

    /// <summary>
    /// Pulls the cell value for a row. Drives sort comparison and the default global
    /// filter (stringified, case-insensitive <c>Contains</c>). Null = no derivable value
    /// (e.g. a pure action/select column), so the column is skipped by sort/filter.
    /// </summary>
    public Func<TItem, object?>? Accessor { get; init; }

    /// <summary>Optional custom cell rendering. When null the helm renders the accessor value.</summary>
    public RenderFragment<TItem>? CellTemplate { get; init; }

    /// <summary>Whether the column participates in sorting. Default true.</summary>
    public bool Sortable { get; init; } = true;

    /// <summary>Whether the column can be hidden via the visibility toggle. Default true.</summary>
    public bool EnableHiding { get; init; } = true;

    /// <summary>
    /// Optional per-column global-filter predicate: <c>(row, query) =&gt; matches</c>.
    /// When set it replaces the default stringified-accessor match for this column.
    /// <c>query</c> is the raw global-filter text.
    /// </summary>
    public Func<TItem, string, bool>? FilterFn { get; init; }
}
