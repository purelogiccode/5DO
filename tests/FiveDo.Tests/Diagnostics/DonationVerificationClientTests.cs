using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Diagnostics.Donations;

namespace FiveDo.Tests.Diagnostics;

public sealed class DonationVerificationClientTests
{
    [Fact]
    public async Task VerifyAsync_PostsEmailSlugAndApiKey()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"donated":true,"applicationSlug":"5do","totalAmount":15.5,"lastDonationAtUtc":"2026-10-30T12:00:00Z"}""");
        using var http = new HttpClient(handler);

        DonationVerificationResult result = await DonationVerificationClient.VerifyAsync(
            " donor@example.com ",
            CreateOptions(),
            http,
            CancellationToken.None);

        Assert.Equal(DonationVerificationStatus.Donated, result.Status);
        Assert.Equal(15.5m, result.TotalAmount);
        Assert.Equal(new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc), result.LastDonationAtUtc);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://example.test/api/donations/verify", handler.RequestUri);
        Assert.Equal("test-key", handler.ApiKey);
        Assert.NotNull(handler.Body);
        Assert.Contains("\"email\":\"donor@example.com\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"applicationSlug\":\"5do\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenNotDonated_ReturnsNotDonated()
    {
        using var http = new HttpClient(new RecordingHandler(
            HttpStatusCode.OK,
            """{"donated":false,"applicationSlug":"5do","totalAmount":0,"lastDonationAtUtc":null}"""));

        DonationVerificationResult result = await DonationVerificationClient.VerifyAsync(
            "donor@example.com",
            CreateOptions(),
            http,
            CancellationToken.None);

        Assert.Equal(DonationVerificationStatus.NotDonated, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_WithEmptyEmail_DoesNotSendARequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"donated":true}""");
        using var http = new HttpClient(handler);

        DonationVerificationResult result = await DonationVerificationClient.VerifyAsync(
            " ",
            CreateOptions(),
            http,
            CancellationToken.None);

        Assert.Equal(DonationVerificationStatus.NotDonated, result.Status);
        Assert.Null(handler.Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task VerifyAsync_WhenServiceRejectsRequest_ReturnsUnavailable(HttpStatusCode statusCode)
    {
        using var http = new HttpClient(new RecordingHandler(statusCode, "{}"));

        DonationVerificationResult result = await DonationVerificationClient.VerifyAsync(
            "donor@example.com",
            CreateOptions(),
            http,
            CancellationToken.None);

        Assert.Equal(DonationVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task VerifyAsync_WhenTransportFails_ReturnsUnavailable()
    {
        using var http = new HttpClient(new ThrowingHandler());

        DonationVerificationResult result = await DonationVerificationClient.VerifyAsync(
            "donor@example.com",
            CreateOptions(),
            http,
            CancellationToken.None);

        Assert.Equal(DonationVerificationStatus.Unavailable, result.Status);
    }

    [Fact]
    public void CreateDonationPageUri_IncludesApplicationAndEncodedEmail()
    {
        Uri uri = DonationVerificationClient.CreateDonationPageUri("donor+tag@example.com");

        Assert.Equal(
            "https://www.purelogiccode.com/donate?app=5do&email=donor%2Btag%40example.com",
            uri.AbsoluteUri);
    }

    [Fact]
    public void CreateDonationPageUri_OmitsMissingEmail()
    {
        Uri uri = DonationVerificationClient.CreateDonationPageUri(null);

        Assert.Equal("https://www.purelogiccode.com/donate?app=5do", uri.AbsoluteUri);
    }

    private static DonationVerificationOptions CreateOptions()
    {
        return new DonationVerificationOptions
        {
            Endpoint = "https://example.test/api/donations/verify",
            ApiKey = "test-key",
            ApplicationSlug = "5do",
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public RecordingHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public HttpMethod? Method { get; private set; }

        public string? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri?.ToString();
            ApiKey = request.Headers.TryGetValues("X-API-KEY", out var values)
                ? string.Join(",", values)
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            };
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
