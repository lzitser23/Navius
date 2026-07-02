using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.DataGrid;

/// <summary>
/// The dependency-free, TanStack-Table-equivalent state engine for a data grid. Owns
/// the derived row pipeline — global filter (over visible columns) → single-column
/// sort → pagination — plus row-selection and column-visibility state. The root
/// (<c>NaviusDataGrid&lt;TItem&gt;</c>) holds the controlled/uncontrolled source of
/// truth and pushes it in via the <c>Set*Internal</c> methods; mutating requests
/// (<c>Toggle*</c>, <c>Set*</c>, page moves) compute the next value and route back
/// through the root's change callbacks so it can decide controlled vs. uncontrolled.
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class DataGridContext<TItem>
{
    private readonly Func<TItem, object> _rowKey;
    private readonly Func<DataGridSort, Task> _onSortingChange;
    private readonly Func<string, Task> _onGlobalFilterChange;
    private readonly Func<DataGridPagination, Task> _onPaginationChange;
    private readonly Func<IReadOnlyCollection<object>, Task> _onRowSelectionChange;
    private readonly Func<IReadOnlyCollection<string>, Task> _onColumnVisibilityChange;

    // Source of truth, pushed by the root each render.
    private IEnumerable<TItem> _items = Array.Empty<TItem>();
    private IReadOnlyList<DataGridColumn<TItem>> _columns = Array.Empty<DataGridColumn<TItem>>();
    private DataGridSort _sorting = DataGridSort.None;
    private string _globalFilter = string.Empty;
    private DataGridPagination _pagination = DataGridPagination.Default;
    private readonly HashSet<object> _selected = new();
    private readonly HashSet<string> _hidden = new();

    // Memoised filter+sort result; pagination slices it live. Selection/pagination do
    // not invalidate it (they don't change which rows match).
    private List<TItem>? _filteredSorted;
    private bool _cacheValid;

    public DataGridContext(
        Func<TItem, object> rowKey,
        Func<DataGridSort, Task> onSortingChange,
        Func<string, Task> onGlobalFilterChange,
        Func<DataGridPagination, Task> onPaginationChange,
        Func<IReadOnlyCollection<object>, Task> onRowSelectionChange,
        Func<IReadOnlyCollection<string>, Task> onColumnVisibilityChange)
    {
        _rowKey = rowKey;
        _onSortingChange = onSortingChange;
        _onGlobalFilterChange = onGlobalFilterChange;
        _onPaginationChange = onPaginationChange;
        _onRowSelectionChange = onRowSelectionChange;
        _onColumnVisibilityChange = onColumnVisibilityChange;
    }

    /// <summary>Raised after any state change so subscribed parts re-render.</summary>
    public event Func<Task>? Changed;

    // ---- read-only state surface ----

    public IReadOnlyList<DataGridColumn<TItem>> Columns => _columns;
    public DataGridSort Sorting => _sorting;
    public string GlobalFilter => _globalFilter;
    public DataGridPagination Pagination => _pagination;
    public int PageIndex => _pagination.PageIndex;
    public int PageSize => _pagination.PageSize;

    /// <summary>The selection identity for a row (the root's <c>RowKey</c>).</summary>
    public object GetRowKey(TItem item) => _rowKey(item);

    // ---- derived rows ----

    /// <summary>All rows after global filter + sort (pre-pagination), in display order.</summary>
    public IReadOnlyList<TItem> AllFilteredRows
    {
        get { EnsureCache(); return _filteredSorted!; }
    }

    /// <summary>The rows on the current page.</summary>
    public IReadOnlyList<TItem> PageRows
    {
        get
        {
            var all = AllFilteredRows;
            if (PageSize <= 0) return all;
            var start = PageIndex * PageSize;
            if (start < 0 || start >= all.Count) return Array.Empty<TItem>();
            return all.Skip(start).Take(PageSize).ToList();
        }
    }

    /// <summary>Total rows matching the current global filter.</summary>
    public int FilteredCount => AllFilteredRows.Count;

    /// <summary>Number of pages for the filtered rows (0 when there are no rows).</summary>
    public int PageCount
    {
        get
        {
            if (PageSize <= 0) return 1;
            var count = FilteredCount;
            return count == 0 ? 0 : (int)Math.Ceiling(count / (double)PageSize);
        }
    }

    // ---- sorting ----

    /// <summary>The sort direction for a column (None unless it is the sorted column).</summary>
    public SortDirection GetSort(string columnKey)
        => _sorting.ColumnKey == columnKey ? _sorting.Direction : SortDirection.None;

    /// <summary>
    /// Cycle the single-column sort for <paramref name="columnKey"/>: a different/unsorted
    /// column → Ascending; Ascending → Descending; Descending → cleared (None). No-op for
    /// columns that are not <see cref="DataGridColumn{TItem}.Sortable"/>.
    /// </summary>
    public Task ToggleSortAsync(string columnKey)
    {
        var col = FindColumn(columnKey);
        if (col is null || !col.Sortable) return Task.CompletedTask;

        DataGridSort next;
        if (_sorting.ColumnKey != columnKey || _sorting.Direction == SortDirection.None)
            next = new DataGridSort(columnKey, SortDirection.Ascending);
        else if (_sorting.Direction == SortDirection.Ascending)
            next = new DataGridSort(columnKey, SortDirection.Descending);
        else
            next = DataGridSort.None;

        return _onSortingChange(next);
    }

    // ---- global filter ----

    /// <summary>Set the global filter text (null is treated as empty).</summary>
    public Task SetGlobalFilterAsync(string? value) => _onGlobalFilterChange(value ?? string.Empty);

    // ---- row selection ----

    public IReadOnlyCollection<object> SelectedKeys => _selected;
    public int SelectedCount => _selected.Count;

    public bool IsRowSelected(object key) => _selected.Contains(key);

    /// <summary>Toggle one row's selection by its key.</summary>
    public Task ToggleRowSelectedAsync(object key)
    {
        var next = new HashSet<object>(_selected);
        if (!next.Remove(key)) next.Add(key);
        return _onRowSelectionChange(next);
    }

    /// <summary>True when every row on the current page is selected (and the page is non-empty).</summary>
    public bool IsAllPageSelected
    {
        get
        {
            var page = PageRows;
            return page.Count > 0 && page.All(r => _selected.Contains(_rowKey(r)));
        }
    }

    /// <summary>True when some — but not all — rows on the current page are selected (indeterminate).</summary>
    public bool IsSomePageSelected
    {
        get
        {
            var page = PageRows;
            if (page.Count == 0) return false;
            var any = page.Any(r => _selected.Contains(_rowKey(r)));
            return any && !page.All(r => _selected.Contains(_rowKey(r)));
        }
    }

    /// <summary>Select all rows on the page, or clear them if all are already selected.</summary>
    public Task ToggleAllOnPageAsync()
    {
        var pageKeys = PageRows.Select(_rowKey).ToList();
        var next = new HashSet<object>(_selected);
        if (IsAllPageSelected)
        {
            foreach (var k in pageKeys) next.Remove(k);
        }
        else
        {
            foreach (var k in pageKeys) next.Add(k);
        }

        return _onRowSelectionChange(next);
    }

    // ---- pagination ----

    public bool CanPrev => PageIndex > 0;
    public bool CanNext => PageIndex + 1 < PageCount;

    public Task NextPageAsync() => CanNext ? SetPageIndexAsync(PageIndex + 1) : Task.CompletedTask;
    public Task PrevPageAsync() => CanPrev ? SetPageIndexAsync(PageIndex - 1) : Task.CompletedTask;

    public Task SetPageIndexAsync(int index)
    {
        if (index < 0) index = 0;
        if (index == PageIndex) return Task.CompletedTask;
        return _onPaginationChange(_pagination with { PageIndex = index });
    }

    // ---- column visibility ----

    public bool IsColumnVisible(string columnKey) => !_hidden.Contains(columnKey);

    /// <summary>The columns currently visible, in declared order.</summary>
    public IReadOnlyList<DataGridColumn<TItem>> VisibleColumns
        => _columns.Where(c => IsColumnVisible(c.Key)).ToList();

    /// <summary>Toggle a column's visibility. No-op for columns with <c>EnableHiding=false</c>.</summary>
    public Task ToggleColumnVisibleAsync(string columnKey)
    {
        var col = FindColumn(columnKey);
        if (col is null || !col.EnableHiding) return Task.CompletedTask;

        var next = new HashSet<string>(_hidden);
        if (!next.Remove(columnKey)) next.Add(columnKey);
        return _onColumnVisibilityChange(next);
    }

    // ---- root-driven source-of-truth setters (controlled or uncontrolled) ----

    internal async Task SetItemsAsync(IEnumerable<TItem>? items)
    {
        items ??= Array.Empty<TItem>();
        if (ReferenceEquals(items, _items)) return;
        _items = items;
        Invalidate();
        await RaiseChangedAsync();
    }

    internal async Task SetColumnsAsync(IReadOnlyList<DataGridColumn<TItem>>? columns)
    {
        columns ??= Array.Empty<DataGridColumn<TItem>>();
        if (ReferenceEquals(columns, _columns)) return;
        _columns = columns;
        Invalidate();
        await RaiseChangedAsync();
    }

    internal async Task SetSortingInternalAsync(DataGridSort sorting)
    {
        sorting ??= DataGridSort.None;
        if (_sorting.Equals(sorting)) return;
        _sorting = sorting;
        Invalidate();
        await RaiseChangedAsync();
    }

    internal async Task SetGlobalFilterInternalAsync(string value)
    {
        value ??= string.Empty;
        if (_globalFilter == value) return;
        _globalFilter = value;
        Invalidate();
        await RaiseChangedAsync();
    }

    internal async Task SetPaginationInternalAsync(DataGridPagination pagination)
    {
        pagination ??= DataGridPagination.Default;
        if (_pagination.Equals(pagination)) return;
        _pagination = pagination;
        // Pagination only slices the cached filter+sort result; no invalidation needed.
        await RaiseChangedAsync();
    }

    internal async Task SetRowSelectionInternalAsync(IEnumerable<object> keys)
    {
        var next = new HashSet<object>(keys ?? Array.Empty<object>());
        if (next.SetEquals(_selected)) return;
        _selected.Clear();
        foreach (var k in next) _selected.Add(k);
        // Selection does not change which rows match; no invalidation.
        await RaiseChangedAsync();
    }

    internal async Task SetColumnVisibilityInternalAsync(IEnumerable<string> hiddenKeys)
    {
        var next = new HashSet<string>(hiddenKeys ?? Array.Empty<string>());
        if (next.SetEquals(_hidden)) return;
        _hidden.Clear();
        foreach (var k in next) _hidden.Add(k);
        Invalidate(); // visibility changes which columns the global filter searches
        await RaiseChangedAsync();
    }

    // ---- pipeline ----

    private void Invalidate() => _cacheValid = false;

    private void EnsureCache()
    {
        if (_cacheValid && _filteredSorted is not null) return;
        _filteredSorted = ComputeFilteredSorted();
        _cacheValid = true;
    }

    private List<TItem> ComputeFilteredSorted()
    {
        IEnumerable<TItem> rows = _items;

        // 1) global filter over visible columns
        if (!string.IsNullOrEmpty(_globalFilter))
        {
            var query = _globalFilter;
            var cols = VisibleColumns;
            rows = rows.Where(item => RowMatches(item, cols, query));
        }

        // 2) single-column sort (stable; LINQ OrderBy preserves input order for ties)
        if (_sorting.ColumnKey is { } sortKey && _sorting.Direction != SortDirection.None)
        {
            var col = FindColumn(sortKey);
            if (col?.Accessor is { } accessor)
            {
                rows = _sorting.Direction == SortDirection.Ascending
                    ? rows.OrderBy(accessor, ValueComparer.Instance)
                    : rows.OrderByDescending(accessor, ValueComparer.Instance);
            }
        }

        return rows.ToList();
    }

    private static bool RowMatches(TItem item, IReadOnlyList<DataGridColumn<TItem>> cols, string query)
    {
        foreach (var col in cols)
        {
            if (col.FilterFn is not null)
            {
                if (col.FilterFn(item, query)) return true;
            }
            else if (col.Accessor is not null)
            {
                var v = col.Accessor(item);
                if (v is not null && v.ToString() is { } s &&
                    s.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private DataGridColumn<TItem>? FindColumn(string key)
    {
        foreach (var c in _columns)
        {
            if (c.Key == key) return c;
        }

        return null;
    }

    private Task RaiseChangedAsync() => Changed is null ? Task.CompletedTask : Changed.Invoke();

    /// <summary>
    /// Total-order comparer over boxed accessor values: nulls first, like-typed
    /// <see cref="IComparable"/> values by their natural order, everything else by a
    /// culture-insensitive string compare (so mixed/unsortable types never throw).
    /// </summary>
    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? a, object? b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            if (a is IComparable ca && a.GetType() == b.GetType()) return ca.CompareTo(b);
            return string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
