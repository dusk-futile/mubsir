namespace Basira.App.Composition;

/// <summary>Registers the screen, brain and action services after the host's own services.</summary>
internal static class Modules
{
    public static void RegisterAll(ServiceRegistry registry)
    {
        Screen.ScreenModule.Register(registry);
        Brain.BrainModule.Register(registry);
        Actions.ActionsModule.Register(registry);
    }
}
