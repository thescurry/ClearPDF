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
        object? manager = null;
        try
        {
            manager = WiaCom.Create("WIA.DeviceManager");
            dynamic dyn = manager;
            var list = new List<ScanDeviceInfo>();
            int count = dyn.DeviceInfos.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic info = dyn.DeviceInfos[i];
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
        finally
        {
            WiaCom.Release(manager);
        }
    }

    public IScanner Open(string deviceId)
    {
        object? manager = null;
        try
        {
            manager = WiaCom.Create("WIA.DeviceManager");
            dynamic dyn = manager;
            int count = dyn.DeviceInfos.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic info = dyn.DeviceInfos[i];
                string id = Convert.ToString(info.DeviceID) ?? string.Empty;
                if (string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    var device = ReadDevice(info);
                    WiaCom.Release(manager);
                    manager = null;
                    return new WiaScanner(device);
                }
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
        finally
        {
            WiaCom.Release(manager);
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
internal sealed class WiaScanner : IScanner, IDisposable
{
    public WiaScanner(ScanDeviceInfo device) => Device = device;

    public ScanDeviceInfo Device { get; }

    public void Dispose()
    {
        // COM lives only on the STA worker for the duration of ScanAsync.
    }

    public Task<IReadOnlyList<ScannedPage>> ScanAsync(
        ScanSettings settings,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (settings.Source == ScanSource.Feeder && !Device.SupportsFeeder)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner has no document feeder.");
        if (settings.Source == ScanSource.Flatbed && !Device.SupportsFlatbed)
            throw new ScannerException(ScannerErrorKind.Unknown, "This scanner has no flatbed.");

        return WiaSta.Run(() => ScanOnSta(settings, progress, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<ScannedPage> ScanOnSta(
        ScanSettings settings,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var pages = new List<ScannedPage>();
        object? manager = null;
        dynamic? device = null;
        try
        {
            manager = WiaCom.Create("WIA.DeviceManager");
            device = ConnectById(manager, Device.Id);
            ApplyDeviceHandling(device, settings);
            var item = WiaCom.FindItem(device, settings.Source) ?? WiaCom.FirstItem(device);
            if (item == null)
                throw new ScannerException(ScannerErrorKind.Unknown, "The scanner has no scan source.");

            ApplyItemSettings(item, settings);
            ApplyAutoPageSize(item, settings);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var page = TransferPage(item, settings, pages.Count, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    pages.Add(page);
                    progress?.Report(new ScanProgress(
                        pages.Count, null, $"Scanned page {pages.Count}", page));
                    if (settings.Source == ScanSource.Flatbed)
                        break;
                    if (settings.MaxPages is > 0 && pages.Count >= settings.MaxPages.Value)
                        break;
                }
                catch (Exception ex)
                {
                    var unwrapped = WiaCom.Unwrap(ex);
                    if (unwrapped is OperationCanceledException)
                        throw;
                    if (unwrapped is ScannerException scanner &&
                        (scanner.Kind is ScannerErrorKind.FeederEmpty or ScannerErrorKind.NoPaper) &&
                        pages.Count > 0)
                        break;
                    if (unwrapped is COMException com)
                    {
                        var decision = WiaTransferPolicy.Decide(com.ErrorCode, settings.Source, pages.Count);
                        if (decision.Action == WiaTransferAction.EndOfFeeder)
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
            WiaCom.Release(manager);
        }

        if (pages.Count == 0)
        {
            throw new ScannerException(settings.Source == ScanSource.Feeder
                ? ScannerErrorKind.FeederEmpty
                : ScannerErrorKind.NoPaper);
        }

        return pages;
    }

    private static dynamic ConnectById(object manager, string deviceId)
    {
        dynamic dyn = manager;
        int count = dyn.DeviceInfos.Count;
        for (var i = 1; i <= count; i++)
        {
            dynamic info = dyn.DeviceInfos[i];
            string id = Convert.ToString(info.DeviceID) ?? string.Empty;
            if (string.Equals(id, deviceId, StringComparison.OrdinalIgnoreCase))
                return info.Connect();
        }

        throw new ScannerException(ScannerErrorKind.DeviceNotFound);
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
        WiaCom.TrySetProperty(item.Properties, WiaCom.Format, WiaCom.BmpFormat);
    }

    private static void ApplyAutoPageSize(dynamic item, ScanSettings settings)
    {
        WiaCom.TrySetProperty(item.Properties, WiaCom.PageSize, WiaCom.PageAuto);
        WiaCom.TrySetProperty(item.Properties, WiaCom.HorizontalStartPosition, 0);
        WiaCom.TrySetProperty(item.Properties, WiaCom.VerticalStartPosition, 0);

        var dpi = Math.Max(1, settings.Dpi);
        var maxW = WiaCom.TryGetPropertyInt(item.Properties, WiaCom.HorizontalExtent);
        var maxH = WiaCom.TryGetPropertyInt(item.Properties, WiaCom.VerticalExtent);
        var bedW = WiaCom.TryGetPropertyInt(item.Properties, WiaCom.HorizontalBedSize);
        var bedH = WiaCom.TryGetPropertyInt(item.Properties, WiaCom.VerticalBedSize);
        var extentW = bedW is > 0 ? Math.Max(1, (int)Math.Round(bedW.Value / 1000.0 * dpi)) : maxW;
        var extentH = bedH is > 0 ? Math.Max(1, (int)Math.Round(bedH.Value / 1000.0 * dpi)) : maxH;
        if (maxW is > 0 && extentW is > 0)
            extentW = Math.Min(extentW.Value, maxW.Value);
        if (maxH is > 0 && extentH is > 0)
            extentH = Math.Min(extentH.Value, maxH.Value);
        if (extentW is > 0)
            WiaCom.TrySetProperty(item.Properties, WiaCom.HorizontalExtent, extentW.Value);
        if (extentH is > 0)
            WiaCom.TrySetProperty(item.Properties, WiaCom.VerticalExtent, extentH.Value);
    }

    private static ScannedPage TransferPage(
        dynamic item, ScanSettings settings, int pagesCompleted, CancellationToken cancellationToken)
    {
        object? imageFile = null;
        try
        {
            string?[] formats = { WiaCom.BmpFormat, WiaCom.JpegFormat, null };
            Exception? last = null;
            for (var i = 0; i < formats.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    imageFile = formats[i] == null ? item.Transfer() : item.Transfer(formats[i]);
                    last = null;
                    break;
                }
                catch (Exception ex)
                {
                    last = ex;
                    var hr = WiaCom.HResultOf(ex);
                    var decision = WiaTransferPolicy.Decide(hr, settings.Source, pagesCompleted);
                    if (decision.Action == WiaTransferAction.RetryFormat && i < formats.Length - 1)
                        continue;
                    if (decision.Action == WiaTransferAction.EndOfFeeder)
                        throw new ScannerException(
                            settings.Source == ScanSource.Feeder
                                ? ScannerErrorKind.FeederEmpty
                                : ScannerErrorKind.NoPaper,
                            inner: WiaCom.Unwrap(ex));
                    throw WiaCom.Wrap(ex, settings.Source);
                }
            }

            if (imageFile == null)
            {
                throw last != null
                    ? WiaCom.Wrap(last, settings.Source)
                    : new ScannerException(ScannerErrorKind.Unknown, "The scanner returned no image.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = WiaCom.ReadBinary(imageFile);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length == 0)
                throw new ScannerException(ScannerErrorKind.Unknown, "The scanner returned an empty page.");

            byte[]? rgb = null;
            var width = 0;
            var height = 0;
            var format = ScanImageFormat.Jpeg;
            if (ScanBmp.TryDecodeRgb(bytes, out width, out height, out rgb))
            {
                format = ScanImageFormat.Bmp;
            }
            else if (!ScanImageHeader.TryRead(bytes, out width, out height, out format))
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
                ColorMode = settings.ColorMode,
                RasterRgb = rgb,
                RasterWidth = rgb == null ? 0 : width,
                RasterHeight = rgb == null ? 0 : height
            };
        }
        finally
        {
            WiaCom.Release(imageFile);
        }
    }
}

/// <summary>
/// WIA Automation is STA COM. A thread-pool MTA call freezes or fails; a
/// dedicated STA worker keeps the WPF UI moving while a page transfers.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WiaSta
{
    public static Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                tcs.TrySetResult(work());
            }
            catch (OperationCanceledException ex)
            {
                tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "ClearPDF-WIA";
        thread.Start();
        return tcs.Task;
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
    public const int PageSize = 3097;
    public const int PageAuto = 100;
    public const int HorizontalStartPosition = 6149;
    public const int VerticalStartPosition = 6150;
    public const int HorizontalExtent = 6151;
    public const int VerticalExtent = 6152;
    public const int HorizontalBedSize = 3074;
    public const int VerticalBedSize = 3075;
    public const int Feed = 0x01;
    public const int Flat = 0x02;
    public const int Duplex = 0x04;
    public const int IntentColor = 0x00000001;
    public const int IntentGray = 0x00000002;
    public const int IntentText = 0x00000004;
    public const string JpegFormat = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
    public const string BmpFormat = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
    public const string CategoryFlatbed = "FB607B19-43F3-488B-855B-FB703EC342A6";
    public const string CategoryFeeder = "FE131934-24C6-4BB6-8C0D-378D5F8582B4";

    public static object Create(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: true)
                   ?? throw new ScannerException(ScannerErrorKind.Communication, "WIA is not installed.");
        return Activator.CreateInstance(type)
               ?? throw new ScannerException(ScannerErrorKind.Communication, "Could not start WIA.");
    }

    public static int HResultOf(Exception ex) =>
        Unwrap(ex) is COMException com ? com.ErrorCode : 0;

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
