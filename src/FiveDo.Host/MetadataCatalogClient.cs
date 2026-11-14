using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Host;

/// <summary>One external metadata lookup result.</summary>
/// <param name="Record">Best matching record, or <see langword="null"/>.</param>
/// <param name="Matches">All matching records, best first.</param>
/// <param name="FromCache">Whether the catalog came from the local cache.</param>
/// <param name="Warning">Non-fatal download or cache message, or <see langword="null"/>.</param>
public sealed record MetadataLookupResult(
    LibraryMetadataRecord? Record,
    IReadOnlyList<LibraryMetadataRecord> Matches,
    bool FromCache,
    string? Warning);

/// <summary>
/// Looks up game metadata in an external library-metadata JSON catalog and keeps a
/// deterministic local cache. The cache is used when the network request fails, so a
/// metadata lookup never blocks the library UI.
/// </summary>
/// <remarks>
/// The catalog uses the same schema as <see cref="LibraryMetadata"/>. A <c>file:</c> URI
/// is supported for offline catalogs and tests. The source is read-only; 5DO never
/// uploads library, game, or user data.
/// </remarks>
public sealed class MetadataCatalogClient : IDisposable
{
    /// <summary>Gets the largest accepted catalog size.</summary>
    public const int MaxCatalogBytes = 8 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private bool _disposed;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">Optional HTTP client; a configured client is created when omitted.</param>
    public MetadataCatalogClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
            _http = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("5DO-MetadataLookup/1.0");
            _ownsClient = true;
        }
        else
        {
            _http = httpClient;
            _ownsClient = false;
        }
    }

    /// <summary>Gets the default cache path.</summary>
    public static string DefaultCachePath =>
        Path.Combine(AppSettingsStore.DefaultDirectory, "metadata-cache.json");

    /// <summary>Downloads, caches, and searches an external metadata catalog.</summary>
    /// <param name="title">Game title to match.</param>
    /// <param name="gameId">Optional 4DO game ID for an exact match.</param>
    /// <param name="url">Catalog URL or file URI. When empty, only the cache is searched.</param>
    /// <param name="cachePath">Cache file. Defaults to <see cref="DefaultCachePath"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching records and any fallback warning.</returns>
    public async Task<MetadataLookupResult> LookupAsync(
        string title,
        string? gameId,
        string? url,
        string? cachePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var resolvedCache = string.IsNullOrWhiteSpace(cachePath) ? DefaultCachePath : cachePath;
        string? warning = null;
        LibraryMetadata? catalog = null;
        var fromCache = false;

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                catalog = await DownloadAsync(url, cancellationToken).ConfigureAwait(false);
                SaveCache(catalog, resolvedCache);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or HttpRequestException
                or TaskCanceledException
                or InvalidDataException
                or ArgumentException)
            {
                warning = $"The external metadata catalog was unavailable: {exception.Message}";
                Serilog.Log.Debug(exception, "External metadata lookup failed for {Url}.", url);
            }
        }

        if (catalog is null)
        {
            catalog = LoadCache(resolvedCache);
            fromCache = catalog is not null;
            warning ??= catalog is null
                ? "No external metadata catalog or cache is available."
                : null;
        }

        if (catalog is null)
        {
            return new MetadataLookupResult(null, [], false, warning);
        }

        LibraryMetadataRecord[] matches = Search(catalog, title, gameId);
        return new MetadataLookupResult(
            matches.Length > 0 ? matches[0] : null,
            matches,
            fromCache,
            warning);
    }

    /// <summary>Adds media paths to a game's metadata record.</summary>
    /// <param name="gameId">4DO game ID, or empty.</param>
    /// <param name="discPath">Disc path, or empty.</param>
    /// <param name="mediaPaths">Media files to attach.</param>
    /// <param name="metadataPath">Metadata file. Defaults to <see cref="LibraryMetadataStore.DefaultPath"/>.</param>
    /// <returns>The updated record, or <see langword="null"/> when no record exists.</returns>
    public static LibraryMetadataRecord? AttachMedia(
        string? gameId,
        string? discPath,
        IEnumerable<string> mediaPaths,
        string? metadataPath = null)
    {
        ArgumentNullException.ThrowIfNull(mediaPaths);
        LibraryMetadata metadata = LibraryMetadataStore.Load(metadataPath);
        LibraryMetadataRecord? record = metadata.Records.FirstOrDefault(item => Matches(item, gameId, discPath));
        if (record is null)
        {
            return null;
        }

        foreach (var path in mediaPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!record.Media.Any(existing => string.Equals(
                    existing,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase)))
            {
                record.Media.Add(fullPath);
            }
        }

        LibraryMetadataStore.Upsert(record, metadataPath);
        return record;
    }

    /// <summary>Removes media paths from a game's metadata record.</summary>
    /// <param name="gameId">4DO game ID, or empty.</param>
    /// <param name="discPath">Disc path, or empty.</param>
    /// <param name="mediaPaths">Media files to detach.</param>
    /// <param name="metadataPath">Metadata file. Defaults to <see cref="LibraryMetadataStore.DefaultPath"/>.</param>
    /// <returns>The updated record, or <see langword="null"/> when no record exists.</returns>
    public static LibraryMetadataRecord? DetachMedia(
        string? gameId,
        string? discPath,
        IEnumerable<string> mediaPaths,
        string? metadataPath = null)
    {
        ArgumentNullException.ThrowIfNull(mediaPaths);
        LibraryMetadata metadata = LibraryMetadataStore.Load(metadataPath);
        LibraryMetadataRecord? record = metadata.Records.FirstOrDefault(item => Matches(item, gameId, discPath));
        if (record is null)
        {
            return null;
        }

        foreach (var path in mediaPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            for (var index = record.Media.Count - 1; index >= 0; index--)
            {
                if (string.Equals(record.Media[index], fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    record.Media.RemoveAt(index);
                }
            }
        }

        LibraryMetadataStore.Upsert(record, metadataPath);
        return record;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    private static bool Matches(LibraryMetadataRecord record, string? gameId, string? discPath)
    {
        var normalizedId = gameId?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedId.Length > 0
            && string.Equals(record.GameId.Trim(), normalizedId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(discPath))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(record.Path),
                Path.GetFullPath(discPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static LibraryMetadataRecord[] Search(
        LibraryMetadata catalog,
        string title,
        string? gameId)
    {
        var normalizedId = gameId?.Trim().ToUpperInvariant() ?? string.Empty;
        var query = NormalizeTitle(title);
        var scored = new List<(LibraryMetadataRecord Record, int Score)>();
        foreach (LibraryMetadataRecord record in catalog.Records)
        {
            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (record is null)
            {
                continue;
            }

            var byId = normalizedId.Length > 0
                && string.Equals(record.GameId.Trim(), normalizedId, StringComparison.OrdinalIgnoreCase);
            var candidate = NormalizeTitle(
                string.IsNullOrWhiteSpace(record.Title) ? Path.GetFileNameWithoutExtension(record.Path) : record.Title);
            var byTitle = query.Length > 0 && string.Equals(candidate, query, StringComparison.Ordinal);
            var byContains = query.Length > 0
                && (candidate.Contains(query, StringComparison.Ordinal)
                    || query.Contains(candidate, StringComparison.Ordinal));
            if (!byId && !byTitle && !byContains)
            {
                continue;
            }

            var score = byId ? 3 : byTitle ? 2 : 1;
            scored.Add((record, score));
        }

        return scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Record.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Record.GameId, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(item => item.Record)
            .ToArray();
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);
        foreach (var character in title.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<LibraryMetadata> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException($"'{url}' is not an absolute metadata catalog URL.", nameof(url));
        }

        byte[] bytes;
        if (uri.IsFile)
        {
            var info = new FileInfo(uri.LocalPath);
            if (info.Length > MaxCatalogBytes)
            {
                throw new InvalidDataException($"The metadata catalog exceeds {MaxCatalogBytes} bytes.");
            }

            bytes = await File.ReadAllBytesAsync(info.FullName, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using HttpResponseMessage response = await _http
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxCatalogBytes)
            {
                throw new InvalidDataException($"The metadata catalog exceeds {MaxCatalogBytes} bytes.");
            }

            using Stream stream = await response.Content
                .ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxCatalogBytes)
                {
                    throw new InvalidDataException($"The metadata catalog exceeds {MaxCatalogBytes} bytes.");
                }

                buffer.Write(chunk, 0, read);
            }

            bytes = buffer.ToArray();
        }

        return Parse(bytes);
    }

    private static LibraryMetadata Parse(byte[] bytes)
    {
        LibraryMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize(
                bytes,
                LibraryMetadataJsonContext.Default.LibraryMetadata) ?? new LibraryMetadata();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The external metadata catalog is not valid JSON.", exception);
        }

        if (metadata.Version != LibraryMetadata.CurrentVersion)
        {
            throw new InvalidDataException(
                $"The external metadata catalog uses unsupported version {metadata.Version}.");
        }

        // JSON can deserialize null despite the non-nullable annotation.
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        metadata.Records ??= [];
        foreach (LibraryMetadataRecord record in metadata.Records)
        {
            // JSON can deserialize null despite the non-nullable annotation.
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (record is null)
            {
                continue;
            }

            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            record.Media ??= [];
        }

        return metadata;
    }

    private static void SaveCache(LibraryMetadata catalog, string cachePath)
    {
        var json = JsonSerializer.Serialize(
            catalog,
            LibraryMetadataJsonContext.Default.LibraryMetadata);
        if (!AtomicFile.WriteAllText(cachePath, json, "metadata cache"))
        {
            Serilog.Log.Debug("Metadata cache {Path} could not be written.", cachePath);
        }
    }

    private static LibraryMetadata? LoadCache(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath))
            {
                return null;
            }

            if (new FileInfo(cachePath).Length > MaxCatalogBytes)
            {
                return null;
            }

            return Parse(File.ReadAllBytes(cachePath));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            Serilog.Log.Debug(exception, "Metadata cache {Path} is unreadable; ignoring it.", cachePath);
            return null;
        }
    }
}
