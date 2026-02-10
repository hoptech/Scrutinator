using System.Reflection;

namespace Scrutinator.Core.Package;

public static class PackageAnalyzer
{
    public static PackagesReport Analyze()
    {
        var report = new PackagesReport();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        
        foreach (var asm in assemblies)
        {
            try 
            {
                var name = asm.GetName().Name ?? "Unknown";
                
                var usedBy = assemblies
                    .Where(other => other.GetReferencedAssemblies().Any(r => r.Name == name))
                    .Select(other => other.GetName().Name ?? "Unknown")
                    .ToArray();

                var isDirect = name == entryAssemblyName || usedBy.Contains(entryAssemblyName);

                report.Packages.Add(new PackageNode(name, asm.GetName().Version?.ToString() ?? "0.0.0.0", asm.Location, usedBy, isDirect));
            }
            catch 
            {
                // Some system assemblies might throw on inspection
            }
        }

        // Sort for easier reading
        report.Packages = report.Packages.OrderBy(a => a.Name).ToList();

        return report;
    }

}