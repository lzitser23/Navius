using System.Text.Json.Serialization;

namespace Navius.Primitives.Components.Field;

/// <summary>
/// The combined validity + interaction snapshot the engine's
/// <c>createConstraintValidation</c> reports to
/// <see cref="NaviusInput.OnFieldStateChange"/>. Property names match the JS
/// payload (camelCase). Validity flags map onto a <see cref="FieldValidity"/>;
/// the interaction flags (<see cref="Focused"/>/<see cref="Touched"/>/
/// <see cref="Dirty"/>/<see cref="Filled"/>) drive the discrete field-state
/// attributes.
/// </summary>
public sealed class FieldStatePayload
{
    [JsonPropertyName("valueMissing")] public bool ValueMissing { get; set; }
    [JsonPropertyName("typeMismatch")] public bool TypeMismatch { get; set; }
    [JsonPropertyName("patternMismatch")] public bool PatternMismatch { get; set; }
    [JsonPropertyName("tooLong")] public bool TooLong { get; set; }
    [JsonPropertyName("tooShort")] public bool TooShort { get; set; }
    [JsonPropertyName("rangeUnderflow")] public bool RangeUnderflow { get; set; }
    [JsonPropertyName("rangeOverflow")] public bool RangeOverflow { get; set; }
    [JsonPropertyName("stepMismatch")] public bool StepMismatch { get; set; }
    [JsonPropertyName("badInput")] public bool BadInput { get; set; }
    [JsonPropertyName("customError")] public bool CustomError { get; set; }
    [JsonPropertyName("valid")] public bool Valid { get; set; } = true;
    [JsonPropertyName("validationMessage")] public string? ValidationMessage { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }

    [JsonPropertyName("focused")] public bool Focused { get; set; }
    [JsonPropertyName("touched")] public bool Touched { get; set; }
    [JsonPropertyName("dirty")] public bool Dirty { get; set; }
    [JsonPropertyName("filled")] public bool Filled { get; set; }

    /// <summary>Project the native validity flags onto a <see cref="FieldValidity"/>.</summary>
    public FieldValidity ToFieldValidity() => new()
    {
        Valid = Valid,
        ValueMissing = ValueMissing,
        TypeMismatch = TypeMismatch,
        PatternMismatch = PatternMismatch,
        TooLong = TooLong,
        TooShort = TooShort,
        RangeUnderflow = RangeUnderflow,
        RangeOverflow = RangeOverflow,
        StepMismatch = StepMismatch,
        BadInput = BadInput,
        CustomError = CustomError,
    };
}
