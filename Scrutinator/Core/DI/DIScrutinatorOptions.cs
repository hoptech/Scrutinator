namespace Scrutinator.Core.DI;

public sealed class DIScrutinatorOptions
{
    /// <summary>
    /// If true, includes Microsoft and System services in the list.
    /// </summary>
    public bool IncludeSystemServices { get; set; } = false;

    /// <summary>
    /// If true, scans for Scoped services injected into Singletons.
    /// </summary>
    public bool ScanForCaptiveDependencies { get; set; } = true;

    /// <summary>
    /// If true, opens the default browser to the dashboard on app start (Dev only).
    /// </summary>
    public bool OpenDashboardAutomatically { get; set; } = true;
}