using Basira.App.Abstractions;
using Basira.App.Composition;
using Basira.App.Screen;
using Basira.App.Screen.Documents;
using Basira.Core.Agent;
using Basira.Core.Logging;

namespace Basira.App.Actions;

/// <summary>Registers the Windows tool executor (<see cref="IToolExecutor"/>). The brain, speech, memory and prompter are optional.</summary>
internal static class ActionsModule
{
    public static void Register(ServiceRegistry registry)
    {
        registry.AddFactory<IToolExecutor>(r => new WindowsToolExecutor(
            r.Get<UiaScreenReader>(),
            r.Get<DocumentReader>(),
            r.Get<ReadingPositions>(),
            Optional<IBrain>(r),
            r.TryGet<ISpeechService>(),
            r.TryGet<ISettingsProvider>(),
            r.TryGet<IMemoryStore>(),
            r.TryGet<IUserPrompter>(),
            r.TryGet<IUsageSink>(),
            r.TryGet<IEventLog>()));
    }

    /// <summary>An optional service: missing, or failing to build (e.g. no secret store yet), means local-only.</summary>
    private static T? Optional<T>(ServiceRegistry r)
        where T : class
    {
        try
        {
            return r.TryGet<T>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
