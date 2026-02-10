namespace Scrutinator.Core.Package;

public record PackageNode(string Name, string Version, string Location, string[] UsedBy, bool IsDirect);