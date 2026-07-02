using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.PasswordToggleField;

/// <summary>
/// Shared state for one password toggle field, cascaded from
/// <c>NaviusPasswordToggleField</c> to its parts. Tracks whether the password
/// is currently revealed and lets the toggle flip it.
/// </summary>
public sealed class PasswordToggleFieldContext
{
    private readonly Func<bool, Task> _requestSetVisible;

    public PasswordToggleFieldContext(Func<bool, Task> requestSetVisible) => _requestSetVisible = requestSetVisible;

    /// <summary>Whether the password text is currently revealed.</summary>
    public bool Visible { get; private set; }

    /// <summary>Stable id for the input, so a toggle can reference it if needed.</summary>
    public string InputId { get; } = $"navius-password-toggle-field-{Guid.NewGuid():N}";

    public event Func<Task>? Changed;

    internal async Task SetVisibleInternalAsync(bool visible)
    {
        if (Visible == visible)
        {
            return;
        }

        Visible = visible;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public Task RequestSetAsync(bool visible) => _requestSetVisible(visible);

    public Task RequestToggleAsync() => _requestSetVisible(!Visible);
}
