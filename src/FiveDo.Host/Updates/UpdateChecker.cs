using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// Checks the GitHub releases feed for a newer 5DO build.
/// </summary>
/// <remarks>
/// <para>
/// The check is best-effort and never throws. Successful responses are cached under the
/// 5DO data directory so a launch does not query GitHub more than once every few hours.
/// Set <c>FIVEDO_UPDATE_CHECK=0</c> (or pass <c>--no-update-check</c>) to opt out. CI
/// environments skip the check automatically.
/// </para>
/// <para>
/// Only the public release feed is requested. The request contains no user, machine,
/// game, or settings data; GitHub receives the usual HTTP metadata (IP address, client
/// user agent, and the requested URL).
/// </para>
/// </remarks>
public static class UpdateChecker
{
    /// <summary>GitHub repository that publishes 5DO releases.</summary>
    public const string Repository = "purelogiccode/5DO";

    /// <summary>Human-readable releases page.</summary>
    public const string ReleasesUrl = "https://github.com/purelogiccode/5DO/releases";

    /// <summary>Latest stable release page.</summary>
    public const string LatestReleaseUrl = "https://github.com/purelogiccode/5DO/releases/latest";

    /// <summary>GitHub REST endpoint for the latest stable release.</summary>
    public const string ReleaseApiUrl = "https://api.github.com/repos/purelogiccode/5DO/releases/latest";

    /// <summary>Default time between successful checks.</summary>
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(6);

    /// <summary>Default network timeout for a check.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>Gets a value indicating whether automatic update checks are enabled.</summary>
    public static bool IsEnabled
    {
        get
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("FIVEDO_UPDATE_CHECK"),
                    "0",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !IsTruthy("CI") && !IsTruthy("GITHUB_ACTIONS") && !IsTruthy("TF_BUILD");
        }
    }

    /// <summary>Gets the default cache path under the active data directory.</summary>
    public static string DefaultCachePath =>
        Path.Combine(PortablePaths.DataDirectory, "update-check.json");

    /// <summary>Checks GitHub for the latest release.</summary>
    /// <param name="force">Ignore a fresh cache and query GitHub again.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The check result; failures are reported through <see cref="UpdateCheckResult.Error"/>.</returns>
    public static Task<UpdateCheckResult> CheckAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        return CheckAsync(
            new UpdateCheckOptions { Force = force },
            Http,
            FiveDoVersion.Version,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    /// <summary>Checks GitHub using explicit options. Intended for tests.</summary>
    /// <param name="options">Check options.</param>
    /// <param name="httpClient">HTTP client used to request the release feed.</param>
    /// <param name="currentVersion">Running version.</param>
    /// <param name="now">Current timestamp used for cache freshness.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The check result.</returns>
    internal static async Task<UpdateCheckResult> CheckAsync(
        UpdateCheckOptions options,
        HttpClient httpClient,
        string currentVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(currentVersion);

        UpdateCacheEntry? cached = TryReadCache(options.CachePath);
        if (!options.Force && IsFresh(cached, now, options.CacheDuration))
        {
            return new UpdateCheckResult(CreateInfo(cached!, currentVersion), null, FromCache: true);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Serilog.Log.Debug(
                    "Update check returned HTTP {Status}; using the cached result when available.",
                    (int)response.StatusCode);
                return Fallback(cached, currentVersion, $"GitHub returned HTTP {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            UpdateCacheEntry entry = ParseRelease(json, now);
            TryWriteCache(options.CachePath, entry);
            return new UpdateCheckResult(CreateInfo(entry, currentVersion), null, FromCache: false);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or JsonException
            or InvalidOperationException
            or NotSupportedException
            or FormatException)
        {
            Serilog.Log.Debug(exception, "Update check failed.");
            return Fallback(cached, currentVersion, exception.Message);
        }
        catch (IOException exception)
        {
            Serilog.Log.Debug(exception, "Update check cache read/write failed.");
            return Fallback(cached, currentVersion, exception.Message);
        }
    }

    private static UpdateCheckResult Fallback(
        UpdateCacheEntry? cached,
        string currentVersion,
        string error)
    {
        return cached is null
            ? new UpdateCheckResult(null, error, FromCache: false)
            : new UpdateCheckResult(CreateInfo(cached, currentVersion), null, FromCache: true);
    }

    private static UpdateInfo CreateInfo(UpdateCacheEntry entry, string currentVersion)
    {
        var updateAvailable = ReleaseVersion.TryParse(entry.LatestVersion, out var latest)
                              && ReleaseVersion.TryParse(currentVersion, out var current)
                              && latest > current;
        return new UpdateInfo(
            updateAvailable,
            currentVersion,
            entry.LatestVersion,
            entry.ReleaseName,
            entry.ReleasePageUrl,
            entry.DownloadUrl,
            entry.PublishedAtUtc,
            entry.AssetName);
    }

    private static bool IsFresh(UpdateCacheEntry? entry, DateTimeOffset now, TimeSpan duration)
    {
        if (entry is null)
        {
            return false;
        }

        var age = now - entry.CheckedAtUtc;
        return age >= TimeSpan.Zero && age <= duration;
    }

    private static UpdateCacheEntry ParseRelease(string json, DateTimeOffset now)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        var tag = GetString(root, "tag_name");
        if (!ReleaseVersion.TryParse(tag, out ReleaseVersion latest))
        {
            throw new FormatException($"The release tag '{tag}' is not a recognized version.");
        }

        var releaseName = GetString(root, "name");
        if (string.IsNullOrWhiteSpace(releaseName))
        {
            releaseName = $"5DO {latest}";
        }

        var releasePage = GetString(root, "html_url");
        if (!IsHttpUrl(releasePage))
        {
            releasePage = LatestReleaseUrl;
        }

        var (assetName, downloadUrl) = SelectAsset(root);
        DateTimeOffset? publishedAt = null;
        if (DateTimeOffset.TryParse(
                GetString(root, "published_at"),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var published))
        {
            publishedAt = published.ToUniversalTime();
        }

        return new UpdateCacheEntry
        {
            CheckedAtUtc = now,
            LatestVersion = latest.ToString(),
            ReleaseName = releaseName,
            ReleasePageUrl = releasePage,
            DownloadUrl = string.IsNullOrWhiteSpace(downloadUrl) ? releasePage : downloadUrl,
            PublishedAtUtc = publishedAt,
            AssetName = assetName,
        };
    }

    private static (string? Name, string? Url) SelectAsset(JsonElement root)
    {
        var token = PlatformAssetToken;
        if (token is null || !root.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            var name = GetString(asset, "name");
            var url = GetString(asset, "browser_download_url");
            if (!string.IsNullOrWhiteSpace(name)
                && IsHttpUrl(url)
                && name.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return (name, url);
            }
        }

        return (null, null);
    }

    private static string? PlatformAssetToken
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "win-x64";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux-x64";
            }

            return OperatingSystem.IsMacOS() ? "osx-x64" : null;
        }
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out JsonElement value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool IsHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
               && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                   || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal));
    }

    private static UpdateCacheEntry? TryReadCache(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            UpdateCacheEntry? entry = JsonSerializer.Deserialize(
                json,
                UpdateCacheJsonContext.Default.UpdateCacheEntry);
            return string.IsNullOrWhiteSpace(entry?.LatestVersion) ? null : entry;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Update check cache could not be read.");
            return null;
        }
    }

    private static void TryWriteCache(string? path, UpdateCacheEntry entry)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(entry, UpdateCacheJsonContext.Default.UpdateCacheEntry));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Update check cache could not be written.");
        }
    }

    private static bool IsTruthy(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null
               && !string.Equals(value, "0", StringComparison.Ordinal)
               && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("5DO-UpdateChecker/1.0");
        return client;
    }
}

/// <summary>Options for one update check.</summary>
public sealed record UpdateCheckOptions
{
    /// <summary>Gets a value indicating whether a fresh cache is ignored.</summary>
    public bool Force { get; init; }

    /// <summary>Gets the cache file path, or <see langword="null"/> to disable caching.</summary>
    public string? CachePath { get; init; } = UpdateChecker.DefaultCachePath;

    /// <summary>Gets the time that a successful result remains fresh.</summary>
    public TimeSpan CacheDuration { get; init; } = UpdateChecker.DefaultCacheDuration;

    /// <summary>Gets the network timeout.</summary>
    public TimeSpan Timeout { get; init; } = UpdateChecker.DefaultTimeout;
}

/// <summary>Cached GitHub release data.</summary>
public sealed record UpdateCacheEntry
{
    /// <summary>Gets or sets the UTC timestamp of the successful check.</summary>
    public DateTimeOffset CheckedAtUtc { get; set; }

    /// <summary>Gets or sets the latest version, without the leading <c>v</c>.</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the release title.</summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>Gets or sets the release page URL.</summary>
    public string ReleasePageUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the preferred download URL.</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the publication timestamp.</summary>
    public DateTimeOffset? PublishedAtUtc { get; set; }

    /// <summary>Gets or sets the selected asset name.</summary>
    public string? AssetName { get; set; }
}
