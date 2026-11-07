using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Host;

/// <summary>
/// Content-addressed local cache for online cover images and the catalog.
/// </summary>
/// <remarks>
/// Images are stored as <c>images/&lt;sha256&gt;.webp</c>. Downloads are written to
/// <c>tmp/</c> first, verified, and then moved into place so a partial file is never
/// exposed to the library.
/// </remarks>
public sealed class CoverStore
{
    private const int BufferSize = 64 * 1024;

    private readonly string _rootDirectory;

    /// <summary>Creates a store rooted at the default or supplied directory.</summary>
    /// <param name="directory">Optional cache root; defaults to <see cref="DefaultDirectory"/>.</param>
    public CoverStore(string? directory = null)
    {
        _rootDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory);
    }

    /// <summary>Gets the default cover-cache root.</summary>
    public static string DefaultDirectory => CoverDefaults.DefaultCacheDirectory;

    /// <summary>Gets the cache root directory.</summary>
    public string RootDirectory => _rootDirectory;

    /// <summary>Gets the cached catalog path.</summary>
    public string CatalogPath => Path.Combine(_rootDirectory, "catalog.json");

    /// <summary>Gets the cached catalog metadata path.</summary>
    public string CatalogMetadataPath => Path.Combine(_rootDirectory, "catalog.meta.json");

    /// <summary>Gets the image directory.</summary>
    public string ImagesDirectory => Path.Combine(_rootDirectory, "images");

    /// <summary>Gets the temporary download directory.</summary>
    public string TempDirectory => Path.Combine(_rootDirectory, "tmp");

    /// <summary>Gets the bulk-pack directory.</summary>
    public string PacksDirectory => Path.Combine(_rootDirectory, "packs");

    /// <summary>Gets the cache path for one content-addressed image.</summary>
    /// <param name="id">SHA-256 identifier.</param>
    /// <returns>The absolute image path.</returns>
    /// <exception cref="ArgumentException">Thrown when the identifier is not a SHA-256 hash.</exception>
    public string GetImagePath(string id)
    {
        ValidateId(id);
        return Path.Combine(ImagesDirectory, id.ToLowerInvariant() + ".webp");
    }

    /// <summary>Checks whether an image is cached.</summary>
    /// <param name="id">SHA-256 identifier.</param>
    /// <param name="path">Cached path when present.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    public bool TryGetImagePath(string id, out string path)
    {
        try
        {
            path = GetImagePath(id);
            return File.Exists(path);
        }
        catch (ArgumentException)
        {
            path = string.Empty;
            return false;
        }
    }

    /// <summary>Checks whether an image is cached.</summary>
    /// <param name="id">SHA-256 identifier.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    public bool HasImage(string id)
    {
        return TryGetImagePath(id, out _);
    }

    /// <summary>Writes the catalog and its cache sidecar atomically.</summary>
    /// <param name="json">Raw catalog JSON.</param>
    /// <param name="sourceUri">Catalog source URI.</param>
    /// <param name="etag">Optional HTTP ETag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveCatalogAsync(
        string json,
        Uri sourceUri,
        string? etag,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(sourceUri);
        Directory.CreateDirectory(_rootDirectory);
        await WriteTextAtomicAsync(CatalogPath, json, cancellationToken).ConfigureAwait(false);
        var metadata = new CoverCatalogCacheMetadata
        {
            SourceUrl = sourceUri.AbsoluteUri,
            ETag = etag ?? string.Empty,
            FetchedUtc = DateTimeOffset.UtcNow,
        };
        var metadataJson = JsonSerializer.Serialize(
            metadata,
            CoverCatalogJsonContext.Default.CoverCatalogCacheMetadata);
        await WriteTextAtomicAsync(CatalogMetadataPath, metadataJson, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads the cached catalog and sidecar when both are readable.</summary>
    /// <param name="json">Raw catalog JSON.</param>
    /// <param name="metadata">Cache sidecar.</param>
    /// <returns><see langword="true"/> when a catalog is cached.</returns>
    public bool TryLoadCatalog(out string json, out CoverCatalogCacheMetadata metadata)
    {
        json = string.Empty;
        metadata = new CoverCatalogCacheMetadata();
        try
        {
            if (!File.Exists(CatalogPath))
            {
                return false;
            }

            json = File.ReadAllText(CatalogPath);
            if (File.Exists(CatalogMetadataPath))
            {
                metadata = JsonSerializer.Deserialize(
                    File.ReadAllText(CatalogMetadataPath),
                    CoverCatalogJsonContext.Default.CoverCatalogCacheMetadata)
                    ?? new CoverCatalogCacheMetadata();
            }

            return json.Length > 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            Serilog.Log.Debug(exception, "Cover catalog cache could not be read.");
            json = string.Empty;
            metadata = new CoverCatalogCacheMetadata();
            return false;
        }
    }

    /// <summary>Writes a downloaded image stream into the cache after verifying its hash.</summary>
    /// <param name="id">Expected SHA-256 identifier.</param>
    /// <param name="source">Image stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional byte progress.</param>
    /// <returns>The cached image path.</returns>
    public async Task<string> SaveImageAsync(
        string id,
        Stream source,
        CancellationToken cancellationToken,
        IProgress<long>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateId(id);
        id = id.ToLowerInvariant();
        Directory.CreateDirectory(ImagesDirectory);
        Directory.CreateDirectory(TempDirectory);
        var temporary = Path.Combine(TempDirectory, Guid.NewGuid().ToString("N") + ".part");
        try
        {
            var bytes = 0L;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                using (var output = new FileStream(
                                 temporary,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 BufferSize,
                                 useAsync: true))
                {
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        bytes += read;
                        if (bytes > CoverDefaults.MaxImageBytes)
                        {
                            throw new InvalidDataException("The cover image exceeds the maximum allowed size.");
                        }

                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        progress?.Report(bytes);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(actual, id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The cover image hash {actual} does not match the catalog hash {id}.");
            }

            var destination = GetImagePath(id);
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Writes already-loaded image bytes into the cache after verifying the hash.</summary>
    /// <param name="id">Expected SHA-256 identifier.</param>
    /// <param name="bytes">Image bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Optional byte progress.</param>
    /// <returns>The cached image path.</returns>
    public Task<string> SaveImageAsync(
        string id,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken,
        IProgress<long>? progress = null)
    {
        return SaveImageAsync(
            id,
            new MemoryStream(bytes.ToArray(), writable: false),
            cancellationToken,
            progress);
    }

    /// <summary>Verifies every cached image referenced by a catalog.</summary>
    /// <param name="catalog">Catalog to verify.</param>
    /// <param name="fix">Whether corrupt files are deleted.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    public async Task<CoverVerifyResult> VerifyAsync(
        CoverCatalog catalog,
        bool fix,
        IProgress<CoverVerifyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var ok = 0;
        var missing = 0;
        var corrupt = 0;
        var bytes = 0L;
        var failures = new List<CoverVerifyFailure>();
        var completed = 0;
        foreach (CoverCatalogEntry entry in catalog.Covers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = GetImagePath(entry.Id);
            if (!File.Exists(path))
            {
                missing++;
                failures.Add(new CoverVerifyFailure(entry.Id, path, "Missing cached image."));
            }
            else
            {
                try
                {
                    var actual = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(actual, entry.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        corrupt++;
                        failures.Add(new CoverVerifyFailure(entry.Id, path, "Cached image hash does not match."));
                        if (fix)
                        {
                            TryDelete(path);
                        }
                    }
                    else
                    {
                        ok++;
                        bytes += new FileInfo(path).Length;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    corrupt++;
                    failures.Add(new CoverVerifyFailure(entry.Id, path, exception.Message));
                    if (fix)
                    {
                        TryDelete(path);
                    }
                }
            }

            completed++;
            progress?.Report(new CoverVerifyProgress(completed, catalog.Covers.Count, entry.Name));
        }

        return new CoverVerifyResult(ok, missing, corrupt, bytes, failures);
    }

    /// <summary>Gets current cache statistics.</summary>
    /// <returns>Image count, size, and catalog/pack presence.</returns>
    public CoverCacheStats GetStats()
    {
        var count = 0;
        var bytes = 0L;
        try
        {
            if (Directory.Exists(ImagesDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(ImagesDirectory, "*.webp"))
                {
                    count++;
                    bytes += new FileInfo(file).Length;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Cover cache statistics could not be read.");
        }

        return new CoverCacheStats(
            count,
            bytes,
            File.Exists(CatalogPath),
            File.Exists(Path.Combine(PacksDirectory, "covers-v1.zip")));
    }

    /// <summary>Deletes cached catalog, images, temporary files, and packs.</summary>
    public void Clear()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Warning(exception, "Cover cache {Directory} could not be cleared.", _rootDirectory);
        }
    }

    /// <summary>Deletes cached images that are no longer referenced by a catalog.</summary>
    /// <param name="catalog">Current catalog.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of deleted files.</returns>
    public Task<int> PruneAsync(CoverCatalog catalog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CoverCatalogEntry entry in catalog.Covers)
        {
            keep.Add(entry.Id.ToLowerInvariant());
        }

        var deleted = 0;
        if (Directory.Exists(ImagesDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(ImagesDirectory, "*.webp"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = Path.GetFileNameWithoutExtension(file);
                if (!keep.Contains(id) && TryDelete(file))
                {
                    deleted++;
                }
            }
        }

        return Task.FromResult(deleted);
    }

    private static async Task WriteTextAtomicAsync(
        string path,
        string text,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, text, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Cover cache file {Path} could not be deleted.", path);
            return false;
        }
    }

    private static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length != 64 || !IsHex(id))
        {
            throw new ArgumentException("The cover image identifier must be a SHA-256 hash.", nameof(id));
        }
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            if (character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F')))
            {
                return false;
            }
        }

        return true;
    }
}
