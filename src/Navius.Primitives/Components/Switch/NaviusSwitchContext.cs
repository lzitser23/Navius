namespace Navius.Primitives.Components.Switch;

/// <summary>
/// Cascaded from <see cref="NaviusSwitch"/> down to <see cref="NaviusSwitchThumb"/>
/// so the thumb can mirror the root's discrete state (checked/disabled/readonly/
/// required) without re-deriving it.
/// </summary>
public sealed class NaviusSwitchContext
{
    public bool Checked { get; init; }

    public bool Disabled { get; init; }

    public bool ReadOnly { get; init; }

    public bool Required { get; init; }
}
