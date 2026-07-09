namespace Navius.Primitives.Components.Field;

/// <summary>
/// The accessibility wiring a <see cref="NaviusFieldControl"/> computes for a
/// custom control: <c>id</c>, <c>aria-describedby</c> (active message ids),
/// <c>aria-invalid</c> and the discrete field-state <c>data-*</c> attributes
/// (data-valid/data-invalid/data-dirty/data-touched and the rest of the field's
/// state set). When the control wraps custom <c>ChildContent</c>, this is cascaded so
/// the consumer can splat it onto their own element via <c>@attributes</c>.
/// <see cref="Attributes"/> is a ready-to-splat dictionary.
/// </summary>
public sealed class ControlProps
{
    public required string Id { get; init; }
    public string? DescribedBy { get; init; }
    public bool Invalid { get; init; }

    /// <summary>The field's discrete <c>data-*</c> state attributes (see <see cref="FieldContext.StateAttributes"/>).</summary>
    public IReadOnlyDictionary<string, object>? State { get; init; }

    /// <summary>A splat-ready attribute map: <c>id</c>, <c>aria-describedby</c>/<c>aria-invalid</c> when applicable, plus the discrete <c>data-*</c> state.</summary>
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

            if (State is not null)
            {
                foreach (var kv in State)
                {
                    map[kv.Key] = kv.Value;
                }
            }

            return map;
        }
    }
}
