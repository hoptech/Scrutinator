namespace Scrutinator.Core;

using Microsoft.Extensions.DependencyInjection;

public class DependencyAnalyzer
{
    public static ScrutinatorReport Analyze(IServiceCollection services, ScrutinatorOptions options)
    {
        var report = new ScrutinatorReport();

        // 1. Indexing Pass: Map every Service Type to its Lifetime(s)
        // We use a Dictionary because a Type might be registered multiple times, 
        // though usually we care if *any* registration is Scoped.
        var lifetimeMap = new Dictionary<Type, ServiceLifetime>();
        
        foreach (var descriptor in services)
        {
            // If multiple registrations exist, we generally track the 'most restrictive' or just the fact it exists.
            // For captive dependency checks, if a type is registered as Scoped anywhere, it's risky.
            if (!lifetimeMap.ContainsKey(descriptor.ServiceType))
            {
                lifetimeMap[descriptor.ServiceType] = descriptor.Lifetime;
            }
        }

        // 2. Scanning Pass
        foreach (var descriptor in services)
        {
            var isSystem = IsSystemService(descriptor);
            
            // Should we hide this from the main list?
            if (isSystem && !options.IncludeSystemServices)
            {
                report.SystemServicesHidden++;
                continue; // Skip adding to the list, but we still analyzed it for the map above
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
                node.Tags.Add("Background");
            }

            report.Services.Add(node);

            // 3. Captive Dependency Check
            if (options.ScanForCaptiveDependencies && descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                CheckForCaptiveDependency(descriptor, lifetimeMap, report);
            }
        }

        report.TotalServices = services.Count;
        return report;
    }
    
    private static void CheckForCaptiveDependency(
        ServiceDescriptor singletonDescriptor, 
        Dictionary<Type, ServiceLifetime> lifetimeMap, 
        ScrutinatorReport report)
    {
        // We can only statically analyze if ImplementationType is known.
        // If it uses a Factory (implFactory), we cannot see inside the delegate easily.
        if (singletonDescriptor.ImplementationType == null) return;

        // Get the constructor (assuming the first public one for simplicity, 
        // effectively DI uses the one with most resolvable parameters)
        var ctor = singletonDescriptor.ImplementationType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor == null) return;

        foreach (var param in ctor.GetParameters())
        {
            var paramType = param.ParameterType;

            // Check if this parameter type is registered as Scoped
            if (lifetimeMap.TryGetValue(paramType, out var dependencyLifetime))
            {
                if (dependencyLifetime == ServiceLifetime.Scoped)
                {
                    report.Warnings.Add(new DependencyWarning
                    {
                        ServiceName = FormatName(singletonDescriptor.ServiceType),
                        WarningType = "Captive Dependency",
                        Message = $"Singleton '{FormatName(singletonDescriptor.ServiceType)}' depends on Scoped service '{FormatName(paramType)}'. This will cause the Scoped service to stay alive forever."
                    });
                }
            }
        }
    }

    // --- Helper Methods ---

    private static bool IsSystemService(ServiceDescriptor descriptor)
    {
        // Check the Service Type (Interface)
        var serviceNs = descriptor.ServiceType.Namespace;
        bool serviceIsSystem = serviceNs != null && (serviceNs.StartsWith("System") || serviceNs.StartsWith("Microsoft"));

        // Check the Implementation Type (Your Class)
        bool implementationIsSystem = true; // Default to true for Factories/Instances we can't read
    
        if (descriptor.ImplementationType != null)
        {
            var implNs = descriptor.ImplementationType.Namespace;
            // If implementation is in user code (does NOT start with System/Microsoft), it's NOT a system service.
            implementationIsSystem = implNs != null && (implNs.StartsWith("System") || implNs.StartsWith("Microsoft"));
        }

        // It is a system service ONLY if the Interface is System AND the Implementation is System (or unknown).
        // If Interface is System (IHostedService) but Implementation is User (MyWorker), return false (Show it).
        return serviceIsSystem && implementationIsSystem;
    }

    private static string GetImplementationName(ServiceDescriptor descriptor)
    {
        // 1. ImplementationType (Standard Registration)
        if (descriptor.ImplementationType != null) 
        {
            return FormatName(descriptor.ImplementationType);
        }
    
        // 2. ImplementationInstance (e.g. services.AddSingleton(new MyObject()))
        if (descriptor.ImplementationInstance != null) 
        {
            return FormatName(descriptor.ImplementationInstance.GetType()) + " (Instance)";
        }
        
        // 3. ImplementationFactory (The "Black Box")
        if (descriptor.ImplementationFactory != null)
        {
            // TRY TO PEEK INSIDE
            var method = descriptor.ImplementationFactory.Method;
            var declaringType = method.DeclaringType?.Name;
        
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

    private static string FormatName(Type type)
    {
        // Makes generic types readable: List`1 becomes List<T>
        if (!type.IsGenericType) return type.Name;
        
        var name = type.Name.Split('`')[0];
        var args = string.Join(", ", type.GetGenericArguments().Select(a => a.Name));

        return $"{name}<{args}>";
    }
}