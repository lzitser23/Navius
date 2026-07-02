namespace Navius.Primitives.Components.Form;

/// <summary>
/// A read-only snapshot of the live field values in a <see cref="NaviusForm"/>,
/// keyed by field name. Passed to a <see cref="NaviusFormMessage"/>'s custom
/// <c>MatchFn</c> so a predicate can validate against other fields (mirrors the
/// <c>formData</c> argument the spec passes to <c>Form.Message match</c>).
/// </summary>
/// <remarks>
/// Values are the latest value strings surfaced by each field's
/// <see cref="NaviusFormControl"/> (read from its splatted <c>value</c> attribute).
/// Fields with no surfaced value are reported as the empty string.
/// </remarks>
public sealed class FormData
{
    private readonly IReadOnlyDictionary<string, string> _values;

    internal FormData(IReadOnlyDictionary<string, string> values) => _values = values;

    /// <summary>The value of field <paramref name="name"/>, or <c>null</c> when no such field is registered.</summary>
    public string? Get(string name) => _values.TryGetValue(name, out var v) ? v : null;

    /// <summary>The value of field <paramref name="name"/> (empty string when absent).</summary>
    public string this[string name] => Get(name) ?? string.Empty;

    /// <summary>Every registered field name.</summary>
    public IEnumerable<string> Names => _values.Keys;
}
