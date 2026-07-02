namespace Navius.Primitives.Components.Collapsible;

public sealed class CollapsibleContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public CollapsibleContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }
    public bool Disabled { get; set; }

    /// <summary>Stable id wiring the Trigger's <c>aria-controls</c> to the Panel.</summary>
    public string PanelId { get; } = $"navius-collapsible-{Guid.NewGuid():N}";

    public event Func<Task>? Changed;

    internal async Task SetOpenInternalAsync(bool open)
    {
        if (Open == open) return;
        Open = open;
        if (Changed is not null) await Changed.Invoke();
    }

    internal void SetDisabled(bool disabled) => Disabled = disabled;

    public Task RequestSetAsync(bool open) => _requestSetOpen(open);
    public Task RequestToggleAsync() => _requestSetOpen(!Open);
}
