using Basira.App.Abstractions;
using Basira.App.Composition;
using Basira.App.Screen;
using Basira.Core.Agent;
using Basira.Core.Logging;

namespace Basira.App.Brain;

/// <summary>Registers the Claude brain (<see cref="IBrain"/>) and Gemini speech-to-text (<see cref="IEars"/>).</summary>
internal static class BrainModule
{
    public static void Register(ServiceRegistry registry)
    {
        registry.AddFactory<IBrain>(r => new ClaudeBrain(
            r.Get<ISecretStore>(),
            r.Get<ISettingsProvider>(),
            r.TryGet<IEventLog>() ?? NullEventLog.Instance));
        registry.AddFactory<IEars>(r => new GeminiEars(
            r.Get<ISecretStore>(),
            r.Get<ISettingsProvider>(),
            r.TryGet<IEventLog>()));
    }
}
