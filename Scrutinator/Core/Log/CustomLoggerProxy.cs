namespace Scrutinator.Core.Log;

using System.Reflection;
using Microsoft.Extensions.Logging;

public class CustomLoggerProxy<TInterface> : DispatchProxy where TInterface : class
{
    private TInterface _realLogger = null!;
    private LogHub _hub = null!;
    private string _category = "CustomLog";
    private CustomLogMapper _mapper = null!;

    // Factory method to create the proxy
    public static TInterface Create(TInterface realLogger, LogHub hub, string category, CustomLogMapper mapper)
    {
        object proxy = Create<TInterface, CustomLoggerProxy<TInterface>>();
        var customProxy = (CustomLoggerProxy<TInterface>)proxy;

        customProxy._realLogger = realLogger;
        customProxy._hub = hub;
        customProxy._category = category;
        customProxy._mapper = mapper;

        return (TInterface)proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var result = targetMethod?.Invoke(_realLogger, args);

        // Intercept and parse using the provided mapper
        if (targetMethod != null && args != null)
        {
            try
            {
                var logData = _mapper(targetMethod.Name, args);
                if (logData.HasValue)
                {
                    _hub.PushLog(_category, logData.Value.Level, logData.Value.Message);
                }
            }
            catch
            { 
                // Never crash the app because of a Scrutinator interception failure
            }
        }

        return result;
    }

    // private void InterceptLog(string methodName, object?[] args)
    // {
    //     // Usually, the message is the first string argument
    //     var message = args.FirstOrDefault(a => a is string) as string;
    //     if (string.IsNullOrEmpty(message)) return;
    //
    //     // Extract Exception if present
    //     if (args.FirstOrDefault(a => a is Exception) is Exception ex)
    //     {
    //         message += $"\n{ex.GetType().Name}: {ex.Message}";
    //     }
    //
    //     // Map method names to standard LogLevels
    //     var level = methodName.ToLower() switch
    //     {
    //         "verbose" => LogLevel.Trace,
    //         "info" => LogLevel.Information,
    //         "warning" => LogLevel.Warning,
    //         "error" => LogLevel.Error,
    //         "fatal" => LogLevel.Critical,
    //         _ => LogLevel.Information
    //     };
    //
    //     _hub.PushLog(_category, level, message);
    // }

    public static (LogLevel Level, string Message)? DefaultMapper(string methodName, object?[] args)
    {
        var message = args.FirstOrDefault(a => a is string) as string;
        if (string.IsNullOrEmpty(message)) return null;

        if (args.FirstOrDefault(a => a is Exception) is Exception ex)
        {
            message += $"\n{ex.GetType().Name}: {ex.Message}";
        }

        var level = methodName.ToLower() switch
        {
            "verbose" => LogLevel.Trace,
            "info" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "fatal" => LogLevel.Critical,
            _ => LogLevel.Information
        };

        return (level, message);
    }
}