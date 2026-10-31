using System;
using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Donations;

/// <summary>
/// Response returned by the PureLogicCode donation verification API.
/// </summary>
public sealed class DonationVerificationResponse
{
    /// <summary>Gets or sets a value indicating whether a completed donation was found.</summary>
    [JsonPropertyName("donated")]
    public bool Donated { get; set; }

    /// <summary>Gets or sets the application slug that was verified.</summary>
    [JsonPropertyName("applicationSlug")]
    public string ApplicationSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the total recognized donation amount.</summary>
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the most recent donation.</summary>
    [JsonPropertyName("lastDonationAtUtc")]
    public DateTime? LastDonationAtUtc { get; set; }
}
