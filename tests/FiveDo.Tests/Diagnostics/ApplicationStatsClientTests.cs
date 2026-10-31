using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Diagnostics.Statistics;

namespace FiveDo.Tests.Diagnostics;

public sealed class ApplicationStatsClientTests
{
    [Fact]
    public async Task TrackLaunchAsync_PostsApplicationIdVersionAndBearerToken()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var options = new ApplicationStatsOptions
        {
            Endpoint = "https://example.test/stats",
            ApiKey = "test-key",
            ApplicationId = "test-app",
            Version = "9.9.9",
        };

        await ApplicationStatsClient.TrackLaunchAsync(options, http);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/stats", handler.RequestUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);
        Assert.NotNull(handler.Body);
        Assert.Contains("\"applicationId\":\"test-app\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"version\":\"9.9.9\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrackLaunchAsync_WhenRateLimited_DoesNotThrow()
    {
        using var http = new HttpClient(new RecordingHandler(HttpStatusCode.TooManyRequests));

        await ApplicationStatsClient.TrackLaunchAsync(CreateOptions(), http);
    }

    [Fact]
    public async Task TrackLaunchAsync_WhenTransportFails_DoesNotThrow()
    {
        using var http = new HttpClient(new ThrowingHandler());

        await ApplicationStatsClient.TrackLaunchAsync(CreateOptions(), http);
    }

    private static ApplicationStatsOptions CreateOptions()
    {
        return new ApplicationStatsOptions
        {
            Endpoint = "https://example.test/stats",
            ApiKey = "test-key",
            ApplicationId = "test-app",
            Version = "9.9.9",
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public RecordingHandler(HttpStatusCode status)
        {
            _status = status;
        }

        public HttpMethod? Method { get; private set; }

        public string? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri?.ToString();
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(_status);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("network down");
        }
    }
}
