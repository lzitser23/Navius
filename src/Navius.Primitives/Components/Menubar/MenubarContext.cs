using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Root state for a menubar. Owns which menu (by its <c>Value</c>) is currently open
/// — at most one — and brokers open/close requests so opening one menu closes any
/// other. Each <see cref="NaviusMenubarMenu"/> reads <see cref="IsOpen"/> for its own
/// value; parts subscribe to <see cref="Changed"/> to re-render. ArrowLeft/Right + Home/End
/// roving between the top-level triggers is provided by the engine over the trigger row.
/// </summary>
public sealed class MenubarContext
{
    private readonly Func<string?, Task> _requestSetValue;
    private readonly List<string> _order = new();

    public MenubarContext(Func<string?, Task> requestSetValue) => _requestSetValue = requestSetValue;

    /// <summary>Registers a menu's value in document order so adjacent-menu navigation can resolve neighbours.</summary>
    public void RegisterMenu(string value)
    {
        if (!_order.Contains(value))
        {
            _order.Add(value);
        }
    }

    public void UnregisterMenu(string value) => _order.Remove(value);

    /// <summary>
    /// Open the menu adjacent to the currently-open one in <paramref name="direction"/>
    /// (+1 = next, -1 = previous). Used when ArrowLeft/Right is pressed inside an open menu
    /// at the top level. Does not wrap unless <see cref="Loop"/>.
    /// </summary>
    public Task MoveToAdjacentAsync(int direction)
    {
        if (OpenValue is null || _order.Count == 0)
        {
            return Task.CompletedTask;
        }

        var index = _order.IndexOf(OpenValue);
        if (index < 0)
        {
            return Task.CompletedTask;
        }

        var target = index + direction;
        if (target < 0 || target >= _order.Count)
        {
            if (!Loop)
            {
                return Task.CompletedTask;
            }

            target = (target + _order.Count) % _order.Count;
        }

        return _requestSetValue(_order[target]);
    }

    /// <summary>The value of the currently open menu, or <c>null</c> when all menus are closed.</summary>
    public string? OpenValue { get; private set; }

    /// <summary><c>"horizontal"</c> or <c>"vertical"</c> — drives the trigger-roving axis.</summary>
    public string Orientation { get; internal set; } = "horizontal";

    /// <summary>
    /// Resolved reading direction (<c>"ltr"</c> or <c>"rtl"</c>). Flips ArrowLeft/Right
    /// trigger roving and submenu open/close keys. Cascaded to content/sub parts.
    /// </summary>
    public string Dir { get; internal set; } = "ltr";

    /// <summary>
    /// Whether keyboard navigation between triggers (and inside menus) wraps at the
    /// ends. the spec defaults this to <c>false</c>.
    /// </summary>
    public bool Loop { get; internal set; }

    /// <summary>
    /// Root modality (the spec default true), overridable via <c>NaviusMenubar.Modal</c>.
    /// Each open menu's Popup reads this (through <see cref="MenubarMenuContext.Modal"/>) to
    /// drive scroll-lock + the outside-pointer guard.
    /// </summary>
    public bool Modal { get; internal set; } = true;

    public event Func<Task>? Changed;

    /// <summary>True when the menu identified by <paramref name="value"/> is the open one.</summary>
    public bool IsOpen(string value) => OpenValue is not null && string.Equals(OpenValue, value, StringComparison.Ordinal);

    /// <summary>Root-only: push the authoritative open value and re-render parts if it changed.</summary>
    internal async Task SetOpenValueInternalAsync(string? value)
    {
        if (string.Equals(OpenValue, value, StringComparison.Ordinal))
        {
            return;
        }

        OpenValue = value;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    /// <summary>Open the menu with <paramref name="value"/> (closing any other).</summary>
    public Task RequestOpenAsync(string value) => _requestSetValue(value);

    /// <summary>Close <paramref name="value"/> if it is the open menu; no-op otherwise.</summary>
    public Task RequestCloseAsync(string value) =>
        IsOpen(value) ? _requestSetValue(null) : Task.CompletedTask;

    /// <summary>Toggle the menu with <paramref name="value"/>.</summary>
    public Task RequestToggleAsync(string value) => _requestSetValue(IsOpen(value) ? null : value);

    /// <summary>Close whatever menu is open.</summary>
    public Task RequestCloseAllAsync() => _requestSetValue(null);
}
