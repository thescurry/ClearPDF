using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScanPreferencesTests
{
    [Fact]
    public void Save_then_load_round_trips_last_used_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), "clearpdf-scan-tests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var store = new ScanPreferencesStore(path);

        store.Save(new ScanPreferences
        {
            DeviceId = "mock-hp-m277dw",
            Source = ScanSource.Feeder,
            ColorMode = ScanColorMode.Grayscale,
            Dpi = 200,
            Duplex = true,
            DropBlankPages = false,
            AutoDeskew = false,
            AutoTrim = true
        });

        var loaded = store.Load();
        Assert.Equal("mock-hp-m277dw", loaded.DeviceId);
        Assert.Equal(ScanSource.Feeder, loaded.Source);
        Assert.Equal(ScanColorMode.Grayscale, loaded.ColorMode);
        Assert.Equal(200, loaded.Dpi);
        Assert.True(loaded.Duplex);
        Assert.False(loaded.DropBlankPages);
        Assert.False(loaded.AutoDeskew);
        Assert.True(loaded.AutoTrim);
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "clearpdf-scan-tests", Guid.NewGuid().ToString("N") + ".json");
        var loaded = new ScanPreferencesStore(path).Load();
        Assert.Null(loaded.DeviceId);
        Assert.Equal(300, loaded.Dpi);
        Assert.True(loaded.DropBlankPages);
        Assert.True(loaded.AutoDeskew);
    }
}
