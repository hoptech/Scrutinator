namespace Scrutinator.Core.Log;

using Microsoft.Extensions.Logging;

// This delegate takes the method name and arguments and returns the mapped Level & Message.
// Returning 'null' tells the proxy to ignore/skip this log.
public delegate (LogLevel Level, string Message)? CustomLogMapper(string methodName, object?[] args);