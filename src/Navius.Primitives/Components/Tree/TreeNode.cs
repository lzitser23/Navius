namespace Navius.Primitives.Components.Tree;

/// <summary>
/// A node in the data-driven <see cref="NaviusTree{TValue}"/> render mode. Pass a
/// hierarchical <c>IEnumerable&lt;TreeNode&lt;TValue&gt;&gt;</c> as <c>Items</c> plus an
/// <c>ItemTemplate</c> and the tree walks it, wiring <c>aria-level</c>/<c>aria-setsize</c>/
/// <c>aria-posinset</c> and the group/treeitem roles automatically. Manual composition with
/// the part components (NaviusTreeItem / NaviusTreeGroup / ...) stays available for custom shapes.
/// </summary>
/// <typeparam name="TValue">The node identity type (must be unique across the tree).</typeparam>
public sealed class TreeNode<TValue>
{
    /// <summary>The node's unique identity (used for selection + expansion state).</summary>
    public TValue Value { get; set; } = default!;

    /// <summary>The node's display / type-ahead label.</summary>
    public string Label { get; set; } = "";

    /// <summary>Child nodes (null or empty = a leaf, which never gets <c>aria-expanded</c>).</summary>
    public IReadOnlyList<TreeNode<TValue>>? Children { get; set; }

    /// <summary>When true the node cannot be selected and is skipped by keyboard navigation.</summary>
    public bool Disabled { get; set; }

    /// <summary>Whether this node has at least one child (drives <c>aria-expanded</c> presence).</summary>
    public bool HasChildren => Children is { Count: > 0 };
}
