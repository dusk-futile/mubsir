using System.Text.Json;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Common;
using Basira.Core.Cost;
using Basira.Core.Logging;

namespace Basira.App.Storage;

/// <summary>Loads and saves the monthly <see cref="CostLedger"/> in <see cref="AppPaths.CostFile"/> (amounts only, no content).</summary>
internal sealed class CostStore
{
    private readonly object _gate = new();
    private readonly IEventLog _log;
    private readonly string _path;

    public CostStore(IEventLog log, string? path = null)
    {
        _log = log;
        _path = path ?? AppPaths.CostFile;
    }

    public CostLedger Load(IClock clock, decimal softCapUsd)
    {
        try
        {
            string? json = File.Exists(_path) ? File.ReadAllText(_path) : null;
            if (CostLedger.TryDeserialize(json, clock, out CostLedger? ledger) && ledger is not null)
            {
                ledger.SoftCapUsd = Math.Max(0m, softCapUsd);
                return ledger;
            }

            if (json is not null)
            {
                _log.Error("cost_file_malformed", LogKind.Budget);
                AtomicFile.QuarantineQuietly(_path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Failure("cost_load", e);
        }

        return new CostLedger(clock, Math.Max(0m, softCapUsd));
    }

    public void Save(CostLedger ledger)
    {
        try
        {
            string json = ledger.Serialize();
            lock (_gate)
            {
                AtomicFile.WriteAllText(_path, json);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Failure("cost_save", e);
        }
    }
}
