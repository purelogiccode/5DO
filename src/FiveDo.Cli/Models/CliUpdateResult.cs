using System;

namespace FiveDo.Cli;

/// <summary>Result of the <c>update</c> command.</summary>
/// <param name="Ok">Whether the release feed was read successfully.</param>
/// <param name="Error">Failure message, or <see langword="null"/>.</param>
/// <param name="CurrentVersion">Running build version.</param>
/// <param name="LatestVersion">Latest published version, or <see langword="null"/> on failure.</param>
/// <param name="UpdateAvailable">Whether a newer build exists.</param>
/// <param name="ReleaseName">Release title, or <see langword="null"/>.</param>
/// <param name="ReleasePageUrl">GitHub release page, or <see langword="null"/>.</param>
/// <param name="DownloadUrl">Download asset or release page, or <see langword="null"/>.</param>
/// <param name="PublishedAtUtc">Publication timestamp, when supplied by GitHub.</param>
/// <param name="FromCache">Whether the answer came from the local cache.</param>
internal sealed record CliUpdateResult(
    bool Ok,
    string? Error,
    string CurrentVersion,
    string? LatestVersion,
    bool UpdateAvailable,
    string? ReleaseName,
    string? ReleasePageUrl,
    string? DownloadUrl,
    DateTimeOffset? PublishedAtUtc,
    bool FromCache);
