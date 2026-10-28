using System;
using FiveDo.Core;
using Serilog;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Serilog helpers shared by the hosts.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>Attaches the emulator subsystem category to a logger.</summary>
    /// <param name="logger">Source logger.</param>
    /// <param name="category">Subsystem category.</param>
    /// <returns>A logger that stamps the <c>Category</c> property.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    public static ILogger ForCategory(this ILogger logger, LogCategory category)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.ForContext("Category", category);
    }
}
