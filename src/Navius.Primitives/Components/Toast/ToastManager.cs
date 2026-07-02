namespace Navius.Primitives.Components.Toast;

/// <summary>
/// The imperative toast store — Base UI's <c>ToastManager</c>. <c>@inject</c>-able and
/// registered <b>scoped</b> by <c>AddNavius()</c> (the same DI shape as the old
/// <c>ToastService</c> it replaces). Drives the compositional tree: the Provider
/// subscribes to <see cref="Changed"/> and re-renders; the Viewport maps
/// <see cref="Toasts"/> to <c>NaviusToastRoot</c>s.
///
/// The Manager owns the queue (<see cref="Limit"/>) but <b>not</b> the timers — each
/// <c>NaviusToastRoot</c> runs its own pausable auto-close timer and calls back into
/// <see cref="Remove"/> once its exit animation has finished.
/// </summary>
public sealed class ToastManager
{
    private readonly List<ToastObject> _toasts = new();

    /// <summary>All toasts (visible + queued), oldest first.</summary>
    public IReadOnlyList<ToastObject> Toasts => _toasts;

    /// <summary>Max simultaneously-visible toasts; the rest queue (<c>data-limited</c>). Set by the Provider.</summary>
    public int Limit { get; set; } = 1;

    /// <summary>Raised after any mutation so the Provider re-renders the tree.</summary>
    public event Action? Changed;

    /// <summary>Enqueue a toast; returns its id. The Root owns the auto-close timer.</summary>
    public string Add(ToastOptions o)
    {
        var toast = new ToastObject
        {
            Title = o.Title,
            Description = o.Description,
            Type = o.Type,
            Priority = o.Priority,
            Timeout = o.Timeout,
            Data = o.Data,
            Action = o.Action,
            OnClose = o.OnClose,
        };
        _toasts.Add(toast);
        RecomputeLimited();
        Changed?.Invoke();
        return toast.Id;
    }

    /// <summary>Begin closing a toast: the Root animates it out, then calls <see cref="Remove"/>.</summary>
    public void Close(string id)
    {
        var toast = Find(id);
        if (toast is null || !toast.Open)
        {
            return;
        }

        toast.Open = false;
        RecomputeLimited();
        Changed?.Invoke();
    }

    /// <summary>
    /// Final removal, called by the Root after its exit animation. Invokes <c>OnClose</c>,
    /// then <see cref="RecomputeLimited"/> auto-promotes the next queued toast (its
    /// <c>Limited</c> flips false so it mounts + starts its timer).
    /// </summary>
    public void Remove(string id)
    {
        var toast = Find(id);
        if (toast is null)
        {
            return;
        }

        _toasts.Remove(toast);
        if (toast.OnClose is not null)
        {
            _ = toast.OnClose.Invoke();
        }
        RecomputeLimited();
        Changed?.Invoke();
    }

    /// <summary>Patch a toast's non-null fields and bump <c>UpdateKey</c> (replays the enter animation).</summary>
    public void Update(string id, ToastOptions patch)
    {
        var toast = Find(id);
        if (toast is null)
        {
            return;
        }

        if (patch.Title is not null) toast.Title = patch.Title;
        if (patch.Description is not null) toast.Description = patch.Description;
        if (patch.Type is not null) toast.Type = patch.Type;
        if (patch.Timeout is not null) toast.Timeout = patch.Timeout;
        if (patch.Data is not null) toast.Data = patch.Data;
        if (patch.Action is not null) toast.Action = patch.Action;
        if (patch.OnClose is not null) toast.OnClose = patch.OnClose;
        toast.UpdateKey++;
        Changed?.Invoke();
    }

    /// <summary>
    /// Drive a loading → success/error toast off a task: shows a sticky loading toast, then
    /// updates it to success (rethrows and updates to error on failure).
    /// </summary>
    public async Task<T> Promise<T>(Task<T> task, PromiseMessages msgs)
    {
        var id = Add(new ToastOptions(Title: msgs.Loading, Type: "loading", Timeout: null));
        try
        {
            var result = await task;
            Update(id, new ToastOptions(Title: msgs.Success, Type: "success", Timeout: null));
            return result;
        }
        catch
        {
            Update(id, new ToastOptions(Title: msgs.Error, Type: "error", Timeout: null));
            throw;
        }
    }

    /// <summary>Close every toast (they animate out and are removed by their Roots).</summary>
    public void Clear()
    {
        var any = false;
        foreach (var toast in _toasts)
        {
            if (toast.Open)
            {
                toast.Open = false;
                any = true;
            }
        }
        if (any)
        {
            RecomputeLimited();
            Changed?.Invoke();
        }
    }

    /// <summary>Open toasts that are currently visible (not queued), newest first — the stacking order.</summary>
    public IReadOnlyList<ToastObject> VisibleOrderedNewestFirst()
    {
        var visible = new List<ToastObject>();
        for (var i = _toasts.Count - 1; i >= 0; i--)
        {
            var t = _toasts[i];
            if (t.Open && !t.Limited)
            {
                visible.Add(t);
            }
        }
        return visible;
    }

    /// <summary>Stack position of <paramref name="toast"/> among the visible toasts (0 = frontmost/newest).</summary>
    public int VisibleIndexOf(ToastObject toast)
    {
        var index = 0;
        for (var i = _toasts.Count - 1; i >= 0; i--)
        {
            var t = _toasts[i];
            if (!t.Open || t.Limited)
            {
                continue;
            }
            if (ReferenceEquals(t, toast))
            {
                return index;
            }
            index++;
        }
        return 0;
    }

    private ToastObject? Find(string id) => _toasts.FirstOrDefault(t => t.Id == id);

    // Among the OPEN toasts, newest-first, the first Limit stay visible; the rest queue.
    private void RecomputeLimited()
    {
        var seen = 0;
        for (var i = _toasts.Count - 1; i >= 0; i--)
        {
            var t = _toasts[i];
            if (!t.Open)
            {
                continue;
            }
            t.Limited = seen >= Limit;
            seen++;
        }
    }
}
