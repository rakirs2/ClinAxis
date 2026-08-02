using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Scrapers.Services.CrawlServices;

/// <summary>
/// Auto-discovers and manages pivot enricher services.
/// Reflects over all IPivotEnricherService implementations and exposes them for use.
/// Allows dynamic enable/disable of pivots via database configuration.
/// </summary>
public sealed class PivotServiceRegistry
{
    private static readonly Action<ILogger, string, Exception> LogPivotInstantiationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "PivotInstantiationFailed"),
            "Failed to instantiate pivot {PivotType}");

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PivotServiceRegistry> _logger;
    private readonly Dictionary<string, IPivotEnricherService> _registeredPivots = new();

    public PivotServiceRegistry(IServiceProvider serviceProvider, ILogger<PivotServiceRegistry> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        DiscoverPivots();
    }

    /// <summary>
    /// Reflect and discover all IPivotEnricherService implementations.
    /// </summary>
    private void DiscoverPivots()
    {
        var pivotType = typeof(IPivotEnricherService);
        var pivotImplementations = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => pivotType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

        foreach (var implementationType in pivotImplementations)
        {
            try
            {
                var instance = (IPivotEnricherService)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType);
                _registeredPivots[instance.PivotName] = instance;
            }
            catch (InvalidOperationException ex)
            {
                // Log but continue discovering other pivots
                LogPivotInstantiationFailed(_logger, implementationType.Name, ex);
            }
        }
    }

    /// <summary>
    /// Get all registered pivot services.
    /// </summary>
    public IReadOnlyDictionary<string, IPivotEnricherService> AllPivots => _registeredPivots.AsReadOnly();

    /// <summary>
    /// Get a specific pivot by name.
    /// </summary>
    public IPivotEnricherService? GetPivot(string pivotName) =>
        _registeredPivots.TryGetValue(pivotName, out var pivot) ? pivot : null;

    /// <summary>
    /// Get all pivot names.
    /// </summary>
    public IEnumerable<string> PivotNames => _registeredPivots.Keys;
}
