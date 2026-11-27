using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Lobby.Protocol;

/// <summary>
/// A small HTTP client for the lobby room API. The client is emulator-agnostic; the
/// caller supplies the emulator tag and content metadata.
/// </summary>
public sealed class LobbyApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _baseUri;
    private bool _disposed;

    /// <summary>Initializes a new lobby API client.</summary>
    /// <param name="baseUri">Lobby base URI, for example <c>http://127.0.0.1:8080/</c>.</param>
    /// <param name="httpClient">Optional caller-owned HTTP client.</param>
    public LobbyApiClient(Uri baseUri, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri
            || (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            throw new ArgumentException("The lobby URI must be an absolute http or https URI.", nameof(baseUri));
        }

        _baseUri = new Uri(baseUri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    /// <summary>Gets the normalized lobby base URI.</summary>
    public Uri BaseUri => _baseUri;

    /// <summary>Creates a room and returns its host token.</summary>
    /// <param name="request">Room metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created room.</returns>
    public async Task<CreateRoomResponse> CreateRoomAsync(
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "v1/rooms"));
        message.Content = JsonContent(request, LobbyJsonContext.Default.CreateRoomRequest);
        using HttpResponseMessage response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync(response, LobbyJsonContext.Default.CreateRoomResponse, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Joins an existing room by join code.</summary>
    /// <param name="joinCode">Human join code.</param>
    /// <param name="request">Joiner metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The joined room and slot token.</returns>
    public async Task<JoinRoomResponse> JoinRoomAsync(
        string joinCode,
        JoinRoomRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(joinCode);
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_baseUri, $"v1/rooms/{Uri.EscapeDataString(joinCode)}/join"));
        message.Content = JsonContent(request, LobbyJsonContext.Default.JoinRoomRequest);
        using HttpResponseMessage response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync(response, LobbyJsonContext.Default.JoinRoomResponse, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets public room status.</summary>
    /// <param name="joinCode">Human join code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The room status.</returns>
    public async Task<RoomStatus> GetRoomAsync(string joinCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(joinCode);
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(_baseUri, $"v1/rooms/{Uri.EscapeDataString(joinCode)}"));
        using HttpResponseMessage response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync(response, LobbyJsonContext.Default.RoomStatus, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Closes a room using the host token.</summary>
    /// <param name="joinCode">Human join code.</param>
    /// <param name="hostToken">Host token returned at creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the room is closed.</returns>
    public async Task CloseRoomAsync(
        string joinCode,
        string hostToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(joinCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostToken);
        using var message = new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri(_baseUri, $"v1/rooms/{Uri.EscapeDataString(joinCode)}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", hostToken);
        using HttpResponseMessage response = await _http
            .SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowErrorAsync(response, cancellationToken).ConfigureAwait(false);
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
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private static StringContent JsonContent<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        return new StringContent(
            JsonSerializer.Serialize(value, typeInfo),
            Encoding.UTF8,
            "application/json");
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(json, typeInfo)
                ?? throw new LobbyApiException(
                    response.StatusCode,
                    "invalid_response",
                    "The lobby returned an empty JSON response.");
        }
        catch (JsonException exception)
        {
            throw new LobbyApiException(
                response.StatusCode,
                "invalid_response",
                $"The lobby returned an unreadable JSON response: {exception.Message}");
        }
    }

    private static async Task ThrowErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        LobbyError? error = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                error = JsonSerializer.Deserialize(json, LobbyJsonContext.Default.LobbyError);
            }
            catch (JsonException)
            {
                // Fall through to the generic message.
            }
        }

        string code = error?.Code ?? $"http_{(int)response.StatusCode}";
        string message = error?.Message
            ?? $"The lobby returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
        throw new LobbyApiException(response.StatusCode, code, message);
    }
}
