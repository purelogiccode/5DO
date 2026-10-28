using System;
using System.Threading;
using Serilog;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Owns the process-wide Serilog logger configured by a host entry point.
/// </summary>
/// <remarks>
/// Serilog's static <see cref="Serilog.Log"/> facade is the routing point used by libraries
/// that do not receive a logger instance. The CLI and desktop hosts call
/// <see cref="Initialize"/> once at startup and <see cref="Shutdown"/> once at exit.
/// </remarks>
public static class AppLogging
{
    private static readonly Lock Gate = new();
    private static InMemorySerilogSink? _sink;
    private static bool _initialized;

    /// <summary>Gets the in-memory sink used by the desktop log viewer, or <see langword="null"/> before startup.</summary>
    public static InMemorySerilogSink? InMemorySink
    {
        get
        {
            lock (Gate)
            {
                return _sink;
            }
        }
    }

    /// <summary>Configures the process-wide logger.</summary>
    /// <param name="options">Host logging options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static void Initialize(LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            var logger = LoggingSetup.CreateLogger(options, out InMemorySerilogSink sink);
            Serilog.Log.Logger = logger;
            _sink = sink;
            _initialized = true;
        }

        Serilog.Log.Information("Logging initialized ({ApplicationMode}).", options.ApplicationMode);
    }

    /// <summary>Flushes and disposes the process-wide logger. Safe to call more than once.</summary>
    public static void Shutdown()
    {
        lock (Gate)
        {
            if (!_initialized)
            {
                return;
            }

            _initialized = false;
            _sink = null;
        }

        Serilog.Log.CloseAndFlush();
    }
}
