using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// Shared defaults for the online cover-art catalog.
/// </summary>
public static class CoverDefaults
{
    /// <summary>The public base URL that hosts the cover catalog and images.</summary>
    public const string BaseUrl = "https://assets.purelogiccode.com/5do/v1";

    /// <summary>The default catalog URL.</summary>
    public const string CatalogUrl = BaseUrl + "/catalog.json";

    /// <summary>The catalog schema version understood by this build.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The default Jaro-Winkler threshold for fuzzy cover matching.</summary>
    public const double DefaultFuzzyThreshold = 0.85;

    /// <summary>The smallest selectable fuzzy threshold.</summary>
    public const double MinFuzzyThreshold = 0.70;

    /// <summary>The largest selectable fuzzy threshold.</summary>
    public const double MaxFuzzyThreshold = 0.95;

    /// <summary>The maximum accepted catalog size.</summary>
    internal const int MaxCatalogBytes = 5 * 1024 * 1024;

    /// <summary>The maximum accepted image size.</summary>
    internal const int MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>The maximum accepted bulk-pack size.</summary>
    internal const long MaxPackBytes = 512L * 1024 * 1024;

    /// <summary>Gets the default cover-cache root shared by all profiles.</summary>
    public static string DefaultCacheDirectory => Path.Combine(PortablePaths.BaseDataDirectory, "covers");

    /// <summary>Clamps a fuzzy threshold to the supported range.</summary>
    /// <param name="value">Requested threshold.</param>
    /// <returns>A finite value between <see cref="MinFuzzyThreshold"/> and <see cref="MaxFuzzyThreshold"/>.</returns>
    public static double ClampFuzzyThreshold(double value)
    {
        if (!double.IsFinite(value))
        {
            return DefaultFuzzyThreshold;
        }

        return Math.Clamp(value, MinFuzzyThreshold, MaxFuzzyThreshold);
    }

    /// <summary>Normalizes a catalog URL or base URL.</summary>
    /// <param name="value">Configured URL or base URL.</param>
    /// <returns>The absolute catalog URL to use.</returns>
    public static string NormalizeCatalogUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CatalogUrl;
        }

        var text = value.Trim();
        // The custom domain serves the bucket root. Accept the R2-style path that
        // includes the bucket name as a convenience for copy/pasted URLs.
        text = text.Replace(
            "/simplelauncher-thumbnails/",
            "/",
            StringComparison.OrdinalIgnoreCase);
        if (!text.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            text = text.TrimEnd('/') + "/catalog.json";
        }

        return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
               && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                   || uri.IsFile)
            ? uri.AbsoluteUri
            : CatalogUrl;
    }

    /// <summary>Gets a value indicating whether a catalog URI is local.</summary>
    /// <param name="uri">Catalog URI.</param>
    /// <returns><see langword="true"/> for file URIs.</returns>
    public static bool IsLocal(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IsFile;
    }
}
