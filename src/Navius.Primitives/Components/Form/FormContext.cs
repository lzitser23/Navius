using Navius.Primitives.Components.Field;

namespace Navius.Primitives.Components.Form;

/// <summary>
/// Root-level state for one <c>&lt;form&gt;</c>, cascaded from <see cref="NaviusForm"/>
/// to its fields. Keeps a registry of fields keyed by name, pushes the form's
/// errors-by-name down to each field, and raises <see cref="Changed"/> when any
/// field's validity changes so dependent parts (e.g. a disabled Submit) re-render.
/// </summary>
public sealed class FormContext
{
    private readonly Dictionary<string, FieldContext> _fields = new(StringComparer.Ordinal);
    private readonly List<FieldContext> _order = new();

    /// <summary>Raised when a registered field's validity changes.</summary>
    public event Func<Task>? Changed;

    /// <summary>True when every registered field is currently valid.</summary>
    public bool IsValid => _fields.Values.All(f => !f.IsInvalid);

    /// <summary>Register a field by name. The last registration for a name wins.</summary>
    public void Register(FieldContext field)
    {
        if (_fields.TryGetValue(field.Name, out var existing))
        {
            _order.Remove(existing);
        }

        _fields[field.Name] = field;
        _order.Add(field);
    }

    public void Unregister(string name)
    {
        if (_fields.TryGetValue(name, out var existing))
        {
            _order.Remove(existing);
        }

        _fields.Remove(name);
    }

    /// <summary>Look up a field by name.</summary>
    public FieldContext? FindField(string name) =>
        _fields.TryGetValue(name, out var field) ? field : null;

    /// <summary>The first registered field that is currently invalid, in DOM-registration order (for focus-first-invalid).</summary>
    public FieldContext? FirstInvalidField() => _order.FirstOrDefault(f => f.IsInvalid);

    /// <summary>A live snapshot of every field's value, for cross-field validation (mirrors the spec <c>formData</c>).</summary>
    public FormData BuildFormData()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in _order)
        {
            map[field.Name] = field.Value;
        }

        return new FormData(map);
    }

    /// <summary>Push the form's errors-by-name down to each field (a field with no entry is cleared).</summary>
    internal async Task ApplyErrorsAsync(IReadOnlyDictionary<string, string[]>? errors)
    {
        foreach (var field in _order)
        {
            var list = errors is not null && errors.TryGetValue(field.Name, out var e)
                ? (IReadOnlyList<string>)e
                : Array.Empty<string>();
            await field.SetFormErrorsAsync(list);
        }
    }

    /// <summary>Reveal validity on every field (fired on a submit attempt so onSubmit-mode fields surface).</summary>
    internal async Task RevealAllAsync()
    {
        foreach (var field in _order)
        {
            await field.RevealAsync();
        }
    }

    /// <summary>Clear standing server errors on every field (fired before resubmission and on reset).</summary>
    internal async Task ClearServerErrorsAsync()
    {
        foreach (var field in _order)
        {
            await field.SetServerInvalidAsync(false);
        }
    }

    /// <summary>Bubble a field-level change up to form-level subscribers.</summary>
    internal Task NotifyChangedAsync() =>
        Changed is null ? Task.CompletedTask : Changed.Invoke();
}
