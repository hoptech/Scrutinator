using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Scrutinator.Util;

public static class BrowserLauncher
{
    public static void Open(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows requires a specific trick to open URLs in the default browser
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") 
                { 
                    CreateNoWindow = true 
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            // Silently fail or log if needed. 
            // We don't want to crash the app just because the browser didn't open.
            Console.WriteLine($"[Scrutinator] Failed to open browser: {ex.Message}");
        }
    }
}