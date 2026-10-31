using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Donations;

/// <summary>Source-generated JSON metadata for donation verification messages.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(DonationVerificationRequest))]
[JsonSerializable(typeof(DonationVerificationResponse))]
internal sealed partial class DonationVerificationJsonContext : JsonSerializerContext;
