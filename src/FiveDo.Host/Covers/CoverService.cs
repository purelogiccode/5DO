using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Host;

/// <summary>
/// Resolves cover art from local files and the online catalog, and downloads missing
/// content-addressed images.
/// </summary>
public sealed class CoverService : IDisposable
{
    private const int DownloadConcurrency = 4;

    private readonly CoverServiceOptions _options;
    private readonly CoverStore _store;
    private readonly CoverDownloader _downloader;
    private readonly SemaphoreSlim _catalogGate = new(1, 1);
    private readonly Lock _localGate = new();
    private readonly Dictionary<string, LocalCoverIndex> _directoryIndexes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _progressGate = new();
    private CoverCatalog? _catalog;
    private CoverCatalogIndex? _index;
    private CoverCatalogCacheMetadata _metadata = new();
    private LocalCoverIndex? _coverDirectoryIndex;
    private string _coverDirectory = string.Empty;
    private bool _disposed;

    /// <summary>Creates a cover service.</summary>
    /// <param name="options">Service options.</param>
    /// <param name="httpClient">Optional HTTP client for tests or a shared connection pool.</param>
    /// <param name="store">Optional cache store for tests.</param>
    public CoverService(
        CoverServiceOptions options,
        HttpClient? httpClient = null,
        CoverStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _store = store ?? new CoverStore(
            string.IsNullOrWhiteSpace(options.CacheDirectory) ? null : options.CacheDirectory);
        _downloader = new CoverDownloader(httpClient);
    }

    /// <summary>Gets the service options.</summary>
    public CoverServiceOptions Options => _options;

    /// <summary>Gets the cover cache store.</summary>
    public CoverStore Store => _store;

    /// <summary>Gets the loaded catalog, if any.</summary>
    public CoverCatalog? Catalog => _catalog;

    /// <summary>Gets the loaded catalog index, if any.</summary>
    public CoverCatalogIndex? Index => _index;

    /// <summary>Gets the current catalog cache metadata.</summary>
    public CoverCatalogCacheMetadata CatalogMetadata => _metadata;

    /// <summary>Refreshes the catalog from its source, falling back to the cache.</summary>
    /// <param name="force">Whether a fresh cache is ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    public async Task<CoverCatalogRefreshResult> RefreshCatalogAsync(
        bool force,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _catalogGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_catalog is null)
            {
                TryLoadCachedCatalog();
            }

            if (!_options.UseOnlineCovers)
            {
                return _catalog is not null
                    ? Success(fromCache: true, notModified: false, error: null)
                    : new CoverCatalogRefreshResult(false, false, false, string.Empty, 0, "Online covers are disabled.");
            }

            if (!force
                && _catalog is not null
                && _metadata.FetchedUtc != default
                && DateTimeOffset.UtcNow - _metadata.FetchedUtc < _options.CatalogCacheDuration)
            {
                return Success(fromCache: true, notModified: false, error: null);
            }

            try
            {
                CoverCatalogHttpResult response = await _downloader.GetCatalogAsync(
                        _options.CatalogUri,
                        _metadata.ETag,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (response.NotModified && _catalog is not null)
                {
                    _metadata.FetchedUtc = DateTimeOffset.UtcNow;
                    Serilog.Log.Debug("Cover catalog is unchanged (HTTP 304).");
                    return Success(fromCache: true, notModified: true, error: null);
                }

                if (!TrySetCatalog(response.Json, response.ETag, out var error))
                {
                    return _catalog is not null
                        ? Success(fromCache: true, notModified: false, error: error)
                        : new CoverCatalogRefreshResult(
                            false,
                            false,
                            false,
                            string.Empty,
                            0,
                            error);
                }

                await _store.SaveCatalogAsync(
                        response.Json,
                        _options.CatalogUri,
                        response.ETag,
                        cancellationToken)
                    .ConfigureAwait(false);
                Serilog.Log.Information(
                    "Cover catalog loaded: {PackVersion}, {CoverCount} covers.",
                    _catalog?.PackVersion,
                    _catalog?.Covers.Count);
                return Success(fromCache: false, notModified: false, error: null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is CoverHttpException
                or HttpRequestException
                or TaskCanceledException
                or IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException)
            {
                Serilog.Log.Debug(exception, "Cover catalog refresh failed; using the cached catalog when available.");
                return _catalog is not null
                    ? Success(fromCache: true, notModified: false, error: exception.Message)
                    : new CoverCatalogRefreshResult(
                        false,
                        false,
                        false,
                        string.Empty,
                        0,
                        exception.Message);
            }
        }
        finally
        {
            _catalogGate.Release();
        }
    }

    /// <summary>Resolves local and catalog covers for every library entry.</summary>
    /// <param name="entries">Library entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One resolution per entry, in the same order.</returns>
    public async Task<IReadOnlyList<CoverResolution>> ResolveAllAsync(
        IReadOnlyList<GameLibraryEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ThrowIfDisposed();
        if (_index is null)
        {
            await RefreshCatalogAsync(force: false, cancellationToken).ConfigureAwait(false);
        }

        LocalCoverIndex? coverDirectory = GetCoverDirectoryIndex();
        var results = new List<CoverResolution>(entries.Count);
        foreach (GameLibraryEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(ResolveEntry(entry, coverDirectory));
        }

        return results;
    }

    /// <summary>Downloads every missing cover referenced by a resolution set.</summary>
    /// <param name="resolutions">Resolved covers.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The download result.</returns>
    public async Task<CoverDownloadResult> DownloadMissingAsync(
        IReadOnlyList<CoverResolution> resolutions,
        IProgress<CoverDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        ThrowIfDisposed();
        if (_index is null || _catalog is null)
        {
            return EmptyDownloadResult();
        }

        var byId = new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase);
        foreach (CoverResolution resolution in resolutions)
        {
            if (!resolution.NeedsDownload
                || resolution.CoverId.Length == 0
                || string.IsNullOrWhiteSpace(resolution.CoverPath))
            {
                continue;
            }

            if (!byId.TryGetValue(resolution.CoverId, out Target? target))
            {
                target = new Target(
                    resolution.CoverId,
                    resolution.CoverPath,
                    resolution.MatchedName);
                byId[resolution.CoverId] = target;
            }

            target.EntryPaths.Add(resolution.EntryPath);
        }

        if (byId.Count == 0)
        {
            return EmptyDownloadResult();
        }

        return await DownloadTargetsAsync(
                byId.Values.ToList(),
                usedPack: false,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Downloads every cover in the catalog, preferring the bulk pack.</summary>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The download result.</returns>
    public async Task<CoverDownloadResult> DownloadAllAsync(
        IProgress<CoverDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_index is null)
        {
            await RefreshCatalogAsync(force: false, cancellationToken).ConfigureAwait(false);
        }

        if (_index is null || _catalog is null)
        {
            return EmptyDownloadResult();
        }

        Uri? packUri = null;
        try
        {
            packUri = CoverCatalogIndex.ResolvePackUri(_options.CatalogUri, _catalog);
        }
        catch (InvalidDataException exception)
        {
            Serilog.Log.Debug(exception, "Cover pack URI is invalid; using per-image downloads.");
        }

        if (packUri is not null)
        {
            try
            {
                CoverDownloadResult? packResult = await TryDownloadPackAsync(
                        packUri,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (packResult is not null)
                {
                    return packResult;
                }
            }
            catch (Exception exception) when (exception is CoverHttpException
                or HttpRequestException
                or TaskCanceledException
                or IOException
                or UnauthorizedAccessException
                or InvalidDataException)
            {
                Serilog.Log.Debug(exception, "Cover pack download failed; falling back to per-image downloads.");
            }
        }

        var targets = new List<Target>(_catalog.Covers.Count);
        foreach (CoverCatalogEntry entry in _catalog.Covers)
        {
            targets.Add(new Target(
                entry.Id,
                _store.GetImagePath(entry.Id),
                entry.Name));
        }

        return await DownloadTargetsAsync(
                targets,
                usedPack: false,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Verifies every cached image against the loaded catalog.</summary>
    /// <param name="fix">Whether corrupt files are deleted.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    public async Task<CoverVerifyResult> VerifyCacheAsync(
        bool fix,
        IProgress<CoverVerifyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_catalog is null)
        {
            await RefreshCatalogAsync(force: false, cancellationToken).ConfigureAwait(false);
        }

        return _catalog is null
            ? new CoverVerifyResult(0, 0, 0, 0, [])
            : await _store.VerifyAsync(_catalog, fix, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets current cache statistics.</summary>
    /// <returns>Cache statistics.</returns>
    public CoverCacheStats GetCacheStats()
    {
        ThrowIfDisposed();
        return _store.GetStats();
    }

    /// <summary>Deletes the online cover cache and local indexes.</summary>
    public void ClearCache()
    {
        ThrowIfDisposed();
        _store.Clear();
        lock (_localGate)
        {
            _directoryIndexes.Clear();
            _coverDirectoryIndex = null;
        }
    }

    /// <summary>Clears cached local-cover indexes so the next resolution rescans the directories.</summary>
    public void ReloadLocalCovers()
    {
        ThrowIfDisposed();
        lock (_localGate)
        {
            _directoryIndexes.Clear();
            _coverDirectoryIndex = null;
            _coverDirectory = string.Empty;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _downloader.Dispose();
        _catalogGate.Dispose();
    }

    private async Task<CoverDownloadResult> DownloadTargetsAsync(
        IReadOnlyList<Target> targets,
        bool usedPack,
        IProgress<CoverDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var downloaded = 0;
        var skipped = 0;
        var failed = 0;
        var bytes = 0L;
        var completed = 0;
        var coverPaths = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<CoverDownloadFailure>();
        var failureGate = new Lock();
        var catalogById = new Dictionary<string, CoverCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (CoverCatalogEntry entry in _catalog!.Covers)
        {
            catalogById[entry.Id] = entry;
        }

        await Parallel.ForEachAsync(
                targets,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = DownloadConcurrency,
                    CancellationToken = cancellationToken,
                },
                async (target, token) =>
                {
                    try
                    {
                        if (_store.HasImage(target.Id))
                        {
                            Interlocked.Increment(ref skipped);
                            AddPaths(coverPaths, target, target.CoverPath);
                            ReportProgress(target.Name);
                            return;
                        }

                        if (!catalogById.TryGetValue(target.Id, out CoverCatalogEntry? catalogEntry))
                        {
                            throw new InvalidDataException("The cover is missing from the loaded catalog.");
                        }

                        Uri imageUri = CoverCatalogIndex.ResolveImageUri(
                            _options.CatalogUri,
                            _catalog,
                            catalogEntry);
                        var path = await _downloader.DownloadImageAsync(
                                imageUri,
                                target.Id,
                                _store,
                                progress: null,
                                cancellationToken: token)
                            .ConfigureAwait(false);
                        Interlocked.Increment(ref downloaded);
                        Interlocked.Add(ref bytes, GetLength(path));
                        AddPaths(coverPaths, target, path);
                        ReportProgress(target.Name);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is CoverHttpException
                        or HttpRequestException
                        or TaskCanceledException
                        or IOException
                        or UnauthorizedAccessException
                        or InvalidDataException
                        or NotSupportedException)
                    {
                        Interlocked.Increment(ref failed);
                        lock (failureGate)
                        {
                            failures.Add(new CoverDownloadFailure(
                                target.EntryPaths.Count > 0 ? target.EntryPaths[0] : string.Empty,
                                target.Id,
                                exception.Message));
                        }

                        Serilog.Log.Debug(exception, "Cover download failed for {CoverId}.", target.Id);
                        ReportProgress(target.Name);
                    }
                })
            .ConfigureAwait(false);

        progress?.Report(new CoverDownloadProgress(
            completed,
            targets.Count,
            bytes,
            string.Empty));
        return new CoverDownloadResult(
            downloaded,
            skipped,
            failed,
            bytes,
            usedPack,
            failures,
            coverPaths);

        void AddPaths(
            ConcurrentDictionary<string, string> paths,
            Target target,
            string path)
        {
            foreach (var entryPath in target.EntryPaths)
            {
                paths[entryPath] = path;
            }
        }

        void ReportProgress(string currentName)
        {
            var value = Interlocked.Increment(ref completed);
            lock (_progressGate)
            {
                progress?.Report(new CoverDownloadProgress(
                    value,
                    targets.Count,
                    Interlocked.Read(ref bytes),
                    currentName));
            }
        }
    }

    private async Task<CoverDownloadResult?> TryDownloadPackAsync(
        Uri packUri,
        IProgress<CoverDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var packPath = Path.Combine(_store.PacksDirectory, "covers-v1.zip");
        await _downloader.DownloadPackAsync(
                packUri,
                packPath,
                _catalog!.PackSize > 0 ? _catalog.PackSize : null,
                string.IsNullOrWhiteSpace(_catalog.PackSha256) ? null : _catalog.PackSha256,
                progress is null
                    ? null
                    : new Progress<long>(value => progress.Report(new CoverDownloadProgress(
                        0,
                        0,
                        value,
                        "Bulk cover pack"))),
                cancellationToken)
            .ConfigureAwait(false);

        var expected = new HashSet<string>(
            _catalog.Covers.Select(entry => entry.Id),
            StringComparer.OrdinalIgnoreCase);
        var extracted = 0;
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(packPath);
            foreach (ZipArchiveEntry zipEntry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = zipEntry.FullName.Replace('\\', '/');
                if (name.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                {
                    name = name["images/".Length..];
                }

                if (name.Contains('/', StringComparison.Ordinal))
                {
                    continue;
                }

                var id = Path.GetFileNameWithoutExtension(name);
                if (!expected.Contains(id) || zipEntry.Length == 0)
                {
                    continue;
                }

                using Stream stream = zipEntry.Open();
                await _store.SaveImageAsync(id, stream, cancellationToken).ConfigureAwait(false);
                extracted++;
                progress?.Report(new CoverDownloadProgress(
                    extracted,
                    _catalog.Covers.Count,
                    zipEntry.Length,
                    Path.GetFileName(name)));
            }
        }
        catch (InvalidDataException exception)
        {
            Serilog.Log.Debug(exception, "Cover pack is not a valid ZIP archive; falling back.");
            return null;
        }

        if (extracted == 0)
        {
            return null;
        }

        var missing = _catalog.Covers.Count(entry => !_store.HasImage(entry.Id));
        if (missing > 0)
        {
            Serilog.Log.Debug(
                "Cover pack contained {Extracted} images but {Missing} catalog covers are still missing.",
                extracted,
                missing);
            return null;
        }

        return new CoverDownloadResult(
            extracted,
            0,
            0,
            new FileInfo(packPath).Length,
            UsedPack: true,
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private CoverResolution ResolveEntry(GameLibraryEntry entry, LocalCoverIndex? coverDirectory)
    {
        var fileName = Path.GetFileNameWithoutExtension(entry.Path);
        if (!string.IsNullOrWhiteSpace(entry.CoverPath) && File.Exists(entry.CoverPath))
        {
            return new CoverResolution(
                entry.Path,
                entry.CoverPath,
                "local",
                1.0,
                Path.GetFileName(entry.CoverPath));
        }

        if (coverDirectory is not null)
        {
            var exact = coverDirectory.FindExact(fileName);
            if (exact is not null)
            {
                return new CoverResolution(entry.Path, exact, "local-exact", 1.0, Path.GetFileName(exact));
            }

            LocalCoverMatch? localExact = coverDirectory.Find(
                entry.Name,
                fileName,
                _options.FuzzyThreshold,
                fuzzy: false,
                entry.Regions,
                entry.ReleaseYear);
            if (localExact is not null)
            {
                return new CoverResolution(
                    entry.Path,
                    localExact.Path,
                    "local-exact",
                    localExact.Score,
                    localExact.MatchedAlias);
            }
        }

        if (_index is not null)
        {
            CoverCatalogEntry? byGameId = _index.FindByGameId(entry.GameId);
            if (byGameId is not null)
            {
                return CatalogResolution(entry, byGameId, 1.0, "catalog-game-id", byGameId.Name);
            }

            CoverCatalogMatch? catalogExact = _index.FindByTitle(
                entry.Name,
                fileName,
                _options.FuzzyThreshold,
                fuzzy: false,
                entry.Regions,
                entry.ReleaseYear);
            if (catalogExact is not null)
            {
                return CatalogResolution(
                    entry,
                    catalogExact.Entry,
                    catalogExact.Score,
                    "catalog-exact",
                    catalogExact.MatchedAlias);
            }
        }

        if (coverDirectory is not null)
        {
            LocalCoverMatch? localFuzzy = coverDirectory.Find(
                entry.Name,
                fileName,
                _options.FuzzyThreshold,
                _options.FuzzyMatching,
                entry.Regions,
                entry.ReleaseYear);
            if (localFuzzy is not null)
            {
                return new CoverResolution(
                    entry.Path,
                    localFuzzy.Path,
                    "local-fuzzy",
                    localFuzzy.Score,
                    localFuzzy.MatchedAlias);
            }
        }

        LocalCoverIndex? sibling = GetDirectoryIndex(Path.GetDirectoryName(entry.Path));
        if (sibling is not null)
        {
            LocalCoverMatch? siblingFuzzy = sibling.Find(
                entry.Name,
                fileName,
                _options.FuzzyThreshold,
                _options.FuzzyMatching,
                entry.Regions,
                entry.ReleaseYear);
            if (siblingFuzzy is not null)
            {
                return new CoverResolution(
                    entry.Path,
                    siblingFuzzy.Path,
                    "local-fuzzy",
                    siblingFuzzy.Score,
                    siblingFuzzy.MatchedAlias);
            }
        }

        if (_index is not null)
        {
            CoverCatalogMatch? catalogFuzzy = _index.FindByTitle(
                entry.Name,
                fileName,
                _options.FuzzyThreshold,
                _options.FuzzyMatching,
                entry.Regions,
                entry.ReleaseYear);
            if (catalogFuzzy is not null)
            {
                return CatalogResolution(
                    entry,
                    catalogFuzzy.Entry,
                    catalogFuzzy.Score,
                    "catalog-fuzzy",
                    catalogFuzzy.MatchedAlias);
            }
        }

        return new CoverResolution(entry.Path, null, "none", 0.0, string.Empty);
    }

    private CoverResolution CatalogResolution(
        GameLibraryEntry entry,
        CoverCatalogEntry catalogEntry,
        double score,
        string source,
        string matchedName)
    {
        var path = _store.GetImagePath(catalogEntry.Id);
        return new CoverResolution(
            entry.Path,
            path,
            source,
            score,
            matchedName,
            catalogEntry.Id,
            NeedsDownload: !File.Exists(path));
    }

    private LocalCoverIndex? GetCoverDirectoryIndex()
    {
        if (string.IsNullOrWhiteSpace(_options.CoverDirectory))
        {
            return null;
        }

        lock (_localGate)
        {
            if (_coverDirectoryIndex is not null
                && string.Equals(_coverDirectory, _options.CoverDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return _coverDirectoryIndex;
            }

            _coverDirectory = _options.CoverDirectory;
            _coverDirectoryIndex = LocalCoverIndex.Build(
                _options.CoverDirectory,
                _options.AnnotationStripping);
            return _coverDirectoryIndex;
        }
    }

    private LocalCoverIndex? GetDirectoryIndex(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        lock (_localGate)
        {
            if (_directoryIndexes.TryGetValue(directory, out LocalCoverIndex? index))
            {
                return index;
            }

            index = LocalCoverIndex.Build(directory, _options.AnnotationStripping);
            _directoryIndexes[directory] = index;
            return index.Count > 0 ? index : null;
        }
    }

    private void TryLoadCachedCatalog()
    {
        if (!_store.TryLoadCatalog(out var json, out CoverCatalogCacheMetadata metadata))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(metadata.SourceUrl)
            && !string.Equals(
                metadata.SourceUrl,
                _options.CatalogUri.AbsoluteUri,
                StringComparison.OrdinalIgnoreCase))
        {
            Serilog.Log.Debug(
                "Ignoring cached cover catalog from {CachedSource}; the configured source is {ConfiguredSource}.",
                metadata.SourceUrl,
                _options.CatalogUri);
            return;
        }

        if (TrySetCatalog(json, metadata.ETag, out var error))
        {
            _metadata = metadata;
            return;
        }

        Serilog.Log.Debug("Cached cover catalog is invalid: {Error}", error);
    }

    private bool TrySetCatalog(string json, string? etag, out string? error)
    {
        CoverCatalogLoadResult result = CoverCatalogLoader.LoadJson(
            json,
            _options.AnnotationStripping);
        if (!result.Success)
        {
            error = result.Error;
            return false;
        }

        _catalog = result.Catalog;
        _index = result.Index;
        _metadata = new CoverCatalogCacheMetadata
        {
            SourceUrl = _options.CatalogUri.AbsoluteUri,
            ETag = etag ?? string.Empty,
            FetchedUtc = DateTimeOffset.UtcNow,
        };
        error = null;
        return true;
    }

    private CoverCatalogRefreshResult Success(bool fromCache, bool notModified, string? error)
    {
        return new CoverCatalogRefreshResult(
            true,
            fromCache,
            notModified,
            _catalog?.PackVersion ?? string.Empty,
            _catalog?.Covers.Count ?? 0,
            error);
    }

    private static CoverDownloadResult EmptyDownloadResult()
    {
        return new CoverDownloadResult(
            0,
            0,
            0,
            0,
            false,
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static long GetLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class Target
    {
        internal Target(string id, string coverPath, string name)
        {
            Id = id;
            CoverPath = coverPath;
            Name = name;
        }

        internal string Id { get; }

        internal string CoverPath { get; }

        internal string Name { get; }

        internal List<string> EntryPaths { get; } = [];
    }
}
