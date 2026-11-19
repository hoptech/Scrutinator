namespace Scrutinator.Core.Package;

public class PackageScrutinatorOptions
{
    /// <summary>
    /// If true (the default), attempts to open the dashboard in the default browser after app start.
    /// This is effective only in Development; set false for headless or remote hosts.
    /// </summary>
    public bool OpenDashboardAutomatically { get; set; } = true;
}