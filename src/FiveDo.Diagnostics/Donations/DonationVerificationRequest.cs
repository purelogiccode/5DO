using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Donations;

/// <summary>
/// Request body sent to the PureLogicCode donation verification API.
/// </summary>
public sealed class DonationVerificationRequest
{
    /// <summary>Gets or sets the email address used to look up donations.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the donationware application slug.</summary>
    [JsonPropertyName("applicationSlug")]
    public string ApplicationSlug { get; set; } = string.Empty;
}
