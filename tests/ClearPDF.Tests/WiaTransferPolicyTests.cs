using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class WiaTransferPolicyTests
{
    [Fact]
    public void Format_errors_are_the_only_retry()
    {
        Assert.True(WiaTransferPolicy.IsFormatRetryable(ScannerErrorMapper.WiaInvalidCommand));
        Assert.True(WiaTransferPolicy.IsFormatRetryable(WiaTransferPolicy.ENotImpl));
        Assert.Equal(WiaTransferAction.RetryFormat,
            WiaTransferPolicy.Decide(ScannerErrorMapper.WiaInvalidCommand, ScanSource.Feeder, 1).Action);
    }

    [Theory]
    [InlineData(ScannerErrorMapper.WiaPaperJam, ScannerErrorKind.PaperJam)]
    [InlineData(ScannerErrorMapper.WiaBusy, ScannerErrorKind.DeviceBusy)]
    [InlineData(ScannerErrorMapper.WiaCoverOpen, ScannerErrorKind.CoverOpen)]
    [InlineData(ScannerErrorMapper.WiaDeviceCommunication, ScannerErrorKind.Communication)]
    [InlineData(ScannerErrorMapper.WiaOffline, ScannerErrorKind.Communication)]
    [InlineData(ScannerErrorMapper.WiaPaperProblem, ScannerErrorKind.Misfeed)]
    public void Device_faults_fail_without_retry_or_end_of_feeder(int hr, ScannerErrorKind kind)
    {
        var decision = WiaTransferPolicy.Decide(hr, ScanSource.Feeder, pagesCompleted: 3);
        Assert.Equal(WiaTransferAction.Fail, decision.Action);
        Assert.Equal(kind, decision.Kind);
        Assert.False(WiaTransferPolicy.IsFormatRetryable(hr));
        Assert.True(WiaTransferPolicy.ShouldAbortJob(kind));
        Assert.False(ScannerErrorMapper.IsEndOfFeeder(hr));
    }

    [Fact]
    public void Paper_empty_after_pages_is_end_of_feeder()
    {
        var decision = WiaTransferPolicy.Decide(
            ScannerErrorMapper.WiaPaperEmpty, ScanSource.Feeder, pagesCompleted: 2);
        Assert.Equal(WiaTransferAction.EndOfFeeder, decision.Action);
    }

    [Fact]
    public void Paper_empty_before_any_page_is_an_error()
    {
        var decision = WiaTransferPolicy.Decide(
            ScannerErrorMapper.WiaPaperEmpty, ScanSource.Feeder, pagesCompleted: 0);
        Assert.Equal(WiaTransferAction.Fail, decision.Action);
        Assert.Equal(ScannerErrorKind.FeederEmpty, decision.Kind);
    }

    [Fact]
    public void Misfeed_is_not_a_successful_end()
    {
        var decision = WiaTransferPolicy.Decide(
            ScannerErrorMapper.WiaPaperProblem, ScanSource.Feeder, pagesCompleted: 2);
        Assert.Equal(WiaTransferAction.Fail, decision.Action);
        Assert.Equal(ScannerErrorKind.Misfeed, decision.Kind);
        Assert.Contains("misfeed", ScannerException.MessageFor(ScannerErrorKind.Misfeed),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cover_open_has_a_plain_language_message()
    {
        Assert.Equal(ScannerErrorKind.CoverOpen,
            ScannerErrorMapper.FromHResult(ScannerErrorMapper.WiaCoverOpen, ScanSource.Flatbed));
        Assert.Contains("cover", ScannerException.MessageFor(ScannerErrorKind.CoverOpen),
            StringComparison.OrdinalIgnoreCase);
    }
}
