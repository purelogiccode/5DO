using FiveDo.Core;

namespace FiveDo.Diagnostics.Statistics;

/// <summary>
/// Configuration for the ApplicationStats launch counter.
/// </summary>
public sealed record ApplicationStatsOptions
{
    /// <summary>The ApplicationStats submit endpoint.</summary>
    public const string DefaultEndpoint = "https://www.purelogiccode.com/ApplicationStats/stats";

    /// <summary>The client API key from the ApplicationStats service instructions.</summary>
    public const string DefaultApiKey = "hjh7yu6t56tyr540o9u8767676r5674534453235264c75b6t7ggghgg76trf564e";

    /// <summary>The normalized application identifier used by the dashboard.</summary>
    public const string DefaultApplicationId = "5do";

    /// <summary>Gets the submit endpoint.</summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>Gets the Bearer API key.</summary>
    public string ApiKey { get; init; } = DefaultApiKey;

    /// <summary>Gets the application identifier.</summary>
    public string ApplicationId { get; init; } = DefaultApplicationId;

    /// <summary>Gets the application version.</summary>
    public string Version { get; init; } = FiveDoVersion.Version;
}
