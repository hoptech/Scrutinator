namespace Scrutinator.Core.Log;

public class LogScrutinatorOptions
{
    /// <summary>
    /// If true (the default), attempts to open the dashboard in the default browser after app start.
    /// This is effective only in Development; set false for headless or remote hosts.
    /// </summary>
    public bool OpenDashboardAutomatically { get; set; } = true;
}