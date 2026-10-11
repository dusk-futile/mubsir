using System.Text.Json;
using System.Text.Json.Serialization;
using Mubsir.Core.Common;

namespace Mubsir.Core.Cost;

public enum BudgetState
{
    Ok,

    Warning,

    Reached,
}

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

    public void AllowExtra(decimal usd = 1m)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usd);
        lock (_gate)
        {
            Rollover();
            _extra += usd;
        }
    }

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
            : 0;
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
