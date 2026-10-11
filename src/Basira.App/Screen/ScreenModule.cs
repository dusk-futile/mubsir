using Basira.App.Abstractions;
using Basira.App.Composition;
using Basira.App.Screen.Documents;
using Basira.Core.Logging;

namespace Basira.App.Screen;

/// <summary>Registers the UI Automation screen reader (as itself and as <see cref="IScreenReader"/>) and the document reader.</summary>
internal static class ScreenModule
{
    public static void Register(ServiceRegistry registry)
    {
        registry.AddFactory(r => new UiaScreenReader(r.TryGet<IEventLog>()));
        registry.AddFactory<IScreenReader>(r => r.Get<UiaScreenReader>());
        registry.AddFactory(r => new DocumentReader(r.Get<UiaScreenReader>(), r.TryGet<IEventLog>() ?? NullEventLog.Instance));
        registry.AddFactory(_ => new ReadingPositions());
    }
}
