using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClearPDF.Scanning;

/// <summary>
/// UI-free scan session: devices, remembered settings, live pages, tidy,
/// save / insert. The WPF dialog is a thin host; tests drive this type with
/// <see cref="MockScanner"/>.
/// </summary>
public sealed class ScanViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IScannerProvider _provider;
    private readonly ScanPreferencesStore _preferences;
    private readonly IScanOcr _ocr;
    private readonly ScanSession _session = new();
    private readonly SynchronizationContext? _ui;
    private readonly HashSet<Guid> _seen = new();
    private readonly object _gate = new();
    private readonly ScanPageStore _store;
    private CancellationTokenSource? _scanCts;
    private bool _scanning;
    private string _status = "Choose a scanner and click Scan.";
    private int _selectedIndex = -1;
    private int _blankDropped;
    private ScanDeviceInfo? _selectedDevice;
    private ScanSource _source = ScanSource.Flatbed;
    private ScanColorMode _colorMode = ScanColorMode.Color;
    private int _dpi = 300;
    private bool _duplex;
    private bool _dropBlankPages = true;
    private bool _autoDeskew = true;
    private bool _autoTrim = true;
    private ScanInsertMode _insertMode = ScanInsertMode.End;
    private int _insertAfterPage = 1;
    private string? _savedPath;

    public ScanViewModel(
        IScannerProvider provider,
        string? openPdfPath,
        ScanPreferencesStore? preferences = null,
        IScanOcr? ocr = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _preferences = preferences ?? new ScanPreferencesStore();
        _ocr = ocr ?? UnavailableScanOcr.Instance;
        _store = new ScanPageStore();
        _ui = SynchronizationContext.Current;
        OpenPdfPath = string.IsNullOrWhiteSpace(openPdfPath) ? null : openPdfPath;
        if (OpenPdfPath != null && File.Exists(OpenPdfPath))
        {
            try
            {
                OpenPdfPageCount = ScannedPdfBuilder.CountPages(OpenPdfPath);
            }
            catch (Exception)
            {
                OpenPdfPageCount = 0;
            }
        }

        ApplyPreferences(_preferences.Load());
        ReloadDevices();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ScannedPage> Pages { get; } = new();

    public ObservableCollection<ScanDeviceInfo> Devices { get; } = new();

    public string? OpenPdfPath { get; }

    public int OpenPdfPageCount { get; }

    public bool HasOpenPdf =>
        !string.IsNullOrWhiteSpace(OpenPdfPath) && File.Exists(OpenPdfPath);

    public IScanOcr Ocr => _ocr;

    public bool OcrAvailable => _ocr.IsAvailable;

    public string OcrUnavailableReason => _ocr.UnavailableReason;

    public string? SavedPath
    {
        get => _savedPath;
        private set => SetField(ref _savedPath, value);
    }

    public ScanDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!SetField(ref _selectedDevice, value))
                return;
            EnsureSourceSupported();
            RaiseCommands();
        }
    }

    public ScanSource Source
    {
        get => _source;
        set
        {
            if (!SetField(ref _source, value))
                return;
            if (value != ScanSource.Feeder)
                Duplex = false;
            RaiseCommands();
        }
    }

    public ScanColorMode ColorMode
    {
        get => _colorMode;
        set => SetField(ref _colorMode, value);
    }

    public int Dpi
    {
        get => _dpi;
        set => SetField(ref _dpi, value <= 0 ? 300 : value);
    }

    public bool Duplex
    {
        get => _duplex;
        set => SetField(ref _duplex, value && CanDuplex);
    }

    public bool DropBlankPages
    {
        get => _dropBlankPages;
        set => SetField(ref _dropBlankPages, value);
    }

    public bool AutoDeskew
    {
        get => _autoDeskew;
        set => SetField(ref _autoDeskew, value);
    }

    public bool AutoTrim
    {
        get => _autoTrim;
        set => SetField(ref _autoTrim, value);
    }

    public ScanInsertMode InsertMode
    {
        get => _insertMode;
        set => SetField(ref _insertMode, value);
    }

    public int InsertAfterPage
    {
        get => _insertAfterPage;
        set => SetField(ref _insertAfterPage, Math.Clamp(value, 0, Math.Max(OpenPdfPageCount, 0)));
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetField(ref _selectedIndex, value);
    }

    public bool IsScanning
    {
        get => _scanning;
        private set
        {
            if (!SetField(ref _scanning, value))
                return;
            RaiseCommands();
        }
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public int BlankDropped => _blankDropped;

    public int PageCount => _session.Count;

    public int VisibleCount => _session.Pages.Count(p => !p.HiddenAsBlank);

    public IEnumerable<ScannedPage> VisiblePages => _session.Pages.Where(p => !p.HiddenAsBlank);

    public IReadOnlyList<ScannedPage> SessionPages => _session.Pages;

    public bool CanDuplex =>
        SelectedDevice is { SupportsDuplex: true } && Source == ScanSource.Feeder;

    public bool CanScan => !IsScanning && SelectedDevice != null;

    public bool CanSave => !IsScanning && VisibleCount > 0;

    public bool CanInsert => CanSave && HasOpenPdf;

    public bool CanScanMore => CanScan && _session.Count > 0;

    public bool CanEditSelected => !IsScanning && SelectedIndex >= 0 && SelectedIndex < _session.Count;

    public IReadOnlyList<ScanSource> AvailableSources
    {
        get
        {
            var list = new List<ScanSource>();
            var device = SelectedDevice;
            if (device == null || device.SupportsFlatbed)
                list.Add(ScanSource.Flatbed);
            if (device is { SupportsFeeder: true })
                list.Add(ScanSource.Feeder);
            if (list.Count == 0)
                list.Add(ScanSource.Flatbed);
            return list;
        }
    }

    public void ReloadDevices()
    {
        IReadOnlyList<ScanDeviceInfo> list;
        try
        {
            list = _provider.ListDevices();
        }
        catch (Exception ex)
        {
            ApplyDeviceList(Array.Empty<ScanDeviceInfo>(), Friendly(ex));
            return;
        }

        ApplyDeviceList(list, list.Count == 0
            ? ScannerException.MessageFor(ScannerErrorKind.DeviceNotFound)
            : "Ready — click Scan.");
    }

    public async Task ReloadDevicesAsync()
    {
        Status = "Looking for scanners…";
        IReadOnlyList<ScanDeviceInfo> list;
        try
        {
            list = await Task.Run(() => _provider.ListDevices()).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ApplyDeviceList(Array.Empty<ScanDeviceInfo>(), Friendly(ex));
            return;
        }

        ApplyDeviceList(list, list.Count == 0
            ? ScannerException.MessageFor(ScannerErrorKind.DeviceNotFound)
            : "Ready — click Scan.");
    }

    private void ApplyDeviceList(IReadOnlyList<ScanDeviceInfo> list, string status)
    {
        Devices.Clear();
        foreach (var device in list)
            Devices.Add(device);

        if (list.Count == 0)
        {
            SelectedDevice = null;
            Status = status;
            RaiseCommands();
            return;
        }

        var remember = _preferences.Load().DeviceId;
        SelectedDevice = list.FirstOrDefault(d =>
                             string.Equals(d.Id, remember, StringComparison.OrdinalIgnoreCase))
                         ?? list[0];
        EnsureSourceSupported();
        Status = status;
        RaiseCommands();
    }

    public async Task ScanAsync(bool addMore = false)
    {
        if (IsScanning)
            return;
        var device = SelectedDevice;
        if (device == null)
        {
            Status = ScannerException.MessageFor(ScannerErrorKind.DeviceNotFound);
            return;
        }

        IScanner scanner;
        try
        {
            scanner = _provider.Open(device.Id);
        }
        catch (Exception ex)
        {
            Status = Friendly(ex);
            return;
        }

        if (!addMore)
            _blankDropped = 0;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _scanCts, cts)?.Dispose();
        IsScanning = true;
        Status = addMore ? "Adding pages…" : "Scanning…";
        PersistPreferences();
        var settings = CurrentSettings(device);
        try
        {
            var pages = await scanner.ScanAsync(settings, new ScanProgressSink(OnScanProgress), cts.Token);
            foreach (var page in pages)
                AddProcessed(page);
            Status = Summarize();
        }
        catch (OperationCanceledException)
        {
            Status = VisibleCount == 0
                ? ScannerException.MessageFor(ScannerErrorKind.Cancelled)
                : $"Scan stopped. {Summarize()}";
        }
        catch (Exception ex)
        {
            Status = VisibleCount == 0 ? Friendly(ex) : $"{Friendly(ex)} {Summarize()}";
        }
        finally
        {
            (scanner as IDisposable)?.Dispose();
            IsScanning = false;
            ReleaseCts();
        }
    }

    public async Task RescanSelectedAsync()
    {
        if (!CanEditSelected || SelectedDevice == null)
            return;

        IScanner scanner;
        try
        {
            scanner = _provider.Open(SelectedDevice.Id);
        }
        catch (Exception ex)
        {
            Status = Friendly(ex);
            return;
        }

        var replaceAt = SelectedIndex;
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _scanCts, cts)?.Dispose();
        IsScanning = true;
        Status = "Rescanning page…";
        try
        {
            var settings = CurrentSettings(SelectedDevice) with { MaxPages = 1 };
            var pages = await scanner.ScanAsync(settings, null, cts.Token);
            if (pages.Count == 0)
            {
                Status = "Rescan returned no page.";
                return;
            }

            var processed = Keep(_store.Persist(ScanPagePipeline.Process(pages[0], PipelineOptions())));
            _seen.Add(processed.Id);
            _session.Replace(replaceAt, processed);
            Pages[replaceAt] = processed;
            SelectedIndex = replaceAt;
            RecountBlanks();
            Status = processed.HiddenAsBlank
                ? "Rescan looked blank — hidden, tap Restore to keep it."
                : "Page replaced.";
        }
        catch (OperationCanceledException)
        {
            Status = ScannerException.MessageFor(ScannerErrorKind.Cancelled);
        }
        catch (Exception ex)
        {
            Status = Friendly(ex);
        }
        finally
        {
            (scanner as IDisposable)?.Dispose();
            IsScanning = false;
            ReleaseCts();
        }
    }

    public void CancelScan()
    {
        try
        {
            _scanCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already tearing down
        }
    }

    public void RotateSelected(int deltaDegrees = 90)
    {
        if (!CanEditSelected)
            return;
        var i = SelectedIndex;
        _session.Rotate(i, deltaDegrees);
        Pages[i] = _session.Pages[i];
        SelectedIndex = i;
    }

    public void DeleteSelected()
    {
        if (!CanEditSelected)
            return;
        var i = SelectedIndex;
        var id = _session.Pages[i].Id;
        _session.Delete(i);
        Pages.RemoveAt(i);
        _seen.Remove(id);
        SelectedIndex = Pages.Count == 0 ? -1 : Math.Min(i, Pages.Count - 1);
        Status = Summarize();
        RaiseCommands();
    }

    public void MoveSelected(int delta)
    {
        if (!CanEditSelected)
            return;
        var from = SelectedIndex;
        var to = from + delta;
        if (to < 0 || to >= _session.Count)
            return;
        Move(from, to);
    }

    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0 ||
            fromIndex >= _session.Count || toIndex >= _session.Count)
            return;
        _session.Move(fromIndex, toIndex);
        var page = Pages[fromIndex];
        Pages.RemoveAt(fromIndex);
        Pages.Insert(toIndex, page);
        SelectedIndex = toIndex;
    }

    public void TrimSelected()
    {
        if (!CanEditSelected)
            return;
        var i = SelectedIndex;
        var trimmed = Keep(_store.Persist(ScanPagePipeline.Trim(_session.Pages[i])));
        _session.Replace(i, trimmed);
        Pages[i] = trimmed;
        SelectedIndex = i;
        Status = "Trimmed margins.";
    }

    public void CropSelected(ScanCropRect rect)
    {
        if (!CanEditSelected)
            return;
        var i = SelectedIndex;
        var cropped = Keep(_store.Persist(ScanPagePipeline.Crop(_session.Pages[i], rect)));
        _session.Replace(i, cropped);
        Pages[i] = cropped;
        SelectedIndex = i;
        Status = "Cropped.";
    }

    public void RestoreSelected()
    {
        if (!CanEditSelected)
            return;
        var i = SelectedIndex;
        var restored = _session.Pages[i].RestoreHidden();
        _session.Replace(i, restored);
        Pages[i] = restored;
        SelectedIndex = i;
        RecountBlanks();
        Status = "Restored a hidden page.";
        RaiseCommands();
    }

    public void RevertSelected()
    {
        if (!CanEditSelected)
            return;
        var i = SelectedIndex;
        var reverted = _session.Pages[i].RevertToSource();
        _session.Replace(i, reverted);
        Pages[i] = reverted;
        SelectedIndex = i;
        RecountBlanks();
        Status = "Reverted to the original scan.";
        RaiseCommands();
    }

    public void SaveNew(string destinationPath) =>
        SaveNewAsync(destinationPath).GetAwaiter().GetResult();

    public async Task SaveNewAsync(string destinationPath)
    {
        var pages = VisiblePages.ToList();
        if (pages.Count == 0)
            throw new InvalidOperationException("Scan at least one page first.");
        await Task.Run(() => ScannedPdfBuilder.SaveNew(pages, destinationPath)).ConfigureAwait(true);
        SavedPath = destinationPath;
        PersistPreferences();
    }

    public void InsertIntoOpenPdf(string? destinationPath = null) =>
        InsertIntoOpenPdfAsync(destinationPath).GetAwaiter().GetResult();

    public async Task InsertIntoOpenPdfAsync(string? destinationPath = null)
    {
        var pages = VisiblePages.ToList();
        if (pages.Count == 0)
            throw new InvalidOperationException("Scan at least one page first.");
        if (string.IsNullOrWhiteSpace(OpenPdfPath) || !File.Exists(OpenPdfPath))
            throw new InvalidOperationException("No open PDF to add pages to.");

        var index = ScannedPdfBuilder.ResolveInsertIndex(InsertMode, InsertAfterPage, OpenPdfPageCount);
        var source = OpenPdfPath;
        await Task.Run(() =>
        {
            if (index >= OpenPdfPageCount)
                ScannedPdfBuilder.Append(pages, source, destinationPath);
            else
                ScannedPdfBuilder.Insert(pages, source, index, destinationPath);
        }).ConfigureAwait(true);

        SavedPath = string.IsNullOrWhiteSpace(destinationPath) ? OpenPdfPath : destinationPath;
        PersistPreferences();
    }

    public void PersistPreferences()
    {
        _preferences.Save(new ScanPreferences
        {
            DeviceId = SelectedDevice?.Id,
            Source = Source,
            ColorMode = ColorMode,
            Dpi = Dpi,
            Duplex = Duplex,
            DropBlankPages = DropBlankPages,
            AutoDeskew = AutoDeskew,
            AutoTrim = AutoTrim
        });
    }

    public void Dispose()
    {
        ReleaseCts();
        _store.Dispose();
    }

    private void ReleaseCts()
    {
        var cts = Interlocked.Exchange(ref _scanCts, null);
        if (cts == null)
            return;
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already cancelled/disposed
        }

        cts.Dispose();
    }

    private ScanSettings CurrentSettings(ScanDeviceInfo device)
    {
        var source = Source;
        if (source == ScanSource.Feeder && !device.SupportsFeeder)
            source = ScanSource.Flatbed;
        var maxPages = source == ScanSource.Flatbed ? 1 : (int?)null;
        return new ScanSettings
        {
            Source = source,
            ColorMode = ColorMode,
            Dpi = Dpi > 0 ? Dpi : 300,
            Duplex = Duplex && device.SupportsDuplex && source == ScanSource.Feeder,
            MaxPages = maxPages
        };
    }

    private ScanPipelineOptions PipelineOptions() => new()
    {
        DropBlankPages = DropBlankPages,
        AutoDeskew = AutoDeskew,
        AutoTrim = AutoTrim
    };

    private void OnScanProgress(ScanProgress progress)
    {
        if (progress.Page != null)
            AddProcessed(progress.Page);
        else if (!string.IsNullOrWhiteSpace(progress.Status))
            PostToUi(() => Status = progress.Status!);
    }

    private void AddProcessed(ScannedPage raw)
    {
        lock (_gate)
        {
            if (!_seen.Add(raw.Id))
                return;
        }

        var processed = Keep(_store.Persist(ScanPagePipeline.Process(raw, PipelineOptions())));
        lock (_gate)
        {
            if (processed.Id != raw.Id)
                _seen.Add(processed.Id);
            _session.Add(processed);
            if (processed.HiddenAsBlank)
                _blankDropped++;
        }

        PostToUi(() =>
        {
            if (Pages.Any(p => p.Id == processed.Id))
                return;
            Pages.Add(processed);
            SelectedIndex = Pages.Count - 1;
            Status = processed.HiddenAsBlank
                ? ( _blankDropped == 1
                    ? "Hidden a blank page. Restore it if that was a mark."
                    : $"Hidden {_blankDropped} blank pages. Restore any that have marks.")
                : $"Scanned page {VisibleCount}.";
            RaiseCommands();
        });
    }

    private static ScannedPage Keep(ScannedPage page) => page;

    private void RecountBlanks() =>
        _blankDropped = _session.Pages.Count(p => p.HiddenAsBlank);

    private void PostToUi(Action action)
    {
        if (_ui == null || SynchronizationContext.Current == _ui)
            action();
        else
            _ui.Post(_ => action(), null);
    }

    private string Summarize()
    {
        var pages = VisibleCount == 1 ? "1 page" : $"{VisibleCount} pages";
        if (_blankDropped == 0)
            return $"{pages} ready. Add more, or save.";
        var blanks = _blankDropped == 1 ? "1 hidden as blank" : $"{_blankDropped} hidden as blank";
        return $"{pages} ready ({blanks}). Restore any that have marks.";
    }

    private void ApplyPreferences(ScanPreferences prefs)
    {
        ColorMode = prefs.ColorMode;
        Dpi = prefs.Dpi <= 0 ? 300 : prefs.Dpi;
        Source = prefs.Source;
        Duplex = prefs.Duplex;
        DropBlankPages = prefs.DropBlankPages;
        AutoDeskew = prefs.AutoDeskew;
        AutoTrim = prefs.AutoTrim;
    }

    private void EnsureSourceSupported()
    {
        var sources = AvailableSources;
        if (!sources.Contains(Source))
            Source = sources[0];
        if (!CanDuplex)
            _duplex = false;
        OnPropertyChanged(nameof(AvailableSources));
        OnPropertyChanged(nameof(CanDuplex));
    }

    private static string Friendly(Exception ex) =>
        ex switch
        {
            ScannerException scanner => scanner.Message,
            OperationCanceledException => ScannerException.MessageFor(ScannerErrorKind.Cancelled),
            _ => string.IsNullOrWhiteSpace(ex.Message) ? "Scan failed." : ex.Message
        };

    private void RaiseCommands()
    {
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanInsert));
        OnPropertyChanged(nameof(CanScanMore));
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(CanDuplex));
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(HasOpenPdf));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    /// <summary>
    /// <see cref="Progress{T}"/> captures a sync context and would run deskew
    /// on the UI thread. This sink stays on the scanner thread until we post.
    /// </summary>
    private sealed class ScanProgressSink : IProgress<ScanProgress>
    {
        private readonly Action<ScanProgress> _handler;

        public ScanProgressSink(Action<ScanProgress> handler) => _handler = handler;

        public void Report(ScanProgress value) => _handler(value);
    }
}
