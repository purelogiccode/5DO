namespace FiveDo.Diagnostics.Donations;

/// <summary>
/// Configuration for the donation verification API client.
/// </summary>
public sealed record DonationVerificationOptions
{
    /// <summary>The default verification endpoint.</summary>
    public const string DefaultEndpoint = "https://www.purelogiccode.com/api/donations/verify";

    /// <summary>The shared client API key configured by the service.</summary>
    public const string DefaultApiKey = "hjh7yu6t56tyr540o9u8767676r5674534453235264c75b6t7ggghgg76trf564e";

    /// <summary>The application slug registered for 5DO on the website.</summary>
    public const string DefaultApplicationSlug = "5do";

    /// <summary>Gets the verification endpoint.</summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    /// <summary>Gets the API key sent in the <c>X-API-KEY</c> header.</summary>
    public string ApiKey { get; init; } = DefaultApiKey;

    /// <summary>Gets the donationware application slug.</summary>
    public string ApplicationSlug { get; init; } = DefaultApplicationSlug;
}
