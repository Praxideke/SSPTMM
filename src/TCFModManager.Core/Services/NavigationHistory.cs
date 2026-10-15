namespace TCFModManager.Core.Services;

//
// Fork (SSPTMM 1.4.2): the places the window has shown, for Steam's back and forward arrows - a
// browser's history: going somewhere new drops whatever was ahead, and Back and Forward move
// along it without changing it.
//
// Return is for a place reached by closing what was over it (an item page's own close): when it
// is the place just before, that is a step back rather than somewhere new, so Forward still
// reopens what was closed.
//
public sealed class NavigationHistory<T>(int capacity = 50, IEqualityComparer<T>? comparer = null)
    where T : notnull
{
    private readonly List<T> _entries = [];
    private readonly IEqualityComparer<T> _comparer = comparer ?? EqualityComparer<T>.Default;
    private int _index = -1;

    /// <summary>Raised whenever the place, or what Back and Forward can reach, changes.</summary>
    public event EventHandler? Changed;

    public T? Current => _index >= 0 ? _entries[_index] : default;

    public bool CanGoBack => _index > 0;

    public bool CanGoForward => _index < _entries.Count - 1;

    /// <summary>Somewhere new. Nothing when it is where the history already stands.</summary>
    public void Visit(T place)
    {
        if (_index >= 0 && _comparer.Equals(_entries[_index], place)) return;

        _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(place);
        if (_entries.Count > capacity) _entries.RemoveAt(0);
        _index = _entries.Count - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Back to a place by closing what was over it: a step back when it is the place
    /// before, otherwise somewhere new.</summary>
    public void Return(T place)
    {
        if (_index > 0 && _comparer.Equals(_entries[_index - 1], place))
        {
            _index--;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        Visit(place);
    }

    /// <summary>Every place kept, oldest first; Index is where the history stands in it.</summary>
    public IReadOnlyList<T> Entries => _entries;

    public int Index => _index;

    /// <summary>Straight to one of the Entries, now current (the arrows' right-click list);
    /// default when it is out of range or already where the history stands.</summary>
    public T? MoveTo(int index) => index == _index ? default : Move(index - _index);

    /// <summary>The place before, now current; default when there is none.</summary>
    public T? Back() => Move(-1);

    /// <summary>The place after, now current; default when there is none.</summary>
    public T? Forward() => Move(1);

    /// <summary>Drops the current place - one that could not be shown again - and stands on the
    /// one before it.</summary>
    public void DropCurrent()
    {
        if (_index < 0) return;

        _entries.RemoveAt(_index);
        _index = Math.Min(_index, _entries.Count) - 1;
        if (_index < 0 && _entries.Count > 0) _index = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private T? Move(int step)
    {
        var next = _index + step;
        if (next < 0 || next >= _entries.Count) return default;

        _index = next;
        Changed?.Invoke(this, EventArgs.Empty);
        return _entries[_index];
    }
}
