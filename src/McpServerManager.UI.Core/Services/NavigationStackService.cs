using System.Collections.Generic;

namespace McpServerManager.UI.Core.Services;

/// <summary>
/// A back/forward navigation stack (browser-style) over items of type <typeparamref name="T"/>.
/// Used by the crosslinked requirement view (PLAN-REQSDESKTOP-001, FR-REQS-CROSSLINK-001):
/// navigating to a referenced requirement pushes onto the stack; Back/Forward traverse history.
/// </summary>
/// <typeparam name="T">The navigation entry type (e.g. a requirement id).</typeparam>
public sealed class NavigationStackService<T>
{
    private readonly List<T> _history = new();
    private int _index = -1;

    /// <summary>The current entry, or the default value when the stack is empty.</summary>
    public T? Current => _index >= 0 && _index < _history.Count ? _history[_index] : default;

    /// <summary>Whether there is an older entry to go back to.</summary>
    public bool CanGoBack => _index > 0;

    /// <summary>Whether there is a newer entry to go forward to.</summary>
    public bool CanGoForward => _index >= 0 && _index < _history.Count - 1;

    /// <summary>The number of entries currently in history.</summary>
    public int Count => _history.Count;

    /// <summary>
    /// Navigates to <paramref name="item"/>: discards any forward branch, appends the entry, and
    /// makes it current.
    /// </summary>
    /// <param name="item">The entry to navigate to.</param>
    public void Navigate(T item)
    {
        if (_index < _history.Count - 1)
            _history.RemoveRange(_index + 1, _history.Count - _index - 1);

        _history.Add(item);
        _index = _history.Count - 1;
    }

    /// <summary>Moves to the previous entry when possible; returns the (possibly unchanged) current.</summary>
    /// <returns>The current entry after the move.</returns>
    public T? Back()
    {
        if (CanGoBack)
            _index--;
        return Current;
    }

    /// <summary>Moves to the next entry when possible; returns the (possibly unchanged) current.</summary>
    /// <returns>The current entry after the move.</returns>
    public T? Forward()
    {
        if (CanGoForward)
            _index++;
        return Current;
    }

    /// <summary>Clears all history.</summary>
    public void Clear()
    {
        _history.Clear();
        _index = -1;
    }
}
