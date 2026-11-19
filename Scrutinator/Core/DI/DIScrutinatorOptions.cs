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
    /// If true (the default), attempts to open the dashboard in the default browser after app start.
    /// This is effective only in Development; set false for headless or remote hosts.
    /// </summary>
    public bool OpenDashboardAutomatically { get; set; } = true;
}