using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Host;

/// <summary>Result of a conditional catalog request.</summary>
/// <param name="NotModified">Whether the server returned HTTP 304.</param>
/// <param name="Json">Catalog JSON.</param>
/// <param name="ETag">Response ETag, or an empty string.</param>
public sealed record CoverCatalogHttpResult(bool NotModified, string Json, string ETag);

/// <summary>An HTTP failure with an optional status code.</summary>
public sealed class CoverHttpException : Exception
{
    /// <summary>Creates an empty exception.</summary>
    public CoverHttpException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Failure message.</param>
    public CoverHttpException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    /// <param name="message">Failure message.</param>
    /// <param name="innerException">Inner exception.</param>
    public CoverHttpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

}

/// <summary>
/// Downloads the cover catalog and content-addressed images. File URIs are supported for
/// offline catalogs and tests; remote catalogs must use HTTP or HTTPS.
/// </summary>
public sealed class CoverDownloader : IDisposable
{
    private const int BufferSize = 64 * 1024;
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    /// <summary>Creates a downloader.</summary>
    /// <param name="httpClient">Optional HTTP client; a configured client is created when omitted.</param>
    public CoverDownloader(HttpClient? httpClient = null)
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
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("5DO-CoverService/1.0");
            _ownsClient = true;
        }
        else
        {
            _http = httpClient;
            _ownsClient = false;
        }
    }

    /// <summary>Requests the catalog, using a conditional request when an ETag is supplied.</summary>
    /// <param name="uri">Catalog URI.</param>
    /// <param name="etag">Optional cached ETag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response, including a 304 result.</returns>
    public async Task<CoverCatalogHttpResult> GetCatalogAsync(
        Uri uri,
        string? etag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.IsFile)
        {
            if (new FileInfo(uri.LocalPath).Length > CoverDefaults.MaxCatalogBytes)
            {
                throw new InvalidDataException("The cover catalog exceeds the maximum allowed size.");
            }

            var json = await File.ReadAllTextAsync(uri.LocalPath, cancellationToken).ConfigureAwait(false);
            return new CoverCatalogHttpResult(false, json.TrimStart('\uFEFF'), string.Empty);
        }

        EnsureRemoteScheme(uri);
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(30), cancellationToken);
        var timeoutToken = timeout.Token;
        return await ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(etag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new CoverCatalogHttpResult(true, string.Empty, etag ?? string.Empty);
            }

            await EnsureSuccessAsync(response, "cover catalog").ConfigureAwait(false);
            var json = await ReadLimitedStringAsync(
                    response,
                    CoverDefaults.MaxCatalogBytes,
                    timeoutToken)
                .ConfigureAwait(false);
            return new CoverCatalogHttpResult(
                false,
                json,
                response.Headers.ETag?.Tag ?? string.Empty);
        }, timeoutToken).ConfigureAwait(false);
    }

    /// <summary>Downloads one image into the content-addressed cache.</summary>
    /// <param name="uri">Image URI.</param>
    /// <param name="id">Expected SHA-256 identifier.</param>
    /// <param name="store">Cache store.</param>
    /// <param name="progress">Optional byte progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached image path.</returns>
    public async Task<string> DownloadImageAsync(
        Uri uri,
        string id,
        CoverStore store,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(store);
        if (uri.IsFile)
        {
            using var file = new FileStream(
                uri.LocalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);
            return await store.SaveImageAsync(id, file, cancellationToken, progress).ConfigureAwait(false);
        }

        EnsureRemoteScheme(uri);
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(120), cancellationToken);
        var timeoutToken = timeout.Token;
        return await ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken)
                .ConfigureAwait(false);
            await EnsureSuccessAsync(response, "cover image").ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is > CoverDefaults.MaxImageBytes)
            {
                throw new InvalidDataException("The cover image exceeds the maximum allowed size.");
            }

            using Stream stream = await response.Content
                .ReadAsStreamAsync(timeoutToken)
                .ConfigureAwait(false);
            return await store.SaveImageAsync(id, stream, timeoutToken, progress).ConfigureAwait(false);
        }, timeoutToken).ConfigureAwait(false);
    }

    /// <summary>Downloads the optional bulk pack to a local path.</summary>
    /// <param name="uri">Pack URI.</param>
    /// <param name="destination">Destination path.</param>
    /// <param name="expectedSize">Optional expected size.</param>
    /// <param name="expectedSha256">Optional expected SHA-256 hash.</param>
    /// <param name="progress">Optional byte progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The destination path.</returns>
    public async Task<string> DownloadPackAsync(
        Uri uri,
        string destination,
        long? expectedSize,
        string? expectedSha256,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (uri.IsFile)
        {
            using var source = new FileStream(
                uri.LocalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                useAsync: true);
            return await CopyPackAsync(
                    source,
                    destination,
                    expectedSize,
                    expectedSha256,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        EnsureRemoteScheme(uri);
        using var timeout = CreateTimeout(TimeSpan.FromMinutes(15), cancellationToken);
        var timeoutToken = timeout.Token;
        return await ExecuteWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken)
                .ConfigureAwait(false);
            await EnsureSuccessAsync(response, "cover pack").ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is > CoverDefaults.MaxPackBytes)
            {
                throw new InvalidDataException("The cover pack exceeds the maximum allowed size.");
            }

            using Stream stream = await response.Content
                .ReadAsStreamAsync(timeoutToken)
                .ConfigureAwait(false);
            return await CopyPackAsync(
                    stream,
                    destination,
                    expectedSize,
                    expectedSha256,
                    progress,
                    timeoutToken)
                .ConfigureAwait(false);
        }, timeoutToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    private static async Task<string> CopyPackAsync(
        Stream source,
        string destination,
        long? expectedSize,
        string? expectedSha256,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var temporary = destination + ".part";
        try
        {
            var bytes = 0L;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                using var output = new FileStream(
                    temporary,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    useAsync: true);
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    bytes += read;
                    if (bytes > CoverDefaults.MaxPackBytes)
                    {
                        throw new InvalidDataException("The cover pack exceeds the maximum allowed size.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report(bytes);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (expectedSize is { } size && bytes != size)
            {
                throw new InvalidDataException(
                    $"The cover pack size {bytes} does not match the catalog size {size}.");
            }

            var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(expectedSha256)
                && !string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"The cover pack hash {actual} does not match the catalog hash {expectedSha256}.");
            }

            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string description)
    {
        if (response.IsSuccessStatusCode)
        {
            return Task.CompletedTask;
        }

        var message = $"The {description} request returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
        if (response.StatusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout)
        {
            return Task.FromException(new TransientCoverHttpException(message));
        }

        return Task.FromException(new CoverHttpException(message));
    }

    private static async Task<string> ReadLimitedStringAsync(
        HttpResponseMessage response,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } contentLength && contentLength > maximumBytes)
        {
            throw new InvalidDataException("The cover catalog exceeds the maximum allowed size.");
        }

        using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] rented = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(rented, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maximumBytes)
                {
                    throw new InvalidDataException("The cover catalog exceeds the maximum allowed size.");
                }

                buffer.Write(rented, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }

    private static CancellationTokenSource CreateTimeout(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (TransientCoverHttpException) when (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested
                                                && attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static void EnsureRemoteScheme(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException($"Unsupported cover URL scheme '{uri.Scheme}'.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Temporary cover file {Path} could not be deleted.", path);
        }
    }

    [SuppressMessage(
        "Roslynator",
        "RCS1194",
        Justification = "Internal marker exception; only the message constructor is used.")]
    private sealed class TransientCoverHttpException : Exception
    {
        internal TransientCoverHttpException(string message)
            : base(message)
        {
        }
    }
}
