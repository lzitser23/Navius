namespace Navius.Primitives.Components.Field;

/// <summary>
/// The accessibility wiring a <see cref="NaviusFieldControl"/> computes for a
/// custom control: <c>id</c>, <c>aria-describedby</c> (active message ids) and
/// <c>aria-invalid</c>. When the control wraps custom <c>ChildContent</c>, this is
/// cascaded so the consumer can splat it onto their own element via
/// <c>@attributes</c>. <see cref="Attributes"/> is a ready-to-splat dictionary.
/// </summary>
public sealed class ControlProps
{
    public required string Id { get; init; }
    public string? DescribedBy { get; init; }
    public bool Invalid { get; init; }

    /// <summary>A splat-ready attribute map: <c>id</c>, plus <c>aria-describedby</c>/<c>aria-invalid</c> when applicable.</summary>
    public IReadOnlyDictionary<string, object> Attributes
    {
        get
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["id"] = Id,
            };

            if (DescribedBy is not null)
            {
                map["aria-describedby"] = DescribedBy;
            }

            if (Invalid)
            {
                map["aria-invalid"] = "true";
            }

            return map;
        }
    }
}
