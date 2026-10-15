using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class ScannerErrorMapperTests
{
    [Theory]
    [InlineData(ScannerErrorMapper.WiaPaperEmpty, ScanSource.Feeder, ScannerErrorKind.FeederEmpty)]
    [InlineData(ScannerErrorMapper.WiaPaperProblem, ScanSource.Feeder, ScannerErrorKind.FeederEmpty)]
    [InlineData(ScannerErrorMapper.WiaPaperEmpty, ScanSource.Flatbed, ScannerErrorKind.NoPaper)]
    [InlineData(ScannerErrorMapper.WiaBusy, ScanSource.Flatbed, ScannerErrorKind.DeviceBusy)]
    [InlineData(ScannerErrorMapper.WiaDeviceLocked, ScanSource.Feeder, ScannerErrorKind.DeviceBusy)]
    [InlineData(ScannerErrorMapper.WiaDeviceCommunication, ScanSource.Flatbed, ScannerErrorKind.Communication)]
    [InlineData(ScannerErrorMapper.WiaOffline, ScanSource.Flatbed, ScannerErrorKind.Communication)]
    [InlineData(ScannerErrorMapper.CancelledHResult, ScanSource.Feeder, ScannerErrorKind.Cancelled)]
    public void FromHResult_maps_wia_codes(int hr, ScanSource source, ScannerErrorKind expected)
    {
        Assert.Equal(expected, ScannerErrorMapper.FromHResult(hr, source));
    }

    [Fact]
    public void Paper_empty_after_pages_is_end_of_feeder_not_fatal()
    {
        Assert.True(ScannerErrorMapper.IsEndOfFeeder(ScannerErrorMapper.WiaPaperEmpty));
        Assert.False(ScannerErrorMapper.IsEndOfFeeder(ScannerErrorMapper.WiaBusy));
    }

    [Fact]
    public void Friendly_messages_exist_for_every_kind()
    {
        foreach (ScannerErrorKind kind in Enum.GetValues<ScannerErrorKind>())
        {
            var message = ScannerException.MessageFor(kind);
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }
}
