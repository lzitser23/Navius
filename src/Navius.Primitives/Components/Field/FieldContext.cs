namespace Navius.Primitives.Components.Field;

/// <summary>
/// Shared state for one <see cref="NaviusField"/>, cascaded to the field's label,
/// control, description, error and items. Owns:
/// <list type="bullet">
///   <item>the generated control <c>id</c> the label points at and the control renders;</item>
///   <item>the field <see cref="Valid"/> tri-state (driven by the control's native
///   ValidityState, the consumer, server errors or form errors) and the
///   <see cref="FieldValidationMode"/> gate that decides <em>when</em> it surfaces;</item>
///   <item>the discrete Base UI interaction state — <see cref="IsFocused"/>/
///   <see cref="IsTouched"/>/<see cref="IsDirty"/>/<see cref="IsFilled"/>;</item>
///   <item>the registry of active message ids feeding <c>aria-describedby</c>.</item>
/// </list>
/// The discrete attributes are exposed as a splat-ready map via
/// <see cref="StateAttributes"/> (merged onto each part with <see cref="MergeState"/>).
/// </summary>
public sealed class FieldContext
{
    private readonly List<string> _messageOrder = new();
    private readonly HashSet<string> _activeMessageIds = new(StringComparer.Ordinal);

    // Validity from independent sources, merged into the public Valid tri-state:
    //   _consumerValidity — supplied top-down via Field.Validity / Field.Invalid (immediate).
    //   _nativeValidity   — reported by the control's native ValidityState (interop, gated).
    //   ServerInvalid     — server-side flag, auto-cleared on the next user edit (immediate).
    //   _formErrors       — errors-by-name pushed down by the form (immediate, auto-cleared on edit).
    private FieldValidity? _consumerValidity;
    private FieldValidity _nativeValidity = FieldValidity.Valid_;
    private bool _hasNative;
    private IReadOnlyList<string> _formErrors = Array.Empty<string>();

    // The validationMode gate: native validity only surfaces (data-valid/data-invalid,
    // aria-invalid) once the field has been "revealed" per its mode. External invalidity
    // — consumer Invalid, ServerInvalid, form errors — surfaces immediately regardless.
    private bool _revealed;
    private bool _wasFocused;

    public FieldContext(string name)
    {
        Name = name;
    }

    /// <summary>The field name (mirrors the spec <c>Field.Root name</c>).</summary>
    public string Name { get; }

    /// <summary>The id wired onto the control and referenced by the label's <c>for</c>.</summary>
    public string ControlId { get; } = $"navius-field-control-{Guid.NewGuid():N}";

    /// <summary>When the field surfaces its validity. Set by <see cref="NaviusField"/>.</summary>
    public FieldValidationMode ValidationMode { get; internal set; } = FieldValidationMode.OnSubmit;

    /// <summary>Whether the field (or its fieldset) is disabled. Set by <see cref="NaviusField"/>.</summary>
    public bool Disabled { get; private set; }

    // --- Discrete interaction state (Base UI field attributes) -------------------

    /// <summary><c>data-focused</c> — the control currently has focus.</summary>
    public bool IsFocused { get; private set; }

    /// <summary><c>data-touched</c> — the control has been blurred at least once.</summary>
    public bool IsTouched { get; private set; }

    /// <summary><c>data-dirty</c> — the value differs from the control's initial value.</summary>
    public bool IsDirty { get; private set; }

    /// <summary><c>data-filled</c> — the control has a non-empty value.</summary>
    public bool IsFilled { get; private set; }

    /// <summary>The control's last-known value. Feeds the form's <see cref="FormData"/>.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Server-side invalidity (the spec <c>serverInvalid</c>); auto-cleared on the next user edit.</summary>
    public bool ServerInvalid { get; private set; }

    /// <summary>Native <c>validationMessage</c> from the control, when available.</summary>
    public string? ValidationMessage { get; private set; }

    // --- Surfaced validity (tri-state, gated by validationMode) ------------------

    // The validity used for message matching: the consumer's when it marks the field
    // invalid, otherwise the native ValidityState.
    private FieldValidity EffectiveValidity =>
        _consumerValidity is { IsInvalid: true } c ? c : _nativeValidity;

    /// <summary>The current effective <see cref="FieldValidity"/> (for a <see cref="NaviusFieldValidity"/> render-prop).</summary>
    public FieldValidity CurrentValidity => EffectiveValidity;

    private bool ExternalInvalid =>
        (_consumerValidity?.IsInvalid ?? false) || ServerInvalid || _formErrors.Count > 0;

    private bool NativeOrConsumerInvalid =>
        (_consumerValidity?.IsInvalid ?? false) || (_hasNative && _nativeValidity.IsInvalid);

    /// <summary>
    /// Tri-state validity: <c>null</c> until surfaced (the spec's <c>valid: null</c>),
    /// then <c>true</c>/<c>false</c>. Drives <c>data-valid</c>/<c>data-invalid</c>.
    /// External invalidity (consumer/server/form) surfaces immediately; the control's
    /// native validity surfaces only once the field is revealed per its mode.
    /// </summary>
    public bool? Valid =>
        ExternalInvalid ? false
        : _revealed ? !NativeOrConsumerInvalid
        : (bool?)null;

    /// <summary><c>data-invalid</c> / <c>aria-invalid</c> source of truth.</summary>
    public bool IsInvalid => Valid == false;

    /// <summary>
    /// The error strings to surface (form errors first, then the native validation
    /// message). A <see cref="NaviusFieldError"/> with no child renders these.
    /// </summary>
    public IReadOnlyList<string> Errors
    {
        get
        {
            if (_formErrors.Count > 0)
            {
                return _formErrors;
            }

            if (IsInvalid && !string.IsNullOrEmpty(ValidationMessage))
            {
                return new[] { ValidationMessage! };
            }

            return Array.Empty<string>();
        }
    }

    /// <summary>Focuses the control. Set by the control; used by the form to focus the first invalid field.</summary>
    internal Func<Task>? FocusControl { get; set; }

    internal Task FocusAsync() => FocusControl?.Invoke() ?? Task.CompletedTask;

    /// <summary>Raised when state changes so parts re-render.</summary>
    public event Func<Task>? Changed;

    /// <summary>Bridges field-level changes up to the form (set by the root).</summary>
    internal Func<Task>? FormChanged { get; set; }

    // --- Splat-ready discrete attributes -----------------------------------------

    /// <summary>The discrete Base UI field-state attributes for the current state.</summary>
    public IReadOnlyDictionary<string, object> StateAttributes
    {
        get
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            if (Disabled) map["data-disabled"] = "";
            if (Valid == true) map["data-valid"] = "";
            if (Valid == false) map["data-invalid"] = "";
            if (IsDirty) map["data-dirty"] = "";
            if (IsTouched) map["data-touched"] = "";
            if (IsFilled) map["data-filled"] = "";
            if (IsFocused) map["data-focused"] = "";
            return map;
        }
    }

    /// <summary>Merge the discrete field-state attributes with a part's user attributes (user wins).</summary>
    public IDictionary<string, object> MergeState(IDictionary<string, object>? user)
    {
        var map = new Dictionary<string, object>(StateAttributes, StringComparer.Ordinal);
        if (user is not null)
        {
            foreach (var kv in user)
            {
                map[kv.Key] = kv.Value;
            }
        }

        return map;
    }

    // --- aria-describedby message registry ---------------------------------------

    /// <summary>Space-separated ids of the shown messages, in order, for <c>aria-describedby</c> (or null when none).</summary>
    public string? DescribedBy
    {
        get
        {
            var ids = _messageOrder.Where(_activeMessageIds.Contains).ToArray();
            return ids.Length == 0 ? null : string.Join(' ', ids);
        }
    }

    public void RegisterMessage(string id)
    {
        if (!_messageOrder.Contains(id, StringComparer.Ordinal))
        {
            _messageOrder.Add(id);
        }
    }

    public void UnregisterMessage(string id)
    {
        _messageOrder.RemoveAll(i => string.Equals(i, id, StringComparison.Ordinal));
        _activeMessageIds.Remove(id);
    }

    /// <summary>Mark a message id active/inactive; re-renders only when the set changes.</summary>
    public Task SetMessageActiveAsync(string id, bool active)
    {
        var changed = active ? _activeMessageIds.Add(id) : _activeMessageIds.Remove(id);
        return changed ? RaiseAsync() : Task.CompletedTask;
    }

    /// <summary>
    /// Resolve a <c>match</c> key against the effective validity. A message is shown
    /// when this returns <c>true</c> (and validity has surfaced).
    /// </summary>
    public bool Matches(string match) => EffectiveValidity.Matches(match);

    // --- State setters -----------------------------------------------------------

    /// <summary>Set whether the field is disabled (from <see cref="NaviusField"/> or a fieldset cascade).</summary>
    internal Task SetDisabledAsync(bool disabled)
    {
        if (Disabled == disabled)
        {
            return Task.CompletedTask;
        }

        Disabled = disabled;
        return RaiseAsync();
    }

    /// <summary>Replace the consumer-supplied (top-down) validity and re-render on change.</summary>
    public Task SetValidityAsync(FieldValidity validity)
    {
        if (Equals(_consumerValidity, validity))
        {
            return Task.CompletedTask;
        }

        _consumerValidity = validity;
        return RaiseAsync();
    }

    /// <summary>Set the server-invalid flag (the spec <c>serverInvalid</c>).</summary>
    public Task SetServerInvalidAsync(bool serverInvalid)
    {
        if (ServerInvalid == serverInvalid)
        {
            return Task.CompletedTask;
        }

        ServerInvalid = serverInvalid;
        return RaiseAsync();
    }

    /// <summary>Push the form-level errors for this field's name (auto-cleared on the next user edit).</summary>
    internal Task SetFormErrorsAsync(IReadOnlyList<string> errors)
    {
        if (_formErrors.SequenceEqual(errors, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        _formErrors = errors;
        return RaiseAsync();
    }

    /// <summary>
    /// Apply the combined validity + interaction snapshot reported by a native control
    /// (via the engine). Also auto-clears server/form errors on a value change and flips
    /// the validationMode reveal gate.
    /// </summary>
    public Task ApplyControlStateAsync(FieldStatePayload p)
    {
        _nativeValidity = p.ToFieldValidity();
        _hasNative = true;
        ValidationMessage = p.ValidationMessage;

        var nextValue = p.Value ?? string.Empty;
        var valueChanged = !string.Equals(Value, nextValue, StringComparison.Ordinal);
        Value = nextValue;

        if (valueChanged)
        {
            // The spec auto-clears server/form errors on the next user edit.
            ServerInvalid = false;
            _formErrors = Array.Empty<string>();
        }

        IsDirty = p.Dirty;
        IsFilled = p.Filled;
        IsTouched = p.Touched;

        var blurred = _wasFocused && !p.Focused;
        IsFocused = p.Focused;
        _wasFocused = p.Focused;

        // Surface native validity per mode: onChange on any value change; onBlur on blur.
        if ((ValidationMode == FieldValidationMode.OnChange && valueChanged)
            || (ValidationMode == FieldValidationMode.OnBlur && blurred))
        {
            _revealed = true;
        }

        return RaiseAsync();
    }

    /// <summary>Reveal validity now (called by the form on a submit attempt).</summary>
    internal Task RevealAsync()
    {
        if (_revealed)
        {
            return Task.CompletedTask;
        }

        _revealed = true;
        return RaiseAsync();
    }

    private async Task RaiseAsync()
    {
        if (Changed is not null)
        {
            await Changed.Invoke();
        }

        if (FormChanged is not null)
        {
            await FormChanged.Invoke();
        }
    }
}
