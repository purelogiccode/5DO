using System;

namespace FiveDo.Diagnostics.Donations;

/// <summary>The outcome of a donation verification request.</summary>
public enum DonationVerificationStatus
{
    /// <summary>A completed donation was confirmed for the application and email.</summary>
    Donated,

    /// <summary>The server responded and found no completed donation.</summary>
    NotDonated,

    /// <summary>The server could not be reached or returned an unexpected response.</summary>
    Unavailable,
}

/// <summary>Details returned by a successful donation verification request.</summary>
public sealed record DonationVerificationResult(
    DonationVerificationStatus Status,
    decimal TotalAmount = 0m,
    DateTime? LastDonationAtUtc = null);
