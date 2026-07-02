using System.Reflection;

namespace Navius.Primitives.Components.Slot;

/// <summary>
/// Merges the attribute dictionaries that <see cref="NaviusSlot"/> forwards onto
/// its single child, reproducing the spec Slot's prop-merge semantics on an
/// attribute dictionary:
/// <list type="bullet">
/// <item><c>class</c> / <c>className</c> are normalized to a single <c>class</c>
/// attribute and concatenated (space separated), so the forwarded value and any
/// value the child already carries both survive.</item>
/// <item><c>style</c> is merged per CSS property (object-spread semantics): both
/// declarations are parsed into a property map, the override/child value wins per
/// property, and the result is re-serialized with no duplicate declarations.</item>
/// <item><c>on*</c> event handlers are <em>composed</em> rather than overwritten:
/// the original (child) handler runs first, then the override (parent/slot)
/// handler runs — matching the spec <c>composeEventHandlers</c>, which never drops a
/// colliding handler.</item>
/// </list>
/// All other keys are last-wins (the <paramref name="overrides"/> dictionary
/// wins).
/// </summary>
public static class SlotMerge
{
    private const string ClassKey = "class";

    public static IReadOnlyDictionary<string, object> Combine(
        IReadOnlyDictionary<string, object>? forwarded,
        IDictionary<string, object>? overrides)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        if (forwarded is not null)
        {
            foreach (var kvp in forwarded)
            {
                Add(result, kvp.Key, kvp.Value);
            }
        }

        if (overrides is not null)
        {
            foreach (var kvp in overrides)
            {
                Add(result, kvp.Key, kvp.Value);
            }
        }

        return result;
    }

    private static void Add(Dictionary<string, object> target, string key, object value)
    {
        // Normalize React-style `className` to the HTML `class` attribute so the
        // two spellings collide on a single key (the spec operates in one class
        // namespace; emitting both `class` and `className` would be invalid HTML).
        if (IsClassKey(key))
        {
            key = ClassKey;
            if (target.TryGetValue(ClassKey, out var existingClass))
            {
                target[ClassKey] = Join(existingClass, value, separator: " ");
                return;
            }

            target[ClassKey] = value;
            return;
        }

        // the spec spreads style as an object; merge per CSS property so duplicate
        // declarations collapse and the incoming value wins per property.
        if (string.Equals(key, "style", StringComparison.Ordinal) && target.TryGetValue(key, out var existingStyle))
        {
            target[key] = MergeStyle(existingStyle, value);
            return;
        }

        // the spec composeEventHandlers: run the existing (child) handler first, then
        // the incoming (parent/slot) handler. Never drop a colliding delegate.
        if (IsEventKey(key) &&
            target.TryGetValue(key, out var existingHandler) &&
            existingHandler is Delegate first &&
            value is Delegate second)
        {
            var composed = ComposeHandlers(first, second);
            if (composed is not null)
            {
                target[key] = composed;
                return;
            }
        }

        target[key] = value;
    }

    private static bool IsClassKey(string key) =>
        string.Equals(key, "class", StringComparison.Ordinal) ||
        string.Equals(key, "className", StringComparison.Ordinal);

    // Blazor binds event attributes by the `on` prefix (onclick, onkeydown, ...).
    private static bool IsEventKey(string key) =>
        key.Length > 2 &&
        (key[0] == 'o' || key[0] == 'O') &&
        (key[1] == 'n' || key[1] == 'N');

    private static string Join(object? a, object? b, string separator)
    {
        var left = a?.ToString();
        var right = b?.ToString();

        if (string.IsNullOrEmpty(left))
        {
            return right ?? string.Empty;
        }

        if (string.IsNullOrEmpty(right))
        {
            return left;
        }

        return left + separator + right;
    }

    /// <summary>
    /// Merges two inline-style strings with object-spread semantics: parse both
    /// into ordered property maps, let <paramref name="incoming"/> win per
    /// property, and re-serialize without duplicate declarations.
    /// </summary>
    private static string MergeStyle(object? existing, object? incoming)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        ParseStyle(existing?.ToString(), map, order);
        ParseStyle(incoming?.ToString(), map, order);

        return string.Join("; ", order.Select(prop => $"{prop}: {map[prop]}"));
    }

    private static void ParseStyle(string? style, Dictionary<string, string> map, List<string> order)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return;
        }

        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var property = declaration[..colon].Trim();
            var declValue = declaration[(colon + 1)..].Trim();
            if (property.Length == 0)
            {
                continue;
            }

            if (!map.ContainsKey(property))
            {
                order.Add(property);
            }

            map[property] = declValue;
        }
    }

    /// <summary>
    /// Builds a single delegate that invokes <paramref name="first"/> (child) then
    /// <paramref name="second"/> (parent/slot). Returns <c>null</c> when the two
    /// delegates are not compose-compatible, in which case the caller falls back to
    /// last-wins.
    /// </summary>
    private static Delegate? ComposeHandlers(Delegate first, Delegate second)
    {
        var firstParams = first.Method.GetParameters();
        var secondParams = second.Method.GetParameters();

        // Parameterless: Action / Func<Task>.
        if (firstParams.Length == 0 && secondParams.Length == 0)
        {
            return new Func<Task>(async () =>
            {
                await InvokeAsync(first);
                await InvokeAsync(second);
            });
        }

        // Single-arg: Action<T> / Func<T, Task>. Compose only when the argument
        // types are compatible so the combined delegate can be invoked once.
        if (firstParams.Length == 1 && secondParams.Length == 1)
        {
            var argType = firstParams[0].ParameterType;
            if (argType == secondParams[0].ParameterType)
            {
                var composer = typeof(SlotMerge)
                    .GetMethod(nameof(ComposeTyped), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(argType);
                return (Delegate)composer.Invoke(null, new object[] { first, second })!;
            }
        }

        return null;
    }

    private static Func<TArg, Task> ComposeTyped<TArg>(Delegate first, Delegate second) =>
        async arg =>
        {
            await InvokeAsync(first, arg);
            await InvokeAsync(second, arg);
        };

    private static Task InvokeAsync(Delegate handler, object? arg = null)
    {
        var args = arg is null ? null : new[] { arg };
        var result = handler.DynamicInvoke(args);
        return result as Task ?? Task.CompletedTask;
    }
}
