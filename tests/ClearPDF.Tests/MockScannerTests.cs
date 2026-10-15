using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class MockScannerTests
{
    [Fact]
    public void ListDevices_returns_registered_scanners()
    {
        var envy = MockScanner.HpEnvy7640();
        var mfp = MockScanner.HpColorLaserJetM277dw();
        var provider = new MockScannerProvider(envy, mfp);

        var names = provider.ListDevices().Select(d => d.Name).ToArray();
        Assert.Equal(new[] { envy.Device.Name, mfp.Device.Name }, names);
        Assert.True(provider.ListDevices()[0].SupportsFlatbed);
        Assert.False(provider.ListDevices()[0].SupportsFeeder);
        Assert.True(provider.ListDevices()[1].SupportsFeeder);
        Assert.True(provider.ListDevices()[1].SupportsDuplex);
    }

    [Fact]
    public void Open_unknown_id_is_device_not_found()
    {
        var provider = new MockScannerProvider(MockScanner.HpEnvy7640());
        var ex = Assert.Throws<ScannerException>(() => provider.Open("missing"));
        Assert.Equal(ScannerErrorKind.DeviceNotFound, ex.Kind);
    }

    [Fact]
    public async Task ScanAsync_returns_queued_pages_and_reports_progress()
    {
        var scanner = MockScanner.HpEnvy7640();
        scanner.Queue.Add(ScanTestPages.Solid(100, 130, 150, tag: 7));
        scanner.Queue.Add(ScanTestPages.Solid(100, 130, 150, tag: 8));
        var pages = await scanner.ScanAsync(
            new ScanSettings { Source = ScanSource.Flatbed, Dpi = 150, ColorMode = ScanColorMode.Color },
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, pages.Count);
        Assert.Equal(new[] { 7, 8 }, pages.Select(ScanTestPages.Tag));
        Assert.Equal(new[] { 1, 2 }, scanner.ProgressReports.Select(r => r.PagesCompleted));
        Assert.Equal(1, scanner.ScanCallCount);
    }

    [Fact]
    public async Task ScanAsync_feeder_empty_when_queue_is_empty()
    {
        var scanner = MockScanner.HpColorLaserJetM277dw();
        var ex = await Assert.ThrowsAsync<ScannerException>(() =>
            scanner.ScanAsync(
                new ScanSettings { Source = ScanSource.Feeder, Dpi = 200 },
                progress: null,
                CancellationToken.None));
        Assert.Equal(ScannerErrorKind.FeederEmpty, ex.Kind);
    }

    [Fact]
    public async Task ScanAsync_flatbed_with_no_pages_is_no_paper()
    {
        var scanner = MockScanner.HpEnvy7640();
        var ex = await Assert.ThrowsAsync<ScannerException>(() =>
            scanner.ScanAsync(
                new ScanSettings { Source = ScanSource.Flatbed, Dpi = 300 },
                progress: null,
                CancellationToken.None));
        Assert.Equal(ScannerErrorKind.NoPaper, ex.Kind);
    }

    [Fact]
    public async Task ScanAsync_device_busy()
    {
        var scanner = MockScanner.HpEnvy7640();
        scanner.IsBusy = true;
        scanner.Queue.Add(ScanTestPages.Solid(10, 10, 150, tag: 1));
        var ex = await Assert.ThrowsAsync<ScannerException>(() =>
            scanner.ScanAsync(new ScanSettings { Dpi = 150 }, null, CancellationToken.None));
        Assert.Equal(ScannerErrorKind.DeviceBusy, ex.Kind);
    }

    [Fact]
    public async Task ScanAsync_honors_cancellation()
    {
        var scanner = MockScanner.HpEnvy7640();
        scanner.Queue.Add(ScanTestPages.Solid(10, 10, 150, tag: 1));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scanner.ScanAsync(new ScanSettings { Dpi = 150 }, null, cts.Token));
    }

    [Fact]
    public async Task ScanMore_appends_to_session()
    {
        var scanner = MockScanner.HpEnvy7640();
        scanner.Queue.Add(ScanTestPages.Solid(20, 28, 200, tag: 1));
        var session = new ScanSession();
        var settings = new ScanSettings { Source = ScanSource.Flatbed, Dpi = 200 };

        session.AddRange(await scanner.ScanAsync(settings, null, CancellationToken.None));
        scanner.Queue.Clear();
        scanner.Queue.Add(ScanTestPages.Solid(20, 28, 200, tag: 2));
        session.AddRange(await scanner.ScanAsync(settings, null, CancellationToken.None));

        Assert.Equal(new[] { 1, 2 }, session.Pages.Select(ScanTestPages.Tag));
        Assert.Equal(2, scanner.ScanCallCount);
    }

    [Fact]
    public async Task ScanAsync_rejects_feeder_on_flatbed_only_device()
    {
        var scanner = MockScanner.HpEnvy7640();
        scanner.Queue.Add(ScanTestPages.Solid(10, 10, 150, tag: 1));
        var ex = await Assert.ThrowsAsync<ScannerException>(() =>
            scanner.ScanAsync(
                new ScanSettings { Source = ScanSource.Feeder, Dpi = 150 },
                null,
                CancellationToken.None));
        Assert.Equal(ScannerErrorKind.Unknown, ex.Kind);
    }
}
