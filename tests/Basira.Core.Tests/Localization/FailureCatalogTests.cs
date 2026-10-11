using Basira.Core.Common;
using Basira.Core.Localization;

namespace Basira.Core.Tests.Localization;

public class FailureCatalogTests
{
    [Fact]
    public void EveryFailureStateIsDescribed_WithSpeakableText()
    {
        foreach (FailureState s in Enum.GetValues<FailureState>())
        {
            FailureInfo info = FailureCatalog.Describe(s);

            Assert.True(Enum.IsDefined(info.Earcon));
            Assert.True(Strings.Has(info.Message));
            Assert.False(string.IsNullOrWhiteSpace(Strings.Get(info.Message, Lang.Arabic)));
            if (info.Recovery is StringKey recovery)
            {
                Assert.True(Strings.Has(recovery));
                Assert.NotEqual(info.Message, recovery);
            }
        }
    }

    [Theory]
    [InlineData(FailureState.Offline, StringKey.NoInternet)]
    [InlineData(FailureState.ApiError, StringKey.AiBusyRetrying)]
    [InlineData(FailureState.BudgetReached, StringKey.BudgetReached)]
    [InlineData(FailureState.MicSilent, StringKey.MicSilent)]
    [InlineData(FailureState.Crashed, StringKey.CrashRecovered)]
    [InlineData(FailureState.NvdaStopped, StringKey.NvdaStopped)]
    [InlineData(FailureState.Paused, StringKey.PausedPressEnter)]
    [InlineData(FailureState.BadUpdate, StringKey.UpdateRolledBack)]
    [InlineData(FailureState.NoApiKey, StringKey.NoApiKey)]
    public void Messages(FailureState state, StringKey message)
    {
        Assert.Equal(message, FailureCatalog.Describe(state).Message);
    }

    [Fact]
    public void EarconsAndRecoveries()
    {
        Assert.Equal(Earcon.Paused, FailureCatalog.Describe(FailureState.Paused).Earcon);
        Assert.Equal(Earcon.Ready, FailureCatalog.Describe(FailureState.Crashed).Earcon);
        Assert.Equal(Earcon.Error, FailureCatalog.Describe(FailureState.Offline).Earcon);
        Assert.Equal(StringKey.LocalStillWorks, FailureCatalog.Describe(FailureState.Offline).Recovery);
        Assert.Equal(StringKey.TypeRequestHint, FailureCatalog.Describe(FailureState.MicSilent).Recovery);
        Assert.Equal(StringKey.NvdaRestartHint, FailureCatalog.Describe(FailureState.NvdaStopped).Recovery);
    }

    [Fact]
    public void UnknownStateThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FailureCatalog.Describe((FailureState)99));
    }
}
