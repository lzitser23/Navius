namespace Navius.Primitives.Components.Field;

/// <summary>
/// When a field surfaces its validity (mirrors Base UI's
/// <c>Field.Root validationMode</c>). Until validity is surfaced a field reports
/// neither <c>data-valid</c> nor <c>data-invalid</c> (the spec's <c>valid: null</c>).
/// </summary>
public enum FieldValidationMode
{
    /// <summary>Validity is surfaced only after a submit attempt, then live (the spec default).</summary>
    OnSubmit,

    /// <summary>Validity is surfaced once the control is blurred, then live.</summary>
    OnBlur,

    /// <summary>Validity is surfaced on the first input, then live.</summary>
    OnChange,
}
