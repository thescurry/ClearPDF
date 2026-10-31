using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

[Collection("AtomicFiles")]
public class ScanViewModelTests
{
    [Fact]
    public async Task Scan_adds_live_pages_from_a_fake_feeder()
    {
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(InkPage(1));
        mfp.Queue.Add(InkPage(2));
        using var vm = NewVm(mfp, source: ScanSource.Feeder);

        await vm.ScanAsync();

        Assert.Equal(2, vm.PageCount);
        Assert.Equal(new[] { 1, 2 }, vm.SessionPages.Select(ScanTestPages.Tag));
        Assert.Equal(2, vm.Pages.Count);
        Assert.Contains("page", vm.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Blank_pages_stay_in_the_strip_and_can_be_restored()
    {
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(WhitePage());
        mfp.Queue.Add(InkPage(4));
        mfp.Queue.Add(WhitePage());
        using var vm = NewVm(mfp, source: ScanSource.Feeder);
        vm.DropBlankPages = true;
        vm.AutoDeskew = false;
        vm.AutoTrim = false;

        await vm.ScanAsync();

        Assert.Equal(3, vm.PageCount);
        Assert.Equal(1, vm.VisibleCount);
        Assert.Equal(2, vm.BlankDropped);
        Assert.Equal(4, ScanTestPages.Tag(vm.VisiblePages.Single()));
        Assert.Contains("blank", vm.Status, StringComparison.OrdinalIgnoreCase);

        vm.SelectedIndex = 0;
        vm.RestoreSelected();
        Assert.Equal(2, vm.VisibleCount);
        Assert.False(vm.SessionPages[0].HiddenAsBlank);
    }

    [Fact]
    public async Task Reorder_rotate_delete_and_rescan_use_the_session()
    {
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(InkPage(1));
        mfp.Queue.Add(InkPage(2));
        mfp.Queue.Add(InkPage(3));
        using var vm = NewVm(mfp, source: ScanSource.Feeder);

        await vm.ScanAsync();
        vm.SelectedIndex = 0;
        vm.MoveSelected(1);
        Assert.Equal(new[] { 2, 1, 3 }, vm.SessionPages.Select(ScanTestPages.Tag));

        vm.SelectedIndex = 2;
        vm.RotateSelected(90);
        Assert.Equal(90, vm.SessionPages[2].RotationDegrees);

        vm.DeleteSelected();
        Assert.Equal(new[] { 2, 1 }, vm.SessionPages.Select(ScanTestPages.Tag));

        mfp.Queue.Clear();
        mfp.Queue.Add(InkPage(9));
        vm.SelectedIndex = 0;
        await vm.RescanSelectedAsync();
        Assert.Equal(9, ScanTestPages.Tag(vm.SessionPages[0]));
        Assert.Equal(2, vm.PageCount);
    }

    [Fact]
    public async Task Crop_and_trim_change_page_pixels()
    {
        var rgb = ScanBlankDetectorTests.Solid(80, 80, 250, 250, 250);
        ScanBlankDetectorTests.DrawRect(rgb, 80, 30, 30, 20, 20, 10, 10, 10);
        var scanner = MockScanner.HpEnvy7640();
        scanner.Queue.Add(MockScanner.CreateRasterPage(80, 80, 150, ScanColorMode.Color, rgb));
        using var vm = NewVm(scanner);
        vm.AutoDeskew = false;
        vm.AutoTrim = false;
        vm.DropBlankPages = false;

        await vm.ScanAsync();
        vm.SelectedIndex = 0;
        vm.CropSelected(new ScanCropRect(0.25, 0.25, 0.75, 0.75));
        Assert.Equal(40, vm.SessionPages[0].PixelWidth);
        Assert.Equal(40, vm.SessionPages[0].PixelHeight);

        vm.TrimSelected();
        Assert.True(vm.SessionPages[0].PixelWidth <= 40);
        Assert.True(vm.SessionPages[0].PixelHeight <= 40);
    }

    [Fact]
    public async Task Save_new_and_insert_at_start_assemble_the_pdf()
    {
        var open = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { ScanTestPages.Solid(2550, 3300, 300, tag: 1) }, open);

        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(InkPage(2));
        using var vm = NewVm(mfp, openPdf: open, source: ScanSource.Feeder);
        await vm.ScanAsync();

        var created = TempPdf();
        vm.SaveNew(created);
        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(created, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            Assert.Equal(1, doc.PageCount);

        vm.InsertMode = ScanInsertMode.Start;
        vm.InsertIntoOpenPdf();
        using var updated = PdfSharp.Pdf.IO.PdfReader.Open(open, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(2, updated.PageCount);
        Assert.Equal(open, vm.SavedPath);
    }

    [Fact]
    public async Task Remembers_last_scanner_and_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), "clearpdf-scan-tests", Guid.NewGuid().ToString("N") + ".json");
        var store = new ScanPreferencesStore(path);
        var envy = MockScanner.HpEnvy7640();
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(InkPage(1));
        var provider = new MockScannerProvider(envy, mfp);

        using (var vm = new ScanViewModel(provider, openPdfPath: null, store))
        {
            vm.SelectedDevice = mfp.Device;
            vm.Source = ScanSource.Feeder;
            vm.ColorMode = ScanColorMode.Grayscale;
            vm.Dpi = 200;
            vm.DropBlankPages = false;
            await vm.ScanAsync();
        }

        using var again = new ScanViewModel(provider, openPdfPath: null, store);
        Assert.Equal(mfp.Device.Id, again.SelectedDevice?.Id);
        Assert.Equal(ScanSource.Feeder, again.Source);
        Assert.Equal(ScanColorMode.Grayscale, again.ColorMode);
        Assert.Equal(200, again.Dpi);
        Assert.False(again.DropBlankPages);
    }

    [Fact]
    public async Task Friendly_errors_for_busy_empty_and_cancel()
    {
        var busy = MockScanner.HpEnvy7640();
        busy.IsBusy = true;
        busy.Queue.Add(InkPage(1));
        using (var vm = NewVm(busy))
        {
            await vm.ScanAsync();
            Assert.Contains("busy", vm.Status, StringComparison.OrdinalIgnoreCase);
        }

        var empty = MockScanner.HpColorLaserJetM277dw();
        using (var vm = NewVm(empty, source: ScanSource.Feeder))
        {
            await vm.ScanAsync();
            Assert.Contains("feeder", vm.Status, StringComparison.OrdinalIgnoreCase);
        }

        var jam = MockScanner.HpColorLaserJetM277dw();
        jam.Error = ScannerErrorKind.PaperJam;
        using (var vm = NewVm(jam, source: ScanSource.Feeder))
        {
            await vm.ScanAsync();
            Assert.Contains("jam", vm.Status, StringComparison.OrdinalIgnoreCase);
        }

        var cancel = MockScanner.HpEnvy7640();
        cancel.Queue.Add(InkPage(1));
        using (var vm = NewVm(cancel))
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            vm.CancelScan();
            await vm.ScanAsync();
        }

        using var missing = new ScanViewModel(new MockScannerProvider(), openPdfPath: null,
            new ScanPreferencesStore(TempPrefs()));
        Assert.Contains("scanner", missing.Status, StringComparison.OrdinalIgnoreCase);
        Assert.False(missing.OcrAvailable);
        Assert.Contains("IScanOcr", missing.OcrUnavailableReason);
    }

    [Fact]
    public async Task Dispose_mid_scan_does_not_throw()
    {
        var scanner = MockScanner.HpColorLaserJetM277dw();
        scanner.Queue.Add(InkPage(1));
        scanner.Queue.Add(InkPage(2));
        scanner.PageHold = new TaskCompletionSource<bool>();
        var vm = NewVm(scanner, source: ScanSource.Feeder);

        var scan = vm.ScanAsync();
        await WaitUntil(() => vm.IsScanning);
        vm.Dispose();
        scanner.PageHold.TrySetResult(true);

        var ex = await Record.ExceptionAsync(() => scan);
        Assert.Null(ex);
    }

    [Fact]
    public async Task Cancel_takes_effect_before_the_next_page()
    {
        var scanner = MockScanner.HpColorLaserJetM277dw();
        scanner.Queue.Add(InkPage(1));
        scanner.Queue.Add(InkPage(2));
        scanner.PageHold = new TaskCompletionSource<bool>();
        using var vm = NewVm(scanner, source: ScanSource.Feeder);

        var scan = vm.ScanAsync();
        await WaitUntil(() => vm.IsScanning);
        vm.CancelScan();
        scanner.PageHold.TrySetResult(true);

        await scan;
        Assert.Contains("cancel", vm.Status, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.PageCount <= 1);
    }

    [Fact]
    public async Task Revert_restores_the_original_scan()
    {
        var rgb = ScanBlankDetectorTests.Solid(80, 80, 250, 250, 250);
        ScanBlankDetectorTests.DrawRect(rgb, 80, 10, 10, 60, 60, 10, 10, 10);
        var scanner = MockScanner.HpEnvy7640();
        scanner.Queue.Add(MockScanner.CreateRasterPage(80, 80, 150, ScanColorMode.Color, rgb));
        using var vm = NewVm(scanner);
        vm.AutoDeskew = false;
        vm.AutoTrim = false;

        await vm.ScanAsync();
        vm.SelectedIndex = 0;
        var originalW = vm.SessionPages[0].PixelWidth;
        vm.CropSelected(new ScanCropRect(0.25, 0.25, 0.75, 0.75));
        Assert.True(vm.SessionPages[0].PixelWidth < originalW);

        vm.RevertSelected();
        Assert.Equal(originalW, vm.SessionPages[0].PixelWidth);
        Assert.Equal(0, vm.SessionPages[0].RotationDegrees);
    }

    [Fact]
    public async Task Reload_save_and_insert_can_run_off_the_caller_thread()
    {
        var open = TempPdf();
        ScannedPdfBuilder.SaveNew(new[] { ScanTestPages.Solid(80, 80, 150, tag: 1) }, open);
        var mfp = MockScanner.HpColorLaserJetM277dw();
        mfp.Queue.Add(InkPage(2));
        using var vm = NewVm(mfp, openPdf: open, source: ScanSource.Feeder);
        await vm.ScanAsync();

        var dest = TempPdf();
        await Task.Run(async () =>
        {
            await vm.ReloadDevicesAsync();
            await vm.SaveNewAsync(dest);
            await vm.InsertIntoOpenPdfAsync();
        });
        Assert.NotNull(vm.SelectedDevice);
        using var updated = PdfSharp.Pdf.IO.PdfReader.Open(open, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(2, updated.PageCount);
    }

    [Fact]
    public async Task Hundred_page_run_evicts_rasters_from_memory()
    {
        var mfp = MockScanner.HpColorLaserJetM277dw();
        for (var i = 0; i < 100; i++)
        {
            var rgb = ScanBlankDetectorTests.Solid(120, 160, (byte)(30 + (i % 20)), 40, 80);
            mfp.Queue.Add(MockScanner.CreateRasterPage(120, 160, 150, ScanColorMode.Color, rgb));
        }

        using var vm = NewVm(mfp, source: ScanSource.Feeder);
        await vm.ScanAsync();

        Assert.Equal(100, vm.PageCount);
        Assert.All(vm.SessionPages, p => Assert.Null(p.RasterRgb));
        Assert.All(vm.SessionPages, p => Assert.NotNull(p.ThumbnailBytes));
        Assert.All(vm.SessionPages, p => Assert.True(File.Exists(p.RasterPath)));
    }

    [Fact]
    public async Task Flatbed_one_click_then_add_more_is_a_loop()
    {
        var envy = MockScanner.HpEnvy7640();
        envy.Queue.Add(InkPage(1));
        envy.Queue.Add(InkPage(2));
        using var vm = NewVm(envy);
        await vm.ScanAsync();
        Assert.Equal(1, vm.PageCount);

        envy.Queue.Clear();
        envy.Queue.Add(InkPage(2));
        await vm.ScanAsync(addMore: true);
        Assert.Equal(new[] { 1, 2 }, vm.SessionPages.Select(ScanTestPages.Tag));
    }

    private static ScanViewModel NewVm(
        MockScanner scanner,
        string? openPdf = null,
        ScanSource source = ScanSource.Flatbed)
    {
        var vm = new ScanViewModel(
            new MockScannerProvider(scanner),
            openPdf,
            new ScanPreferencesStore(TempPrefs()));
        vm.SelectedDevice = scanner.Device;
        vm.Source = source;
        vm.AutoDeskew = false;
        vm.AutoTrim = false;
        vm.DropBlankPages = false;
        vm.Dpi = 150;
        return vm;
    }

    private static ScannedPage InkPage(int tag)
    {
        var rgb = ScanBlankDetectorTests.Solid(48, 64, 250, 250, 250);
        ScanBlankDetectorTests.DrawRect(rgb, 48, 8, 10, 32, 40, (byte)tag, 40, 80);
        rgb[0] = (byte)tag;
        rgb[1] = 40;
        rgb[2] = 80;
        return MockScanner.CreateRasterPage(48, 64, 150, ScanColorMode.Color, rgb);
    }

    private static ScannedPage WhitePage() =>
        MockScanner.CreateRasterPage(
            48, 64, 150, ScanColorMode.Color,
            ScanBlankDetectorTests.Solid(48, 64, 252, 252, 252));

    private static string TempPrefs()
    {
        var dir = Path.Combine(Path.GetTempPath(), "clearpdf-scan-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".json");
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        for (var i = 0; i < 50 && !ready(); i++)
            await Task.Delay(10);
        Assert.True(ready());
    }

    private static string TempPdf()
    {
        var dir = Path.Combine(Path.GetTempPath(), "clearpdf-scan-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".pdf");
    }
}
