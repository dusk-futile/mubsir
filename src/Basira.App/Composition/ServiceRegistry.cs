namespace Basira.App.Composition;

/// <summary>
/// A deliberately tiny service container: singletons and lazy factories keyed by type.
/// Modules register what they provide; the host resolves what it needs and runs in
/// local-only mode when an optional service (brain, ears) is missing.
/// </summary>
public sealed class ServiceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<ServiceRegistry, object>> _factories = [];
    private readonly Dictionary<Type, object> _instances = [];

    public void AddSingleton<T>(T instance)
        where T : class
    {
        lock (_gate)
        {
            _instances[typeof(T)] = instance;
        }
    }

    /// <summary>Registers a factory; it runs once, on first <see cref="Get{T}"/>.</summary>
    public void AddFactory<T>(Func<ServiceRegistry, T> factory)
        where T : class
    {
        lock (_gate)
        {
            _factories[typeof(T)] = factory;
            _instances.Remove(typeof(T));
        }
    }

    public bool Has<T>()
        where T : class
    {
        lock (_gate)
        {
            return _instances.ContainsKey(typeof(T)) || _factories.ContainsKey(typeof(T));
        }
    }

    public T Get<T>()
        where T : class
        => TryGet<T>() ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");

    public T? TryGet<T>()
        where T : class
    {
        Func<ServiceRegistry, object>? factory;
        lock (_gate)
        {
            if (_instances.TryGetValue(typeof(T), out var existing))
            {
                return (T)existing;
            }

            if (!_factories.TryGetValue(typeof(T), out factory))
            {
                return null;
            }
        }

        // Run the factory outside the lock: it may resolve other services.
        var created = (T)factory(this);
        lock (_gate)
        {
            if (_instances.TryGetValue(typeof(T), out var raced))
            {
                return (T)raced;
            }

            _instances[typeof(T)] = created;
            return created;
        }
    }
}
