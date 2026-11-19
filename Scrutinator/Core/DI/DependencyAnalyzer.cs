namespace Scrutinator.Core.DI;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

internal sealed record DIScrutinatorServiceCollection(IServiceCollection Services);

public static class DependencyAnalyzer
{
    private const string BackgroundServiceTag = "Background";

    public static ScrutinatorReport Analyze(IServiceCollection services, DIScrutinatorOptions options)
    {
        var report = new ScrutinatorReport();
        var descriptors = services
            .Where(descriptor => descriptor.ServiceType != typeof(DIScrutinatorServiceCollection))
            .ToArray();

        if (options.ScanForCaptiveDependencies)
        {
            report.AnalysisLimitations.Add(
                "Only direct constructor dependencies are checked. Transitive dependencies and services resolved later through IServiceProvider or IKeyedServiceProvider are not analyzed.");
        }

        foreach (var descriptor in descriptors)
        {
            var isSystem = IsSystemService(descriptor);
            
            if (isSystem && !options.IncludeSystemServices)
            {
                report.SystemServicesHidden++;
                continue;
            }

            var node = new ServiceNode
            {
                ServiceType = FormatName(descriptor.ServiceType),
                ImplementationType = GetImplementationName(descriptor),
                Lifetime = descriptor.Lifetime.ToString(),
                Namespace = descriptor.ServiceType.Namespace ?? "Global"
            };

            if (descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
            {
                node.Tags.Add(BackgroundServiceTag);
            }
            report.Services.Add(node);

            if (options.ScanForCaptiveDependencies && descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                CheckForCaptiveDependency(descriptor, descriptors, report);
            }
        }

        report.TotalServices = descriptors.Length;
        return report;
    }

    private static void CheckForCaptiveDependency(
        ServiceDescriptor singletonDescriptor,
        ServiceDescriptor[] descriptors,
        ScrutinatorReport report)
    {
        var implementationType = GetImplementationType(singletonDescriptor);
        if (implementationType == null)
        {
            var limitation = GetImplementationInstance(singletonDescriptor) != null
                ? $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' is registered as an instance; its construction dependencies cannot be inspected."
                : $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' is registered with a factory; the factory dependency graph cannot be inspected.";
            AddAnalysisLimitation(report, limitation);
            return;
        }

        if (implementationType.ContainsGenericParameters)
        {
            AddAnalysisLimitation(
                report,
                $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' is an open-generic registration; its closed implementation and constructor dependencies cannot be determined.");
            return;
        }

        var constructors = implementationType
            .GetConstructors()
            .Where(constructor => constructor.GetParameters().All(parameter => CanResolve(parameter, descriptors)))
            .ToArray();

        if (constructors.Length == 0)
        {
            AddAnalysisLimitation(
                report,
                $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' has no public constructor whose parameters can be resolved from the registered services; its dependencies were not analyzed.");
            return;
        }

        var greatestParameterCount = constructors.Max(constructor => constructor.GetParameters().Length);
        var selectedConstructors = constructors
            .Where(constructor => constructor.GetParameters().Length == greatestParameterCount)
            .ToArray();

        if (selectedConstructors.Length != 1)
        {
            AddAnalysisLimitation(
                report,
                $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' has multiple equally greedy resolvable constructors; its dependencies were not analyzed.");
            return;
        }

        foreach (var parameter in selectedConstructors[0].GetParameters())
        {
            if (parameter.GetCustomAttribute<FromKeyedServicesAttribute>() != null)
            {
                AddAnalysisLimitation(
                    report,
                    $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' has a keyed constructor dependency; keyed service lifetimes are not analyzed.");
                continue;
            }

            var isEnumerable = TryGetEnumerableElementType(parameter.ParameterType, out var dependencyType);
            var matchingRegistrations = GetMatchingRegistrations(dependencyType, descriptors);

            var hasScopedDependency = isEnumerable
                ? matchingRegistrations.Any(registration => registration.Lifetime == ServiceLifetime.Scoped)
                : matchingRegistrations.LastOrDefault()?.Lifetime == ServiceLifetime.Scoped;

            if (hasScopedDependency)
            {
                report.Warnings.Add(new DependencyWarning
                {
                    ServiceName = FormatName(singletonDescriptor.ServiceType),
                    WarningType = "Potential Captive Dependency",
                    Message = $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' directly depends on Scoped service '{FormatName(dependencyType)}' through its selected constructor."
                });
            }
        }
    }

    private static bool CanResolve(ParameterInfo parameter, ServiceDescriptor[] descriptors)
    {
        if (parameter.HasDefaultValue ||
            parameter.GetCustomAttribute<FromKeyedServicesAttribute>() != null ||
            parameter.ParameterType == typeof(IServiceProvider) ||
            parameter.ParameterType == typeof(IServiceScopeFactory) ||
            parameter.ParameterType == typeof(IKeyedServiceProvider) ||
            TryGetEnumerableElementType(parameter.ParameterType, out _))
        {
            return true;
        }

        return GetMatchingRegistrations(parameter.ParameterType, descriptors).Length > 0;
    }

    private static ServiceDescriptor[] GetMatchingRegistrations(Type serviceType, ServiceDescriptor[] descriptors)
    {
        return descriptors
            .Where(descriptor =>
                !descriptor.IsKeyedService &&
                (descriptor.ServiceType == serviceType ||
                 descriptor.ServiceType.IsGenericTypeDefinition &&
                 serviceType.IsConstructedGenericType &&
                 descriptor.ServiceType == serviceType.GetGenericTypeDefinition()))
            .ToArray();
    }

    private static bool TryGetEnumerableElementType(Type serviceType, out Type elementType)
    {
        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = serviceType.GetGenericArguments()[0];
            return true;
        }

        elementType = serviceType;
        return false;
    }

    private static void AddAnalysisLimitation(ScrutinatorReport report, string limitation)
    {
        if (!report.AnalysisLimitations.Contains(limitation))
        {
            report.AnalysisLimitations.Add(limitation);
        }
    }

    // --- Helper Methods ---

    private static bool IsSystemService(ServiceDescriptor descriptor)
    {
        // Check the Service Type (Interface)
        var serviceNs = descriptor.ServiceType.Namespace;
        var serviceIsSystem = serviceNs != null && (serviceNs.StartsWith("System") || serviceNs.StartsWith("Microsoft"));

        // Check the Implementation Type (Your Class)
        var implementationIsSystem = true; // Default to true for Factories/Instances we can't read
    
        var implementationType = GetImplementationType(descriptor);
        if (implementationType != null)
        {
            var implNs = implementationType.Namespace;
            // If implementation is in user code (does NOT start with System/Microsoft), it's NOT a system service.
            implementationIsSystem = implNs != null && (implNs.StartsWith("System") || implNs.StartsWith("Microsoft"));
        }

        // It is a system service ONLY if the Interface is System AND the Implementation is System (or unknown).
        // If Interface is System (IHostedService) but Implementation is User (MyWorker), return false (Show it).
        return serviceIsSystem && implementationIsSystem;
    }

    private static string GetImplementationName(ServiceDescriptor descriptor)
    {
        var implementationType = GetImplementationType(descriptor);
        if (implementationType != null)
        {
            return FormatName(implementationType);
        }
    
        var implementationInstance = GetImplementationInstance(descriptor);
        if (implementationInstance != null)
        {
            return FormatName(implementationInstance.GetType()) + " (Instance)";
        }
        
        var implementationFactoryMethod = descriptor.IsKeyedService
            ? descriptor.KeyedImplementationFactory?.Method
            : descriptor.ImplementationFactory?.Method;
        if (implementationFactoryMethod != null)
        {
            var declaringType = implementationFactoryMethod.DeclaringType?.Name;
        
            // If the factory is a simple lambda in Program.cs, DeclaringType is usually the compiler-generated class
            // But for System services, it often reveals the helper class.
        
            if (declaringType != null && declaringType.Contains("<>"))
            {
                // Compiler generated (lambda)
                return "Factory (Lambda)";
            }

            return $"Factory ({declaringType ?? "Unknown"})";
        }

        return "Unknown";
    }

    private static Type? GetImplementationType(ServiceDescriptor descriptor) =>
        descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType;

    private static object? GetImplementationInstance(ServiceDescriptor descriptor) =>
        descriptor.IsKeyedService ? descriptor.KeyedImplementationInstance : descriptor.ImplementationInstance;

    private static string FormatName(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        
        var name = type.Name.Split('`')[0];
        var args = string.Join(", ", type.GetGenericArguments().Select(a => a.Name));

        return $"{name}<{args}>";
    }
}