using System;
using System.Threading.Tasks;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Runs asynchronous UI event handlers without letting an unhandled exception escape
/// the dispatcher loop and terminate the process.
/// </summary>
internal static class AsyncEvent
{
    /// <summary>Starts an asynchronous operation and logs any exception it raises.</summary>
    /// <param name="operation">Operation to run.</param>
    /// <param name="description">Description used in the log message.</param>
    public static void Run(Func<Task> operation, string description)
    {
        _ = RunCoreAsync(operation, description);
    }

    private static async Task RunCoreAsync(Func<Task> operation, string description)
    {
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "Unhandled exception while {Operation}.", description);
        }
    }
}
