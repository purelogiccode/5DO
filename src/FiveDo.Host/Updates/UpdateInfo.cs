using System;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// Describes the newest release reported by the GitHub releases API.
/// </summary>
/// <param name="UpdateAvailable">Whether <paramref name="LatestVersion"/> is newer than the running build.</param>
/// <param name="CurrentVersion">Version of the running build.</param>
/// <param name="LatestVersion">Newest published release tag, without the leading <c>v</c>.</param>
/// <param name="ReleaseName">Human-readable release title.</param>
/// <param name="ReleasePageUrl">GitHub release page.</param>
/// <param name="DownloadUrl">Best matching downloadable asset, or the release page when no asset matches.</param>
/// <param name="PublishedAtUtc">Publication timestamp, when GitHub supplied one.</param>
/// <param name="AssetName">Selected asset file name, when one matched this platform.</param>
public sealed record UpdateInfo(
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleasePageUrl,
    string DownloadUrl,
    DateTimeOffset? PublishedAtUtc,
    string? AssetName)
{
    /// <summary>Gets a one-line notice suitable for a console or log message.</summary>
    public string Summary => UpdateAvailable
        ? $"{FiveDoVersion.ProductName} {LatestVersion} is available (you have {CurrentVersion})."
        : $"{FiveDoVersion.ProductName} {CurrentVersion} is up to date.";
}
