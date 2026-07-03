namespace Navius.Motion;

/// <summary>
/// One micro-interaction preset defined as data: a named keyframe animation plus its
/// timing, whether it loops, and how it behaves under reduced motion. This single
/// definition feeds both the CSS generator (<see cref="MotionStylesheet"/>, which emits
/// a <c>@keyframes</c> block plus a <c>.motion-*</c> class) and the WAAPI runtime
/// (<see cref="MotionPrograms.Micro"/>), which is what keeps the zero-JS tier and the
/// programmatic tier in agreement.
/// </summary>
public sealed record MicroPreset(
    string Name,
    IReadOnlyList<MicroFrame> Keyframes,
    double DurationMs,
    string Easing,
    bool Loop,
    MicroReduce Reduce,
    IReadOnlyList<MicroDecl>? BaseStyle = null)
{
    /// <summary>The CSS class the generated stylesheet emits for this preset.</summary>
    public string Class => "motion-" + Name;

    /// <summary>The generated <c>@keyframes</c> name.</summary>
    public string KeyframesName => "navius-" + Name;

    /// <summary>The opacity-only <c>@keyframes</c> name used under reduced motion (see <see cref="MicroReduce.OpacityOnly"/>).</summary>
    public string ReducedKeyframesName => "navius-" + Name + "-reduced";
}

/// <summary>
/// One micro-animation keyframe restricted to the properties the presets animate
/// (serialized to camelCase for WAAPI, kebab-case for CSS; null members are dropped in
/// both tiers). <see cref="Offset"/> is a 0..1 progress fraction.
/// </summary>
public sealed record MicroFrame(
    double Offset,
    string? Transform = null,
    string? Opacity = null,
    string? BoxShadow = null,
    string? BackgroundPosition = null);

/// <summary>A single CSS declaration for a preset's static base style. <see cref="Property"/> is camelCase (kebab-cased for CSS output).</summary>
public sealed record MicroDecl(string Property, string Value);

/// <summary>How a micro preset degrades under <c>prefers-reduced-motion: reduce</c>.</summary>
public enum MicroReduce
{
    /// <summary>Stop entirely (<c>animation: none</c>): the element rests at its base style.</summary>
    Collapse,

    /// <summary>Keep only the opacity keyframes (transforms dropped): the beat continues without motion.</summary>
    OpacityOnly,
}
