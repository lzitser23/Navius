namespace Navius.Primitives.Components.TagInput;

/// <summary>A key/char that commits the current field text into a chip.</summary>
public enum TagDelimiter
{
    /// <summary>The Enter key.</summary>
    Enter,
    /// <summary>A comma (also splits pasted text).</summary>
    Comma,
    /// <summary>The Tab key.</summary>
    Tab,
    /// <summary>A space (also splits pasted text).</summary>
    Space,
}

/// <summary>Cascaded from a <c>NaviusTag</c> to its <c>NaviusTagRemove</c> so the button knows which value it removes.</summary>
public sealed record TagValueContext(string Value);

/// <summary>
/// Shared state for one <c>NaviusTagInput</c>. The root owns the tag collection, the
/// chip-navigation highlight and the one-shot focus requests; the List/Tag/TagRemove/Field
/// parts read this surface and route their actions back through the constructor delegates
/// (mirroring the Combobox chip conventions without touching Combobox). Parts subscribe to
/// <see cref="Changed"/> to re-render when the root mutates state.
/// </summary>
public sealed class TagInputContext
{
    private readonly Func<string, Task> _add;
    private readonly Func<int, Task> _removeAt;
    private readonly Func<Task> _removeHighlighted;
    private readonly Func<int, bool, Task> _highlight;

    public TagInputContext(
        Func<string, Task> add,
        Func<int, Task> removeAt,
        Func<Task> removeHighlighted,
        Func<int, bool, Task> highlight)
    {
        _add = add;
        _removeAt = removeAt;
        _removeHighlighted = removeHighlighted;
        _highlight = highlight;
    }

    /// <summary>The committed tags, in order.</summary>
    public IReadOnlyList<string> Tags { get; internal set; } = Array.Empty<string>();

    /// <summary>The chip-navigation highlight, or -1 when the field holds focus.</summary>
    public int HighlightedIndex { get; internal set; } = -1;

    public bool Disabled { get; internal set; }

    /// <summary>Commit the field text on blur (mirrors the root's AddOnBlur).</summary>
    public bool AddOnBlur { get; internal set; }

    /// <summary>The active commit delimiters (the field reads these for key/char detection).</summary>
    public IReadOnlyList<TagDelimiter> Delimiters { get; internal set; } = Array.Empty<TagDelimiter>();

    public bool Empty => Tags.Count == 0;

    // One-shot focus requests consumed by the parts after a re-render.
    internal int PendingChipFocus { get; set; } = -1;
    internal bool PendingFieldFocus { get; set; }

    /// <summary>The Tag at <paramref name="index"/> claims a queued focus request (and clears it).</summary>
    public bool ConsumeChipFocus(int index)
    {
        if (PendingChipFocus != index)
        {
            return false;
        }

        PendingChipFocus = -1;
        return true;
    }

    /// <summary>The Field claims a queued focus request (and clears it).</summary>
    public bool ConsumeFieldFocus()
    {
        if (!PendingFieldFocus)
        {
            return false;
        }

        PendingFieldFocus = false;
        return true;
    }

    public event Action? Changed;

    internal void RaiseChanged() => Changed?.Invoke();

    public int IndexOf(string tag)
    {
        for (var i = 0; i < Tags.Count; i++)
        {
            if (Tags[i] == tag)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Commit <paramref name="text"/> as a new chip (subject to transform/validate/duplicate/max rules).</summary>
    public Task AddAsync(string text) => _add(text);

    /// <summary>Remove the chip at <paramref name="index"/> and move the highlight to an adjacent chip.</summary>
    public Task RemoveAtAsync(int index) => _removeAt(index);

    /// <summary>Remove the currently-highlighted chip and return focus to the field (empty-Backspace path).</summary>
    public Task RemoveHighlightedAsync() => _removeHighlighted();

    /// <summary>Set the highlight to <paramref name="index"/> (-1 = field); when <paramref name="focus"/>, also move DOM focus.</summary>
    public Task HighlightAsync(int index, bool focus) => _highlight(index, focus);
}
