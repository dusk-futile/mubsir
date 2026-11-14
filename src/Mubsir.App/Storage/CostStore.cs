using System.Text.Json;
using Mubsir.Core.Common;
using Mubsir.Core.Cost;
using Mubsir.Core.Logging;

namespace Mubsir.App.Storage;

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
