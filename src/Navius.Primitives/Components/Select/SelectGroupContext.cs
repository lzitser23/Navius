namespace Navius.Primitives.Components.Select;

/// <summary>
/// Cascaded by <c>NaviusSelectGroup</c> so a child <c>NaviusSelectLabel</c> can
/// adopt the group's generated id, which the group references via aria-labelledby
/// (the spec's group/label association).
/// </summary>
public sealed class SelectGroupContext
{
    public string LabelId { get; } = $"navius-select-label-{Guid.NewGuid():N}";
}
