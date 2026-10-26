namespace FiveDo.Core;

/// <summary>
/// Diagnostic log severity.
/// </summary>
public enum LogLevel
{
    /// <summary>Highly detailed tracing, usually disabled.</summary>
    Trace = 0,

    /// <summary>Debug detail useful while porting a subsystem.</summary>
    Debug = 1,

    /// <summary>Normal lifecycle information.</summary>
    Info = 2,

    /// <summary>A recoverable anomaly.</summary>
    Warning = 3,

    /// <summary>An operation failed but the emulator can continue.</summary>
    Error = 4,

    /// <summary>A fatal failure.</summary>
    Critical = 5,

    /// <summary>Disables logging when used as a minimum level.</summary>
    None = 6,
}
