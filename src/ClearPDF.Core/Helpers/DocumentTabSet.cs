namespace ClearPDF.Helpers;

/// <summary>
/// Open-document tabs: add, close, switch, and reuse an existing tab when
/// the same path is opened again. Find state lives on each tab.
/// </summary>
public sealed class DocumentTab
{
    public DocumentTab(string path)
    {
        Path = path;
        var name = System.IO.Path.GetFileName(path);
        Title = string.IsNullOrEmpty(name) ? path : name;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Path { get; }
    public string Title { get; }
    public FindSession Find { get; } = new();
}

public readonly record struct DocumentTabOpenResult(DocumentTab Tab, bool AlreadyOpen);

public sealed class DocumentTabSet
{
    private readonly List<DocumentTab> _tabs = new();
    private int _current = -1;

    public IReadOnlyList<DocumentTab> Tabs => _tabs;
    public int Count => _tabs.Count;
    public int CurrentIndex => _current;
    public DocumentTab? Current =>
        _current >= 0 && _current < _tabs.Count ? _tabs[_current] : null;

    public DocumentTabOpenResult Open(string path)
    {
        var existing = _tabs.FindIndex(t => DocumentPath.Same(t.Path, path));
        if (existing >= 0)
        {
            _current = existing;
            return new DocumentTabOpenResult(_tabs[existing], AlreadyOpen: true);
        }

        var tab = new DocumentTab(path);
        _tabs.Add(tab);
        _current = _tabs.Count - 1;
        return new DocumentTabOpenResult(tab, AlreadyOpen: false);
    }

    public bool Select(string id)
    {
        var i = _tabs.FindIndex(t => t.Id == id);
        if (i < 0)
            return false;
        _current = i;
        return true;
    }

    public bool SelectNext()
    {
        if (_tabs.Count == 0)
            return false;
        _current = (_current + 1 + _tabs.Count) % _tabs.Count;
        return true;
    }

    public bool SelectPrevious()
    {
        if (_tabs.Count == 0)
            return false;
        _current = (_current - 1 + _tabs.Count) % _tabs.Count;
        return true;
    }

    public bool CloseCurrent() => Current != null && Close(Current.Id);

    public bool Close(string id)
    {
        var i = _tabs.FindIndex(t => t.Id == id);
        if (i < 0)
            return false;

        _tabs.RemoveAt(i);
        if (_tabs.Count == 0)
        {
            _current = -1;
            return true;
        }

        if (_current > i)
            _current--;
        else if (_current == i)
            _current = Math.Min(i, _tabs.Count - 1);

        return true;
    }
}
