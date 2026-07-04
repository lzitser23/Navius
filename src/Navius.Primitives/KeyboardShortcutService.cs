using Microsoft.JSInterop;
using Navius.Primitives.Interop;

namespace Navius.Primitives;

/// <summary>
/// A global keyboard-shortcut registry. <c>@inject</c>-able and registered <b>scoped</b> by
/// <c>AddNavius()</c> (the same DI shape as <c>ToastManager</c>). Consumers
/// <see cref="Register"/> a chord (e.g. <c>"mod+k"</c>) with a handler and hold the returned
/// token for the lifetime they want it live; disposing the token unregisters it.
///
/// The one hard problem this solves: a global shortcut that must call
/// <c>event.preventDefault()</c> (to stop the browser's own Ctrl+K / "/" handling) has to do
/// so <b>synchronously</b>, before any Blazor interop round trip resolves. So the service
/// pushes the current effective chord table down to a single JS keydown listener
/// (<c>createShortcutListener</c>); JS decides preventDefault off that table and then
/// dispatches the matched chord back here (<see cref="OnShortcut"/>) for handler execution.
///
/// Scoping: a shortcut registered with a non-null <see cref="ShortcutOptions.Scope"/> only
/// fires while that scope is pushed (<see cref="PushScope"/>); global (null-scope) shortcuts
/// always fire. Dialogs / command palettes push a scope on open and dispose it on close.
///
/// MVP: single chords only (no <c>g d</c>-style sequences) and normalized chord strings only
/// (per-platform ⌘/Ctrl label rendering is a helm/docs concern, not a service concern).
/// <c>"mod"</c> expands to both <c>ctrl</c> and <c>meta</c> so one registration works on
/// every platform.
/// </summary>
public sealed class KeyboardShortcutService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly List<Registration> _registrations = new();
    private readonly List<ScopeToken> _scopes = new();

    private NaviusJsInterop? _interop;
    private ShortcutListener? _listener;
    private DotNetObjectReference<KeyboardShortcutService>? _selfRef;
    private Task? _startTask;
    private bool _disposed;

    public KeyboardShortcutService(IJSRuntime js) => _js = js;

    /// <summary>
    /// Register <paramref name="handler"/> for the chord in <paramref name="options"/>. Returns
    /// an <see cref="IDisposable"/> token; dispose it to unregister (the engine-handle idiom
    /// used everywhere else in the codebase, not a Guid/Unregister pair).
    /// </summary>
    public IDisposable Register(ShortcutOptions options, Func<Task> handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        var registration = new Registration(this, options, handler, ExpandChord(options.Chord));
        _registrations.Add(registration);
        _ = SyncAsync();
        return registration;
    }

    /// <summary>
    /// Push a scope onto the active stack; scoped shortcuts naming it now fire. Returns an
    /// <see cref="IDisposable"/> token; dispose it to pop the scope (typically on dialog close).
    /// </summary>
    public IDisposable PushScope(string scope)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);

        var token = new ScopeToken(this, scope);
        _scopes.Add(token);
        _ = SyncAsync();
        return token;
    }

    /// <summary>Invoked from JS when a live chord matches: run every active handler for it.</summary>
    [JSInvokable]
    public async Task OnShortcut(string chord)
    {
        var handlers = EffectiveRegistrations()
            .Where(r => r.CanonicalChords.Contains(chord))
            .Select(r => r.Handler)
            .ToList();

        foreach (var handler in handlers)
        {
            try
            {
                await handler();
            }
            catch
            {
                // A consumer handler threw: swallow so one bad handler cannot tear down the
                // shared global listener (the dispatcher stays live for every other shortcut).
            }
        }
    }

    private void RemoveRegistration(Registration registration)
    {
        if (_registrations.Remove(registration))
        {
            _ = SyncAsync();
        }
    }

    private void PopScope(ScopeToken token)
    {
        if (_scopes.Remove(token))
        {
            _ = SyncAsync();
        }
    }

    // Global (null-scope) shortcuts always fire; a scoped shortcut fires only while its scope
    // is present in the pushed stack.
    private bool IsActive(Registration r)
        => r.Options.Scope is null || _scopes.Any(s => s.Scope == r.Options.Scope);

    private IEnumerable<Registration> EffectiveRegistrations() => _registrations.Where(IsActive);

    private Task EnsureStartedAsync() => _startTask ??= StartAsync();

    private async Task StartAsync()
    {
        _interop = new NaviusJsInterop(_js);
        _selfRef = DotNetObjectReference.Create(this);
        _listener = await _interop.CreateShortcutListenerAsync(_selfRef);
    }

    private async Task SyncAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await EnsureStartedAsync();
            if (_listener is null || _disposed)
            {
                return;
            }
            await _listener.UpdateChordsAsync(BuildTable());
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // Aggregate the effective registrations into one entry per canonical chord: preventDefault
    // (and the input/repeat guards) apply if ANY active registration for that chord requests it.
    private ChordEntry[] BuildTable()
    {
        var map = new Dictionary<string, ChordEntry>();
        foreach (var registration in EffectiveRegistrations())
        {
            foreach (var chord in registration.CanonicalChords)
            {
                var existing = map.TryGetValue(chord, out var e) ? e : new ChordEntry(chord, false, false, false);
                map[chord] = existing with
                {
                    PreventDefault = existing.PreventDefault || registration.Options.PreventDefault,
                    AllowInInputs = existing.AllowInInputs || registration.Options.AllowInInputs,
                    Repeat = existing.Repeat || registration.Options.Repeat,
                };
            }
        }
        return map.Values.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _registrations.Clear();
        _scopes.Clear();

        // Await any in-flight startup so a StartAsync that raced this dispose has finished
        // assigning _listener before we tear it down; otherwise its listener (and the document
        // keydown handler it registered) would leak, since destroy() would never run.
        if (_startTask is not null)
        {
            try
            {
                await _startTask;
            }
            catch
            {
                // Startup faulted (e.g. JS disconnected mid-listener-creation): _listener stayed
                // null, so there is nothing extra to tear down here.
            }
        }

        if (_listener is not null)
        {
            await _listener.DisposeAsync();
        }
        if (_interop is not null)
        {
            await _interop.DisposeAsync();
        }
        _selfRef?.Dispose();
    }

    // --- Chord canonicalization --------------------------------------------------------------
    // Produce the fixed-order canonical form(s) JS matches against: ctrl+alt+shift+meta+key,
    // lowercased. "mod" fans out to a ctrl variant and a meta variant so one registration is
    // cross-platform.

    internal static IReadOnlyList<string> ExpandChord(string chord)
    {
        bool ctrl = false, alt = false, shift = false, meta = false, mod = false;
        string? key = null;

        foreach (var raw in chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    ctrl = true;
                    break;
                case "alt":
                case "option":
                case "opt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "meta":
                case "cmd":
                case "command":
                case "win":
                case "super":
                    meta = true;
                    break;
                case "mod":
                    mod = true;
                    break;
                default:
                    key = NormalizeKey(raw.ToLowerInvariant());
                    break;
            }
        }

        if (key is null)
        {
            return Array.Empty<string>();
        }

        if (mod)
        {
            var ctrlVariant = Compose(true, alt, shift, meta, key);
            var metaVariant = Compose(ctrl, alt, shift, true, key);
            return ctrlVariant == metaVariant ? new[] { ctrlVariant } : new[] { ctrlVariant, metaVariant };
        }

        return new[] { Compose(ctrl, alt, shift, meta, key) };
    }

    private static string Compose(bool ctrl, bool alt, bool shift, bool meta, string key)
    {
        var parts = new List<string>(5);
        if (ctrl) parts.Add("ctrl");
        if (alt) parts.Add("alt");
        if (shift) parts.Add("shift");
        if (meta) parts.Add("meta");
        parts.Add(key);
        return string.Join("+", parts);
    }

    private static string NormalizeKey(string k) => k switch
    {
        " " or "space" or "spacebar" => "space",
        "esc" => "escape",
        "return" => "enter",
        "up" => "arrowup",
        "down" => "arrowdown",
        "left" => "arrowleft",
        "right" => "arrowright",
        _ => k,
    };

    private sealed record ChordEntry(string Chord, bool PreventDefault, bool AllowInInputs, bool Repeat);

    private sealed class Registration : IDisposable
    {
        private readonly KeyboardShortcutService _owner;
        private bool _disposed;

        public Registration(KeyboardShortcutService owner, ShortcutOptions options, Func<Task> handler, IReadOnlyList<string> canonicalChords)
        {
            _owner = owner;
            Options = options;
            Handler = handler;
            CanonicalChords = canonicalChords;
        }

        public ShortcutOptions Options { get; }
        public Func<Task> Handler { get; }
        public IReadOnlyList<string> CanonicalChords { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _owner.RemoveRegistration(this);
        }
    }

    private sealed class ScopeToken : IDisposable
    {
        private readonly KeyboardShortcutService _owner;
        private bool _disposed;

        public ScopeToken(KeyboardShortcutService owner, string scope)
        {
            _owner = owner;
            Scope = scope;
        }

        public string Scope { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _owner.PopScope(this);
        }
    }
}

/// <summary>
/// Options for <see cref="KeyboardShortcutService.Register"/>. <see cref="Chord"/> is a
/// normalized, case-insensitive chord (<c>"mod+k"</c>, <c>"shift+?"</c>, <c>"escape"</c>);
/// a null <see cref="Scope"/> means global. <see cref="PreventDefault"/> stops the browser's
/// default for the chord; <see cref="AllowInInputs"/> lets it fire while an editable element
/// is focused; <see cref="Repeat"/> lets a held key re-fire.
/// </summary>
public sealed record ShortcutOptions(
    string Chord,
    string? Scope = null,
    bool PreventDefault = true,
    bool AllowInInputs = false,
    bool Repeat = false);
