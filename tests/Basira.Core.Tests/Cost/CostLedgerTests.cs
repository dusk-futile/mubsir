using Basira.Core.Common;
using Basira.Core.Cost;

namespace Basira.Core.Tests.Cost;

public class CostLedgerTests
{
    private static readonly DateTimeOffset Oct10 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static UsageRecord Usage(string model, long input = 0, long cacheRead = 0, long cacheWrite = 0, long output = 0, long audio = 0, int searches = 0) =>
        new(Oct10, model, input, cacheRead, cacheWrite, output, audio, searches);

    [Fact]
    public void DefaultPrices()
    {
        ModelPrice haiku = PriceTable.Defaults["claude-haiku-5-5"];
        Assert.Equal(0.10m, haiku.InputPerMTok);
        Assert.Equal(0.01m, haiku.CacheReadPerMTok);
        Assert.Equal(0.125m, haiku.CacheWritePerMTok);
        Assert.Equal(0.50m, haiku.OutputPerMTok);
        Assert.Equal(100_000, haiku.LongPromptThresholdTokens);
        Assert.Equal(5m, haiku.LongPromptMultiplier);

        ModelPrice sonnet = PriceTable.Defaults["claude-sonnet-5-5"];
        Assert.Equal((2.00m, 0.20m, 2.50m, 10.00m), (sonnet.InputPerMTok, sonnet.CacheReadPerMTok, sonnet.CacheWritePerMTok, sonnet.OutputPerMTok));

        ModelPrice gemini = PriceTable.Defaults["gemini-3.5-flash-lite"];
        Assert.Equal((0.30m, 0.03m, 2.50m, 0.30m), (gemini.InputPerMTok, gemini.CacheReadPerMTok, gemini.OutputPerMTok, gemini.AudioInputPerMTok));
        Assert.Equal(0.01m, PriceTable.WebSearchPerCall);
        Assert.True(PriceTable.Defaults.ContainsKey("CLAUDE-HAIKU-5-5"));
    }

    [Fact]
    public void PriceMath_InputOutputAndCache()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));

        // 10k in, 50k cache read, 2k cache write, 500 out on Haiku.
        decimal cost = ledger.Add(Usage("claude-haiku-5-5", input: 10_000, cacheRead: 50_000, cacheWrite: 2_000, output: 500));

        decimal expected = ((10_000 * 0.10m) + (50_000 * 0.01m) + (2_000 * 0.125m) + (500 * 0.50m)) / 1_000_000m;
        Assert.Equal(expected, cost);
        Assert.Equal(0.002m, cost);
        Assert.Equal(cost, ledger.MonthToDateUsd);
    }

    [Fact]
    public void PriceMath_Sonnet()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));

        Assert.Equal(0.012m, ledger.Add(Usage("claude-sonnet-5-5", input: 1_000, output: 1_000)));
    }

    [Fact]
    public void LongPromptMultiplier_AppliesToAllTokensAboveThreshold()
    {
        ModelPrice haiku = PriceTable.Defaults["claude-haiku-5-5"];

        // Exactly at the threshold: normal price.
        decimal atThreshold = PriceTable.Compute(Usage("claude-haiku-5-5", input: 100_000, output: 1_000), haiku);
        Assert.Equal((100_000 * 0.10m + 1_000 * 0.50m) / 1_000_000m, atThreshold);

        // Input + cache over the threshold: every token (output too) costs 5x.
        decimal over = PriceTable.Compute(Usage("claude-haiku-5-5", input: 60_000, cacheRead: 41_000, output: 1_000), haiku);
        decimal baseCost = (60_000 * 0.10m + 41_000 * 0.01m + 1_000 * 0.50m) / 1_000_000m;
        Assert.Equal(baseCost * 5, over);

        // Output alone never triggers it.
        decimal bigOutput = PriceTable.Compute(Usage("claude-haiku-5-5", input: 1_000, output: 200_000), haiku);
        Assert.Equal((1_000 * 0.10m + 200_000 * 0.50m) / 1_000_000m, bigOutput);
    }

    [Fact]
    public void LongPromptMultiplier_DoesNotApplyToWebSearchFees()
    {
        ModelPrice haiku = PriceTable.Defaults["claude-haiku-5-5"];
        decimal cost = PriceTable.Compute(Usage("claude-haiku-5-5", input: 200_000, searches: 2), haiku);

        Assert.Equal((200_000 * 0.10m / 1_000_000m * 5) + 0.02m, cost);
    }

    [Fact]
    public void AudioTokens_UseTheAudioPrice()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));

        // 10 s of speech is about 320 audio tokens; transcript about 40 tokens out.
        decimal cost = ledger.Add(Usage("gemini-3.5-flash-lite", input: 200, audio: 320, output: 40));

        Assert.Equal(((200 * 0.30m) + (320 * 0.30m) + (40 * 2.50m)) / 1_000_000m, cost);
    }

    [Fact]
    public void AudioTokens_WithoutAnAudioPrice_CostAsInput()
    {
        var price = new ModelPrice("m", 1m, 0m, 0m, 0m);

        Assert.Equal(1m, PriceTable.Compute(Usage("m", audio: 1_000_000), price));
    }

    [Fact]
    public void WebSearch_CostsPerCall()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));

        Assert.Equal(0.03m, ledger.Add(Usage("claude-haiku-5-5", searches: 3)));
    }

    [Fact]
    public void NegativeTokenCounts_CountAsZero()
    {
        Assert.Equal(0m, PriceTable.Compute(Usage("claude-haiku-5-5", input: -5, output: -1, searches: -2), PriceTable.Defaults["claude-haiku-5-5"]));
    }

    [Fact]
    public void UnknownModel_CostsZeroAndIsFlagged()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));

        Assert.Equal(0m, ledger.Add(Usage("gpt-6-luna", input: 1_000_000, output: 1_000_000)));
        Assert.True(ledger.HasUnknownModels);
        Assert.Equal(["gpt-6-luna"], ledger.UnknownModels);
        Assert.Equal(0m, ledger.MonthToDateUsd);
        Assert.Equal(1, ledger.RequestCount);
    }

    [Fact]
    public void DatedModelIds_FindTheirBasePrice()
    {
        Assert.True(PriceTable.TryFind(PriceTable.Defaults, "claude-haiku-5-5-20261001", out ModelPrice p));
        Assert.Equal("claude-haiku-5-5", p.ModelId);
        Assert.True(PriceTable.TryFind(PriceTable.Defaults, "Gemini-3.5-Flash-Lite", out _));
        Assert.False(PriceTable.TryFind(PriceTable.Defaults, "claude-haiku-5", out _));
        Assert.False(PriceTable.TryFind(PriceTable.Defaults, "claude-haiku-5-55", out _));
        Assert.False(PriceTable.TryFind(PriceTable.Defaults, null, out _));
        Assert.False(PriceTable.TryFind(PriceTable.Defaults, "  ", out _));
    }

    [Fact]
    public void CustomPrices_Override()
    {
        var prices = new Dictionary<string, ModelPrice> { ["x"] = new("x", 1m, 0m, 0m, 2m) };
        var ledger = new CostLedger(new ManualClock(utc: Oct10), 8m, prices);

        Assert.Equal(3m, ledger.Add(Usage("x", input: 1_000_000, output: 1_000_000)));
        Assert.Equal(0m, ledger.Add(Usage("claude-haiku-5-5", input: 1_000)));
    }

    [Fact]
    public void BudgetStates_WarningAt80Percent_ReachedAt100()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10), softCapUsd: 10m, prices: FlatPrices());

        ledger.Add(Dollars(7.99m));
        Assert.Equal(BudgetState.Ok, ledger.State);
        ledger.Add(Dollars(0.01m));
        Assert.Equal(BudgetState.Warning, ledger.State);
        ledger.Add(Dollars(1.99m));
        Assert.Equal(BudgetState.Warning, ledger.State);
        ledger.Add(Dollars(0.01m));
        Assert.Equal(BudgetState.Reached, ledger.State);
        Assert.Equal(10m, ledger.MonthToDateUsd);
    }

    [Fact]
    public void ExtraAllowance_RaisesTheLimit()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10), softCapUsd: 8m, prices: FlatPrices());
        ledger.Add(Dollars(8m));
        Assert.Equal(BudgetState.Reached, ledger.State);

        ledger.AllowExtra();
        Assert.Equal(1m, ledger.ExtraAllowanceUsd);
        Assert.Equal(9m, ledger.LimitUsd);
        Assert.Equal(BudgetState.Warning, ledger.State); // 8 of 9 is still above 80%

        ledger.AllowExtra(2m);
        Assert.Equal(3m, ledger.ExtraAllowanceUsd);
        Assert.Equal(BudgetState.Warning, ledger.State); // 8 of 11 = 72.7%...
    }

    [Fact]
    public void ExtraAllowance_StateMath()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10), softCapUsd: 8m, prices: FlatPrices());
        ledger.Add(Dollars(8m));
        ledger.AllowExtra(4m); // 8 of 12 = 66%

        Assert.Equal(BudgetState.Ok, ledger.State);
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.AllowExtra(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.AllowExtra(-1m));
    }

    [Fact]
    public void WarningIsAnnouncedOncePerMonth()
    {
        var clock = new ManualClock(utc: Oct10);
        var ledger = new CostLedger(clock, softCapUsd: 10m, prices: FlatPrices());

        Assert.False(ledger.TryClaimWarningAnnouncement()); // nothing spent yet
        ledger.Add(Dollars(8.5m));
        Assert.True(ledger.TryClaimWarningAnnouncement());
        Assert.True(ledger.WarningAnnounced);
        Assert.False(ledger.TryClaimWarningAnnouncement());
        ledger.Add(Dollars(2m));
        Assert.False(ledger.TryClaimWarningAnnouncement()); // reached: still once

        clock.Advance((long)TimeSpan.FromDays(25).TotalMilliseconds); // November
        Assert.False(ledger.WarningAnnounced);
        ledger.Add(Dollars(9m));
        Assert.True(ledger.TryClaimWarningAnnouncement());
    }

    [Fact]
    public void MonthlyRollover_ResetsTotalsExtraAndWarning()
    {
        var clock = new ManualClock(utc: new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero));
        var ledger = new CostLedger(clock, softCapUsd: 5m, prices: FlatPrices());
        ledger.Add(Dollars(6m));
        ledger.AllowExtra();
        ledger.MarkWarningAnnounced();
        Assert.Equal(BudgetState.Reached, ledger.State);

        clock.Advance(60_000 * 2); // 2026-11-01 00:01 UTC

        Assert.Equal(0m, ledger.MonthToDateUsd);
        Assert.Equal(0m, ledger.ExtraAllowanceUsd);
        Assert.False(ledger.WarningAnnounced);
        Assert.Equal(BudgetState.Ok, ledger.State);
        Assert.Equal(0, ledger.RequestCount);
        Assert.Empty(ledger.CostByModel());
        Assert.Equal(5m, ledger.SoftCapUsd);
    }

    [Fact]
    public void Rollover_UsesUtcMonths()
    {
        // 01:00 on Nov 1 at UTC+3 is still Oct 31 (22:00) in UTC.
        var clock = new ManualClock(utc: new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.FromHours(3)));
        var ledger = new CostLedger(clock, prices: FlatPrices());
        ledger.Add(Dollars(1m));

        clock.Advance((long)TimeSpan.FromHours(1).TotalMilliseconds); // Oct 31 23:00 UTC
        Assert.Equal(1m, ledger.MonthToDateUsd);
        clock.Advance((long)TimeSpan.FromHours(1).TotalMilliseconds); // Nov 1 00:00 UTC
        Assert.Equal(0m, ledger.MonthToDateUsd);
    }

    [Fact]
    public void CostByModel_TracksEachModel()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));
        ledger.Add(Usage("claude-haiku-5-5", input: 1_000_000));
        ledger.Add(Usage("claude-haiku-5-5", input: 1_000_000));
        ledger.Add(Usage("gemini-3.5-flash-lite", output: 1_000_000));

        IReadOnlyDictionary<string, decimal> byModel = ledger.CostByModel();

        Assert.Equal(0.20m, byModel["claude-haiku-5-5"]);
        Assert.Equal(2.50m, byModel["gemini-3.5-flash-lite"]);
        Assert.Equal(2.70m, ledger.MonthToDateUsd);
        Assert.Equal(3, ledger.RequestCount);
    }

    [Fact]
    public void Persistence_RoundTrip()
    {
        var clock = new ManualClock(utc: Oct10);
        var ledger = new CostLedger(clock, softCapUsd: 6m);
        ledger.Add(Usage("claude-haiku-5-5", input: 12_345, cacheRead: 6_789, output: 321, searches: 1));
        ledger.Add(Usage("gemini-3.5-flash-lite", audio: 500, output: 30));
        ledger.Add(Usage("mystery-model", input: 10));
        ledger.AllowExtra(1.5m);
        ledger.MarkWarningAnnounced();

        string json = ledger.Serialize();
        CostLedger restored = CostLedger.Deserialize(json, clock);

        Assert.Equal(ledger.MonthToDateUsd, restored.MonthToDateUsd);
        Assert.Equal(6m, restored.SoftCapUsd);
        Assert.Equal(1.5m, restored.ExtraAllowanceUsd);
        Assert.True(restored.WarningAnnounced);
        Assert.Equal(3, restored.RequestCount);
        Assert.Equal(ledger.UnknownModels, restored.UnknownModels);
        Assert.Equal(ledger.CostByModel()["claude-haiku-5-5"], restored.CostByModel()["claude-haiku-5-5"]);
        Assert.Equal(json, restored.Serialize());
        Assert.Contains("\"monthToDateUsd\"", json);
        Assert.Contains("\"year\":2026", json);
        Assert.Contains("\"month\":10", json);
    }

    [Fact]
    public void Persistence_SavedLastMonth_RollsOverOnLoad()
    {
        var october = new ManualClock(utc: Oct10);
        var ledger = new CostLedger(october, prices: FlatPrices());
        ledger.Add(Dollars(5m));
        string json = ledger.Serialize();

        var november = new ManualClock(utc: new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        CostLedger restored = CostLedger.Deserialize(json, november, FlatPrices());

        Assert.Equal(0m, restored.MonthToDateUsd);
    }

    [Fact]
    public void Persistence_ClockMovedBackwards_KeepsTheTotal()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10), prices: FlatPrices());
        ledger.Add(Dollars(5m));

        var september = new ManualClock(utc: new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero));
        CostLedger restored = CostLedger.Deserialize(ledger.Serialize(), september, FlatPrices());

        Assert.Equal(5m, restored.MonthToDateUsd);
    }

    [Fact]
    public void Persistence_ToleratesMissingAndBadFields()
    {
        var clock = new ManualClock(utc: Oct10);

        CostLedger fromEmptyObject = CostLedger.Deserialize("{}", clock);
        Assert.Equal(0m, fromEmptyObject.MonthToDateUsd);
        Assert.Equal(8m, fromEmptyObject.SoftCapUsd);

        CostLedger negative = CostLedger.Deserialize("""{"year":2026,"month":10,"monthToDateUsd":-3,"softCapUsd":-1,"extra":"x"}""", clock);
        Assert.Equal(0m, negative.MonthToDateUsd);
        Assert.Equal(0m, negative.SoftCapUsd);

        Assert.Throws<System.Text.Json.JsonException>(() => CostLedger.Deserialize("not json", clock));
        Assert.False(CostLedger.TryDeserialize("not json", clock, out _));
        Assert.False(CostLedger.TryDeserialize(null, clock, out _));
        Assert.True(CostLedger.TryDeserialize("{}", clock, out CostLedger? ok));
        Assert.NotNull(ok);
    }

    [Fact]
    public void SoftCap_Validation()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10));
        ledger.SoftCapUsd = 3m;
        Assert.Equal(3m, ledger.SoftCapUsd);
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.SoftCapUsd = -1m);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CostLedger(new ManualClock(), -1m));
        Assert.Throws<ArgumentNullException>(() => new CostLedger(null!));
    }

    [Fact]
    public void ZeroCap_MeansReached()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10), softCapUsd: 0m);

        Assert.Equal(BudgetState.Reached, ledger.State);
    }

    [Fact]
    public async Task ConcurrentAdds_AreCounted()
    {
        var ledger = new CostLedger(new ManualClock(utc: Oct10), softCapUsd: 1000m, prices: FlatPrices());

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                ledger.Add(Dollars(0.01m));
            }
        })));

        Assert.Equal(80m, ledger.MonthToDateUsd);
        Assert.Equal(8000, ledger.RequestCount);
    }

    /// <summary>A model priced at $1 per million input tokens, so token counts read as micro-dollars.</summary>
    private static Dictionary<string, ModelPrice> FlatPrices() => new() { ["flat"] = new("flat", 1m, 0m, 0m, 0m) };

    private static UsageRecord Dollars(decimal usd) => Usage("flat", input: (long)(usd * 1_000_000m));
}
