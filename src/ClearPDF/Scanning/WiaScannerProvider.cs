using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClearPDF.Scanning;

namespace ClearPDF.Scanning.Wia;

/// <summary>
/// Windows Image Acquisition 2.0 via the WIA Automation COM layer
/// (<c>WIA.DeviceManager</c> / wiaaut.dll). Chosen over WinRT
/// <c>Windows.Devices.Scanners</c> because WIA 2 is the desktop stack HP ENVY
/// and LaserJet MFPs already expose, reports feeder-empty/busy as stable
/// HRESULTs, and does not need CsWinRT or a Windows 10 SDK retarget.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WiaScannerProvider : IScannerProvider
{
    public IReadOnlyList<ScanDeviceInfo> ListDevices()
    {
        try
        {
            dynamic manager = WiaCom.Create("WIA.DeviceManager");
            var list = new List<ScanDeviceInfo>();
            int count = manager.DeviceInfos.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic info = manager.DeviceInfos[i];
                if (Convert.ToInt32(info.Type) != WiaCom.DeviceTypeScanner)
                    continue;
                list.Add(ReadDevice(info));
            }

            return list;
        }
        catch (Exception ex)
        {
            throw WiaCom.Wrap(ex, ScanSource.Flatbed);
        }
    }

    public IScanner Open(string deviceId)
    {
        try
        {
            dynamic manager = WiaCom.Create("WIA.DeviceManager");
            int count = manager.DeviceInfos.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic info = manager.DeviceInfos[i];
                string id = Convert.ToString(info.DeviceID) ?? string.Empty;
                if (string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase))
                    return new WiaScanner(info, ReadDevice(info));
            }

            throw new ScannerException(ScannerErrorKind.DeviceNotFound);
        }
        catch (ScannerException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw WiaCom.Wrap(ex, ScanSource.Flatbed);
        }
    }

    private static ScanDeviceInfo ReadDevice(dynamic info)
    {
        var id = Convert.ToString(info.DeviceID) ?? "wia";
        var name = WiaCom.TryGetPropertyString(info.Properties, "Name") ?? id;
        var supportsFlatbed = true;
        var supportsFeeder = false;
        var supportsDuplex = false;
        try
        {
            dynamic device = info.Connect();
            try
            {
                var caps = WiaCom.TryGetPropertyInt(device.Properties, WiaCom.DocumentHandlingCapabilities);
                if (caps is int c)
                {
                    supportsFeeder = (c & WiaCom.Feed) != 0;
                    supportsFlatbed = (c & WiaCom.Flat) != 0 || !supportsFeeder;
                    supportsDuplex = (c & WiaCom.Duplex) != 0;
                }

                if (WiaCom.FindItem(device, ScanSource.Feeder) != null)
                    supportsFeeder = true;
                if (WiaCom.FindItem(device, ScanSource.Flatbed) != null)
                    supportsFlatbed = true;
            }
            finally
            {
                WiaCom.Release(device);
            }
        }
        catch (Exception)
        {
            // Listing must still succeed when a device is busy/offline.
        }

        return new ScanDeviceInfo
        {
            Id = id,
            Name = name,
            SupportsFlatbed = supportsFlatbed,
            SupportsFeeder = supportsFeeder,
            SupportsDuplex = supportsDuplex,
            DpiOptions = ScanDefaults.DpiOptions,
            ColorModes = ScanDefaults.ColorModes
        };
    }
}

[SupportedOSPlatform("windows")]
internal sealed class WiaScanner : IScanner
{
    private readonly dynamic _info;

    public WiaScanner(dynamic info, ScanDeviceInfo device)
    {
        _info = info;
        Device = device;
    }

    public ScanDeviceInfo Device { get; }

    public async Task<IReadOnlyList<ScannedPage>> ScanAsync(
        ScanSettings settings,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (settings.Source == ScanSource.Feeder && !Device.SupportsFeeder)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner has no document feeder.");
        if (settings.Source == ScanSource.Flatbed && !Device.SupportsFlatbed)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner has no flatbed.");

        var pages = new List<ScannedPage>();
        dynamic? device = null;
        try
        {
            device = _info.Connect();
            ApplyDeviceHandling(device, settings);
            var item = WiaCom.FindItem(device, settings.Source) ?? WiaCom.FirstItem(device);
            if (item == null)
                throw new ScannerException(ScannerErrorKind.Unknown, "The scanner has no scan source.");

            ApplyItemSettings(item, settings);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var page = TransferPage(item, settings);
                    pages.Add(page);
                    progress?.Report(new ScanProgress(pages.Count, null, $"Scanned page {pages.Count}"));
                    if (settings.Source == ScanSource.Flatbed)
                        break;
                    await Task.Yield();
                }
                catch (Exception ex)
                {
                    var unwrapped = WiaCom.Unwrap(ex);
                    if (unwrapped is OperationCanceledException)
                        throw;
                    if (unwrapped is COMException com && ScannerErrorMapper.IsEndOfFeeder(com.ErrorCode))
                    {
                        if (pages.Count == 0)
                            throw new ScannerException(ScannerErrorKind.FeederEmpty, inner: com);
                        break;
                    }

                    throw WiaCom.Wrap(ex, settings.Source);
                }
            }
        }
        catch (ScannerException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw WiaCom.Wrap(ex, settings.Source);
        }
        finally
        {
            WiaCom.Release(device);
        }

        if (pages.Count == 0)
        {
            throw new ScannerException(settings.Source == ScanSource.Feeder
                ? ScannerErrorKind.FeederEmpty
                : ScannerErrorKind.NoPaper);
        }

        return pages;
    }

    private static void ApplyDeviceHandling(dynamic device, ScanSettings settings)
    {
        var select = settings.Source == ScanSource.Feeder ? WiaCom.Feed : WiaCom.Flat;
        if (settings.Duplex && settings.Source == ScanSource.Feeder)
            select |= WiaCom.Duplex;
        WiaCom.TrySetProperty(device.Properties, WiaCom.DocumentHandlingSelect, select);
        if (settings.Source == ScanSource.Feeder)
            WiaCom.TrySetProperty(device.Properties, WiaCom.Pages, 0);
    }

    private static void ApplyItemSettings(dynamic item, ScanSettings settings)
    {
        var intent = settings.ColorMode switch
        {
            ScanColorMode.Color => WiaCom.IntentColor,
            ScanColorMode.Grayscale => WiaCom.IntentGray,
            ScanColorMode.BlackAndWhite => WiaCom.IntentText,
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.ColorMode, null)
        };
        WiaCom.TrySetProperty(item.Properties, WiaCom.CurrentIntent, intent);
        WiaCom.TrySetProperty(item.Properties, WiaCom.HorizontalResolution, settings.Dpi);
        WiaCom.TrySetProperty(item.Properties, WiaCom.VerticalResolution, settings.Dpi);
        WiaCom.TrySetProperty(item.Properties, WiaCom.Format, WiaCom.JpegFormat);
    }

    private static ScannedPage TransferPage(dynamic item, ScanSettings settings)
    {
        dynamic imageFile;
        try
        {
            imageFile = item.Transfer(WiaCom.JpegFormat);
        }
        catch (Exception)
        {
            imageFile = item.Transfer();
        }

        byte[] bytes = WiaCom.ReadBinary(imageFile);
        if (bytes.Length == 0)
            throw new ScannerException(ScannerErrorKind.Unknown, "The scanner returned an empty page.");

        if (!ScanImageHeader.TryRead(bytes, out var width, out var height, out var format))
        {
            format = ScanImageFormat.Jpeg;
            width = Math.Max(1, settings.Dpi * 8);
            height = Math.Max(1, settings.Dpi * 11);
        }

        return new ScannedPage
        {
            ImageBytes = bytes,
            Format = format,
            PixelWidth = width,
            PixelHeight = height,
            Dpi = Math.Max(1, settings.Dpi),
            ColorMode = settings.ColorMode
        };
    }
}

[SupportedOSPlatform("windows")]
internal static class WiaCom
{
    public const int DeviceTypeScanner = 1;
    public const int DocumentHandlingCapabilities = 3086;
    public const int DocumentHandlingSelect = 3088;
    public const int Pages = 3096;
    public const int ItemCategory = 4125;
    public const int CurrentIntent = 6146;
    public const int HorizontalResolution = 6147;
    public const int VerticalResolution = 6148;
    public const int Format = 4106;
    public const int Feed = 0x01;
    public const int Flat = 0x02;
    public const int Duplex = 0x04;
    public const int IntentColor = 0x00000001;
    public const int IntentGray = 0x00000002;
    public const int IntentText = 0x00000004;
    public const string JpegFormat = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
    public const string CategoryFlatbed = "FB607B19-43F3-488B-855B-FB703EC342A6";
    public const string CategoryFeeder = "FE131934-24C6-4BB6-8C0D-378D5F8582B4";

    public static object Create(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: true)
                   ?? throw new ScannerException(ScannerErrorKind.Communication, "WIA is not installed.");
        return Activator.CreateInstance(type)
               ?? throw new ScannerException(ScannerErrorKind.Communication, "Could not start WIA.");
    }

    public static Exception Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: { } inner })
            ex = inner;
        return ex;
    }

    public static ScannerException Wrap(Exception ex, ScanSource source)
    {
        var inner = Unwrap(ex);
        if (inner is ScannerException scanner)
            return scanner;
        if (inner is OperationCanceledException)
            return new ScannerException(ScannerErrorKind.Cancelled, inner: inner);
        if (inner is COMException com)
            return new ScannerException(ScannerErrorMapper.FromHResult(com.ErrorCode, source), inner.Message, com);
        return new ScannerException(ScannerErrorKind.Unknown, inner.Message, inner);
    }

    public static void Release(object? com)
    {
        if (com == null)
            return;
        try
        {
            if (Marshal.IsComObject(com))
                Marshal.FinalReleaseComObject(com);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    public static dynamic? FirstItem(dynamic device)
    {
        try
        {
            if (device.Items.Count >= 1)
                return device.Items[1];
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public static dynamic? FindItem(dynamic device, ScanSource source)
    {
        var wanted = source == ScanSource.Feeder ? CategoryFeeder : CategoryFlatbed;
        return FindByCategory(device.Items, wanted);
    }

    private static dynamic? FindByCategory(dynamic items, string category)
    {
        int count;
        try
        {
            count = items.Count;
        }
        catch (Exception)
        {
            return null;
        }

        for (var i = 1; i <= count; i++)
        {
            dynamic item;
            try
            {
                item = items[i];
            }
            catch (Exception)
            {
                continue;
            }

            var cat = TryGetPropertyString(item.Properties, ItemCategory)
                      ?? TryGetPropertyString(item.Properties, "Item Category");
            if (GuidEquals(cat, category))
                return item;

            try
            {
                var child = FindByCategory(item.Items, category);
                if (child != null)
                    return child;
            }
            catch (Exception)
            {
                // leaf item
            }
        }

        return null;
    }

    private static bool GuidEquals(string? value, string guid)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var a = value.Trim().Trim('{', '}');
        return string.Equals(a, guid, StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryGetPropertyString(dynamic properties, object id)
    {
        try
        {
            var v = properties.Item(id).Value;
            return v?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static int? TryGetPropertyInt(dynamic properties, object id)
    {
        try
        {
            return Convert.ToInt32(properties.Item(id).Value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void TrySetProperty(dynamic properties, object id, object value)
    {
        try
        {
            properties.Item(id).Value = value;
        }
        catch (Exception)
        {
            // Property not supported on this driver — keep going.
        }
    }

    public static byte[] ReadBinary(dynamic imageFile)
    {
        try
        {
            var data = imageFile.FileData.BinaryData;
            if (data is byte[] bytes)
                return bytes;
            if (data is Array arr)
            {
                var copy = new byte[arr.Length];
                arr.CopyTo(copy, 0);
                return copy;
            }
        }
        catch (Exception)
        {
            return Array.Empty<byte>();
        }

        return Array.Empty<byte>();
    }
}
