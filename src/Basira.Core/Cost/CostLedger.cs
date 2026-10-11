using System.Text.Json;
using System.Text.Json.Serialization;
using Basira.Core.Common;

namespace Basira.Core.Cost;

public enum BudgetState
{
    Ok,

    /// <summary>At least 80% of the soft cap plus extra allowance is spent.</summary>
    Warning,

    /// <summary>The soft cap plus extra allowance is spent.</summary>
    Reached,
}

/// <summary>
/// Month-to-date AI spending against a local soft cap. Months are UTC calendar months; the first
/// access in a new month resets the total, the extra allowance and the warning flag. Thread-safe.
/// The real spending ceiling is the provider-side limit; this ledger warns the user early.
/// </summary>
public sealed class CostLedger
{
    private const decimal WarningFraction = 0.8m;

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly IReadOnlyDictionary<string, ModelPrice> _prices;
    private readonly Dictionary<string, decimal> _byModel = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> _unknownModels = new(StringComparer.OrdinalIgnoreCase);
    private int _monthIndex;
    private decimal _monthToDate;
    private decimal _softCap;
    private decimal _extra;
    private bool _warningAnnounced;
    private long _requests;

    public CostLedger(IClock clock, decimal softCapUsd = 8m, IReadOnlyDictionary<string, ModelPrice>? prices = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegative(softCapUsd);
        _clock = clock;
        _prices = prices ?? PriceTable.Defaults;
        _softCap = softCapUsd;
        _monthIndex = MonthIndex(clock.UtcNow);
    }

    public decimal MonthToDateUsd
    {
        get
        {
            lock (_gate)
            {
                Rollover();
                return _monthToDate;
            }
        }
    }

    public decimal SoftCapUsd
    {
        get
        {
            lock (_gate)
            {
                return _softCap;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (_gate)
            {
                _softCap = value;
            }
        }
    }

    /// <summary>Extra dollars the user allowed this month after reaching the cap.</summary>
    public decimal ExtraAllowanceUsd
    {
        get
        {
            lock (_gate)
            {
                Rollover();
                return _extra;
            }
        }
    }

    /// <summary>The cap plus this month's extra allowance.</summary>
    public decimal LimitUsd
    {
        get
        {
            lock (_gate)
            {
                Rollover();
                return _softCap + _extra;
            }
        }
    }

    public BudgetState State
    {
        get
        {
            lock (_gate)
            {
                Rollover();
                return ComputeState();
            }
        }
    }

    /// <summary>Whether the 80% warning was already spoken this month.</summary>
    public bool WarningAnnounced
    {
        get
        {
            lock (_gate)
            {
                Rollover();
                return _warningAnnounced;
            }
        }
    }

    /// <summary>Requests recorded this month.</summary>
    public long RequestCount
    {
        get
        {
            lock (_gate)
            {
                Rollover();
                return _requests;
            }
        }
    }

    /// <summary>Models seen without a known price; their token cost was counted as zero.</summary>
    public IReadOnlyCollection<string> UnknownModels
    {
        get
        {
            lock (_gate)
            {
                return _unknownModels.ToArray();
            }
        }
    }

    public bool HasUnknownModels
    {
        get
        {
            lock (_gate)
            {
                return _unknownModels.Count > 0;
            }
        }
    }

    /// <summary>
    /// Records one request and returns its cost. An unknown model's token cost counts as zero and
    /// the model is flagged in <see cref="UnknownModels"/>; web search fees still count.
    /// </summary>
    public decimal Add(UsageRecord r)
    {
        ArgumentNullException.ThrowIfNull(r);
        lock (_gate)
        {
            Rollover();
            decimal cost;
            string model = string.IsNullOrWhiteSpace(r.Model) ? "unknown" : r.Model.Trim();
            if (PriceTable.TryFind(_prices, model, out ModelPrice price))
            {
                cost = PriceTable.Compute(r, price);
            }
            else
            {
                _unknownModels.Add(model);
                cost = Math.Max(0, r.WebSearches) * PriceTable.WebSearchPerCall;
            }

            _monthToDate += cost;
            _requests++;
            _byModel[model] = _byModel.GetValueOrDefault(model) + cost;
            return cost;
        }
    }

    /// <summary>Allows <paramref name="usd"/> more this month (the "press Enter for one more dollar" key).</summary>
    public void AllowExtra(decimal usd = 1m)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usd);
        lock (_gate)
        {
            Rollover();
            _extra += usd;
        }
    }

    /// <summary>
    /// True exactly once per month, the first time it is called while the budget is at
    /// <see cref="BudgetState.Warning"/> or above; marks the warning as announced.
    /// </summary>
    public bool TryClaimWarningAnnouncement()
    {
        lock (_gate)
        {
            Rollover();
            if (_warningAnnounced || ComputeState() == BudgetState.Ok)
            {
                return false;
            }

            _warningAnnounced = true;
            return true;
        }
    }

    public void MarkWarningAnnounced()
    {
        lock (_gate)
        {
            Rollover();
            _warningAnnounced = true;
        }
    }

    /// <summary>This month's cost per model id.</summary>
    public IReadOnlyDictionary<string, decimal> CostByModel()
    {
        lock (_gate)
        {
            Rollover();
            return new Dictionary<string, decimal>(_byModel, StringComparer.OrdinalIgnoreCase);
        }
    }

    public string Serialize()
    {
        LedgerState state;
        lock (_gate)
        {
            Rollover();
            state = new LedgerState
            {
                Year = _monthIndex / 12,
                Month = (_monthIndex % 12) + 1,
                MonthToDateUsd = _monthToDate,
                SoftCapUsd = _softCap,
                ExtraAllowanceUsd = _extra,
                WarningAnnounced = _warningAnnounced,
                Requests = _requests,
                ByModel = new Dictionary<string, decimal>(_byModel),
                UnknownModels = _unknownModels.ToList(),
            };
        }

        return JsonSerializer.Serialize(state, CostJsonContext.Default.LedgerState);
    }

    /// <summary>Restores a ledger saved by <see cref="Serialize"/>. Throws <see cref="JsonException"/> on malformed input.</summary>
    public static CostLedger Deserialize(string json, IClock clock) => Deserialize(json, clock, null);

    public static CostLedger Deserialize(string json, IClock clock, IReadOnlyDictionary<string, ModelPrice>? prices)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(clock);
        LedgerState state = JsonSerializer.Deserialize(json, CostJsonContext.Default.LedgerState)
            ?? throw new JsonException("Empty cost ledger.");

        var ledger = new CostLedger(clock, Math.Max(0, state.SoftCapUsd), prices);
        ledger._monthIndex = state.Year is >= 2000 and <= 9999 && state.Month is >= 1 and <= 12
            ? (state.Year * 12) + state.Month - 1
            : 0; // unknown month: totals are reset on first access
        ledger._monthToDate = Math.Max(0, state.MonthToDateUsd);
        ledger._extra = Math.Max(0, state.ExtraAllowanceUsd);
        ledger._warningAnnounced = state.WarningAnnounced;
        ledger._requests = Math.Max(0, state.Requests);
        foreach ((string model, decimal cost) in state.ByModel ?? [])
        {
            ledger._byModel[model] = cost;
        }

        foreach (string model in state.UnknownModels ?? [])
        {
            ledger._unknownModels.Add(model);
        }

        return ledger;
    }

    public static bool TryDeserialize(string? json, IClock clock, out CostLedger? ledger)
    {
        ledger = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            ledger = Deserialize(json, clock);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int MonthIndex(DateTimeOffset at)
    {
        DateTime utc = at.UtcDateTime;
        return (utc.Year * 12) + utc.Month - 1;
    }

    private BudgetState ComputeState()
    {
        decimal limit = _softCap + _extra;
        if (_monthToDate >= limit)
        {
            return BudgetState.Reached;
        }

        return _monthToDate >= limit * WarningFraction ? BudgetState.Warning : BudgetState.Ok;
    }

    /// <summary>A later UTC month starts fresh. A clock that moved backwards never resets the total.</summary>
    private void Rollover()
    {
        int current = MonthIndex(_clock.UtcNow);
        if (current > _monthIndex)
        {
            _monthIndex = current;
            _monthToDate = 0;
            _extra = 0;
            _warningAnnounced = false;
            _requests = 0;
            _byModel.Clear();
        }
    }
}

/// <summary>Persisted form of <see cref="CostLedger"/>.</summary>
internal sealed class LedgerState
{
    public int Version { get; set; } = 1;

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal MonthToDateUsd { get; set; }

    public decimal SoftCapUsd { get; set; } = 8m;

    public decimal ExtraAllowanceUsd { get; set; }

    public bool WarningAnnounced { get; set; }

    public long Requests { get; set; }

    public Dictionary<string, decimal>? ByModel { get; set; }

    public List<string>? UnknownModels { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LedgerState))]
internal sealed partial class CostJsonContext : JsonSerializerContext;
