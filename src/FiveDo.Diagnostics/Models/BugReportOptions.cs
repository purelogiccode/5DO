using System;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Configuration for the bug-report HTTP sink.
/// </summary>
public sealed record BugReportOptions
{
    /// <summary>The bug-report submit endpoint from the service instructions.</summary>
    public const string DefaultEndpoint = "https://www.purelogiccode.com/bugreport/api/send-bug-report";

    /// <summary>The submit API key from the service instructions. Override with <c>FIVEDO_BUGREPORT_API_KEY</c>.</summary>
    public const string DefaultApiKey = "hjh7yu6t56tyr540o9u8767676r5674534453235264c75b6t7ggghgg76trf564e";

    /// <summary>Gets the submit endpoint.</summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>Gets the API key sent in the <c>X-API-KEY</c> header.</summary>
    public string ApiKey { get; init; } = DefaultApiKey;

    /// <summary>Gets the application mode shown in the report environment field (CLI, Desktop, Probe).</summary>
    public string ApplicationMode { get; init; } = "Desktop";

    /// <summary>Gets the maximum number of submissions per minute. The service allows 10.</summary>
    public int MaxRequestsPerMinute { get; init; } = 10;

    /// <summary>Gets the window in which an identical message is not submitted twice.</summary>
    public TimeSpan DedupeWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the bounded queue capacity. Oldest reports are dropped when full.</summary>
    public int QueueCapacity { get; init; } = 64;

    /// <summary>Gets the HTTP timeout for one submission.</summary>
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
