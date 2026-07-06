namespace Navius.Primitives.Components.Tree;

/// <summary>
/// Per-node state shared from a <c>NaviusTreeItem</c> to its Content / Trigger / Indicator /
/// Group parts. Carries the node's boxed value + aria-level and reflects the live
/// expanded / selected / disabled state back through the owning <see cref="TreeContext"/>.
/// The Group marks the node <see cref="Expandable"/> when it mounts, which is what gives a
/// parent its <c>aria-expanded</c> (leaves never get it).
/// </summary>
public sealed class TreeItemContext
{
    private readonly TreeContext _tree;
    private readonly Func<bool> _disabled;
    private readonly Action _notify;

    public TreeItemContext(TreeContext tree, object value, int level, Func<bool> disabled, Action notify)
    {
        _tree = tree;
        Value = value;
        Level = level;
        _disabled = disabled;
        _notify = notify;
    }

    /// <summary>The node's boxed identity.</summary>
    public object Value { get; }

    /// <summary>1-based aria-level (root nodes are level 1).</summary>
    public int Level { get; }

    /// <summary>Whether this node has a child group (parent nodes get aria-expanded; leaves do not).</summary>
    public bool Expandable { get; private set; }

    public bool IsExpanded => _tree.IsExpanded(Value);

    public bool IsSelected => _tree.IsSelected(Value);

    public bool IsDisabled => _tree.IsItemDisabled(_disabled());

    public bool IsActive => _tree.IsActive(Value);

    /// <summary>Called by the Group on mount/unmount so the treeitem re-renders with/without aria-expanded.</summary>
    public void SetExpandable(bool value)
    {
        if (Expandable != value)
        {
            Expandable = value;
            _notify();
        }
    }

    public Task ToggleExpandAsync() => _tree.ToggleExpandedAsync(Value);

    /// <summary>Click/Enter activation: toggle expansion (parent) then apply selection.</summary>
    public Task ActivateAsync() => _tree.ActivateAsync(Value);
}
