namespace Navius.Primitives.Components.Field;

/// <summary>
/// A snapshot of a field's validity, mirroring the constraint-validation
/// <c>ValidityState</c> keys from the WHATWG/MDN spec. The consumer (or the
/// control) supplies this; a <see cref="NaviusFieldError"/> with a matching
/// <c>Match</c> renders only while the corresponding flag is <c>true</c>.
/// </summary>
/// <remarks>
/// <see cref="Valid"/> is the canonical "everything passes" flag. The remaining
/// flags are individual failure reasons; at least one is <c>true</c> whenever
/// <see cref="Valid"/> is <c>false</c>. <see cref="CustomError"/> backs
/// consumer-supplied invalidity.
/// </remarks>
public sealed record FieldValidity
{
    /// <summary>The all-clear state: no constraint is violated.</summary>
    public static readonly FieldValidity Valid_ = new() { Valid = true };

    public bool Valid { get; init; } = true;
    public bool BadInput { get; init; }
    public bool CustomError { get; init; }
    public bool PatternMismatch { get; init; }
    public bool RangeOverflow { get; init; }
    public bool RangeUnderflow { get; init; }
    public bool StepMismatch { get; init; }
    public bool TooLong { get; init; }
    public bool TooShort { get; init; }
    public bool TypeMismatch { get; init; }
    public bool ValueMissing { get; init; }

    /// <summary>True when this field is invalid (i.e. not <see cref="Valid"/>).</summary>
    public bool IsInvalid => !Valid;

    /// <summary>
    /// Resolves a built-in <c>match</c> key (e.g. <c>"valueMissing"</c>) to its
    /// flag. Returns <c>false</c> for unknown keys.
    /// </summary>
    public bool Matches(string match) => match switch
    {
        "badInput" => BadInput,
        "customError" => CustomError,
        "patternMismatch" => PatternMismatch,
        "rangeOverflow" => RangeOverflow,
        "rangeUnderflow" => RangeUnderflow,
        "stepMismatch" => StepMismatch,
        "tooLong" => TooLong,
        "tooShort" => TooShort,
        "typeMismatch" => TypeMismatch,
        "valueMissing" => ValueMissing,
        "valid" => Valid,
        _ => false,
    };
}
