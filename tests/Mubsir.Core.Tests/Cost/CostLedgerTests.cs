using Mubsir.Core.Common;
using Mubsir.Core.Cost;
using Mubsir.Core.Localization;
using Mubsir.Core.Router;

namespace Mubsir.Core.Tests.Cost;

public class CostLedgerTests
{
    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset now)
        {
            UtcNow = now;
        }

        public long NowMs => UtcNow.ToUnixTimeMilliseconds();

        public DateTimeOffset UtcNow { get; set; }
    }

    [Fact]
    public void FishVoiceCost_CalculatesFifteenDollarsPerMillionBytes()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 10m);

        decimal cost1M = ledger.AddFishVoice(1_000_000);
        Assert.Equal(15.00m, cost1M);
        Assert.Equal(15.00m, ledger.MonthToDateUsd);

        decimal cost100K = ledger.AddFishVoice(100_000);
        Assert.Equal(1.50m, cost100K);
        Assert.Equal(16.50m, ledger.MonthToDateUsd);
    }

    [Fact]
    public void FishVoiceCost_ZeroOrNegativeBytes_CostsZero()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 10m);

        Assert.Equal(0m, ledger.AddFishVoice(0));
        Assert.Equal(0m, ledger.AddFishVoice(-50));
        Assert.Equal(0m, ledger.MonthToDateUsd);
    }

    [Fact]
    public void FishHearingCost_CalculatesThirtySixCentsPerHour()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 10m);

        decimal cost1Hour = ledger.AddFishHearing(TimeSpan.FromHours(1));
        Assert.Equal(0.36m, cost1Hour);
        Assert.Equal(0.36m, ledger.MonthToDateUsd);

        decimal cost30Mins = ledger.AddFishHearing(TimeSpan.FromMinutes(30));
        Assert.Equal(0.18m, cost30Mins);
        Assert.Equal(0.54m, ledger.MonthToDateUsd);
    }

    [Fact]
    public void FishCosts_TriggerBudgetWarningAtEightyPercent()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 10m);

        Assert.Equal(BudgetState.Ok, ledger.State);
        Assert.False(ledger.TryClaimWarningAnnouncement());

        // Add 500,000 bytes = $7.50, which is 75% (< 80%)
        ledger.AddFishVoice(500_000);
        Assert.Equal(BudgetState.Ok, ledger.State);
        Assert.False(ledger.TryClaimWarningAnnouncement());

        // Add 50,000 bytes = $0.75, total = $8.25 (> 80% of $10)
        ledger.AddFishVoice(50_000);
        Assert.Equal(BudgetState.Warning, ledger.State);
        Assert.True(ledger.TryClaimWarningAnnouncement());

        // Cannot claim twice
        Assert.False(ledger.TryClaimWarningAnnouncement());
    }

    [Fact]
    public void FishCosts_TriggerBudgetReachedAtLimit()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 5m);

        // Add ~400,000 bytes = $6.00 (> $5 soft cap)
        ledger.AddFishVoice(400_000);
        Assert.Equal(BudgetState.Reached, ledger.State);
    }

    [Fact]
    public void SpendingByService_GroupsModelsCorrectly()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 20m);

        // Add Claude usage
        ledger.Add(new UsageRecord(clock.UtcNow, "claude-haiku-5-5", 10_000, 0, 0, 1_000));
        // Add Fish Voice
        ledger.AddFishVoice(100_000);
        // Add Fish Hearing
        ledger.AddFishHearing(TimeSpan.FromMinutes(10));

        var breakdown = ledger.GetSpendingByService();
        Assert.True(breakdown.ContainsKey("Claude"));
        Assert.True(breakdown.ContainsKey("Fish voice"));
        Assert.True(breakdown.ContainsKey("Fish hearing"));

        Assert.True(breakdown["Claude"] > 0m);
        Assert.Equal(1.50m, breakdown["Fish voice"]);
        Assert.Equal(0.06m, breakdown["Fish hearing"]);
    }

    [Fact]
    public void ServiceDisplayName_ReturnsBilingualNames()
    {
        var (claudeEn, claudeAr) = CostLedger.ServiceDisplayName("Claude");
        Assert.Equal("Claude", claudeEn);
        Assert.Equal("كلود", claudeAr);

        var (voiceEn, voiceAr) = CostLedger.ServiceDisplayName("Fish voice");
        Assert.Equal("Fish voice", voiceEn);
        Assert.Equal("صوت فيش", voiceAr);

        var (hearingEn, hearingAr) = CostLedger.ServiceDisplayName("Fish hearing");
        Assert.Equal("Fish hearing", hearingEn);
        Assert.Equal("سمع فيش", hearingAr);
    }

    [Fact]
    public void Serialization_RoundTripsFishCosts()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 15m);
        ledger.AddFishVoice(200_000); // $3.00
        ledger.AddFishHearing(TimeSpan.FromHours(2)); // $0.72

        string json = ledger.Serialize();
        Assert.Contains("fish-voice", json);
        Assert.Contains("fish-hearing", json);

        var restored = CostLedger.Deserialize(json, clock);
        Assert.Equal(3.72m, restored.MonthToDateUsd);
        Assert.Equal(3.00m, restored.ByModel["fish-voice"]);
        Assert.Equal(0.72m, restored.ByModel["fish-hearing"]);
    }

    [Theory]
    [InlineData("صرفت كام")]
    [InlineData("كم صرفت")]
    [InlineData("صرفت قد ايه")]
    [InlineData("صرفت كام الشهر ده")]
    [InlineData("how much have I spent")]
    [InlineData("how much did I spend")]
    [InlineData("spending")]
    [InlineData("my spending")]
    [InlineData("how much صرفت")]
    [InlineData("صرفت how much")]
    public void SpendingCommand_MatchesPhrases(string phrase)
    {
        CommandMatch? match = CommandGrammar.Match(phrase);
        Assert.NotNull(match);
        Assert.Equal(LocalCommand.Spending, match.Command);
    }

    [Fact]
    public void Strings_SpendingFormatting()
    {
        string zeroAr = Strings.Get(StringKey.SpendingZero, Lang.Arabic, 8.00m);
        Assert.Contains("8", zeroAr);
        Assert.Contains("لم تصرف", zeroAr);

        string zeroEn = Strings.Get(StringKey.SpendingZero, Lang.English, 8.00m);
        Assert.Contains("8", zeroEn);
        Assert.Contains("haven't spent anything", zeroEn);

        string breakdownAr = Strings.Get(StringKey.SpendingBreakdown, Lang.Arabic, "كلود 1 دولار", 1.00m, 8.00m);
        Assert.Contains("كلود 1 دولار", breakdownAr);
        Assert.Contains("الإجمالي", breakdownAr);

        string breakdownEn = Strings.Get(StringKey.SpendingBreakdown, Lang.English, "Claude 1 dollar", 1.00m, 8.00m);
        Assert.Contains("Claude 1 dollar", breakdownEn);
        Assert.Contains("Total", breakdownEn);
    }
}
