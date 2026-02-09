namespace Scrutinator.Core.DI;

public sealed class ScrutinatorReport
{
    public List<ServiceNode> Services { get; set; } = [];
    public List<DependencyWarning> Warnings { get; set; } = [];
    public int TotalServices { get; set; }
    public int SystemServicesHidden { get; set; }
}

public class ServiceNode
{
    public required string ServiceType { get; set; }
    public required string ImplementationType { get; set; }
    public required string Lifetime { get; set; }
    public required string Namespace { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class DependencyWarning
{
    public required string ServiceName { get; set; }
    public required string WarningType { get; set; }
    public required string Message { get; set; }
}