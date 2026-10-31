using System.Text.Json;

namespace ClearPDF.Scanning;

public sealed class ScanPreferences
{
    public string? DeviceId { get; set; }
    public ScanSource Source { get; set; } = ScanSource.Flatbed;
    public ScanColorMode ColorMode { get; set; } = ScanColorMode.Color;
    public int Dpi { get; set; } = 300;
    public bool Duplex { get; set; }
    public bool DropBlankPages { get; set; } = true;
    public bool AutoDeskew { get; set; } = true;
    public bool AutoTrim { get; set; } = true;
}

/// <summary>Last-used scanner + settings. Local JSON, no cloud.</summary>
public sealed class ScanPreferencesStore
{
    private readonly string _storePath;

    public ScanPreferencesStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClearPDF");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "scan.json");
    }

    internal ScanPreferencesStore(string storePath) => _storePath = storePath;

    public ScanPreferences Load()
    {
        try
        {
            if (!File.Exists(_storePath))
                return new ScanPreferences();
            var json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<ScanPreferences>(json) ?? new ScanPreferences();
        }
        catch (Exception)
        {
            return new ScanPreferences();
        }
    }

    public void Save(ScanPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var dir = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storePath, json);
    }
}
