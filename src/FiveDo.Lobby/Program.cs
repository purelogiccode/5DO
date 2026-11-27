using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FiveDo.Lobby.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FiveDo.Lobby;

/// <summary>The lobby executable entry point.</summary>
internal static class LobbyProgram
{
    /// <summary>Starts the lobby web application.</summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        int maxRequestBytes = builder.Configuration.GetValue("Lobby:MaxRequestBytes", 64 * 1024);
        builder.WebHost.ConfigureKestrel(
            options => options.Limits.MaxRequestBodySize = maxRequestBytes);
        builder.Services.Configure<IISServerOptions>(
            options => options.MaxRequestBodySize = maxRequestBytes);

        builder.Services.AddWindowsService(
            static options => options.ServiceName = "FiveDo Lobby");
        builder.Services.Configure<LobbyOptions>(
            builder.Configuration.GetSection(LobbyOptions.SectionName));
        builder.Services.AddHttpLogging(
            options => options.LoggingFields = HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode);
        builder.Services.AddRateLimiter(
            options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter = Math
                            .Ceiling(retryAfter.TotalSeconds)
                            .ToString(CultureInfo.InvariantCulture);
                    }

                    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                        new LobbyError("rate_limited", "Too many requests."),
                        LobbyJsonContext.Default.LobbyError);
                    await context.HttpContext.Response.Body
                        .WriteAsync(payload, cancellationToken)
                        .ConfigureAwait(false);
                };
                options.AddPolicy(
                    "room-create",
                    context => FixedPartition(context, static value => value.RoomCreatePerMinute, "room-create"));
                options.AddPolicy(
                    "room-join",
                    context => FixedPartition(context, static value => value.RoomJoinPerMinute, "room-join"));
                options.AddPolicy(
                    "signal",
                    context => FixedPartition(context, static value => value.SignalPerMinute, "signal"));
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context => FixedPartition(
                        context,
                        static value => value.GlobalRequestsPerMinute,
                        "global"));
            });
        builder.Services.AddSingleton<RoomManager>();
        builder.Services.AddSingleton<UdpLobbyService>();
        builder.Services.AddHostedService(
            static services => services.GetRequiredService<UdpLobbyService>());
        builder.Services.AddHostedService<RoomCleanupService>();

        WebApplication app = builder.Build();
        app.UseHttpLogging();
        app.Use(
            async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Cache-Control"] = "no-store";
                if (context.Request.IsHttps)
                {
                    context.Response.Headers["Strict-Transport-Security"] =
                        "max-age=31536000; includeSubDomains";
                }

                await next().ConfigureAwait(false);
            });
        app.Use(
            async (context, next) =>
            {
                LobbyOptions options = context.RequestServices
                    .GetRequiredService<IOptions<LobbyOptions>>()
                    .Value;
                bool loopbackHealth = context.Request.Path.Equals(
                        "/healthz",
                        StringComparison.OrdinalIgnoreCase)
                    && context.Connection.RemoteIpAddress is { } remoteAddress
                    && IPAddress.IsLoopback(remoteAddress);
                if (!string.IsNullOrEmpty(options.OriginSecret) && !loopbackHealth)
                {
                    string provided = context.Request.Headers[options.OriginSecretHeader].ToString();
                    if (!OriginSecretMatches(provided, options.OriginSecret))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                            new LobbyError("forbidden", "The origin secret is missing or invalid."),
                            LobbyJsonContext.Default.LobbyError);
                        await context.Response.Body
                            .WriteAsync(payload, context.RequestAborted)
                            .ConfigureAwait(false);
                        return;
                    }
                }

                await next().ConfigureAwait(false);
            });
        app.UseWebSockets(
            new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30),
            });
        app.UseRateLimiter();
        app.MapLobbyEndpoints();
        app.MapLobbySignal();
        app.Run();
    }

    private static bool OriginSecretMatches(string provided, string expected)
    {
        if (provided.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }

    private static RateLimitPartition<string> FixedPartition(
        HttpContext context,
        Func<LobbyOptions, int> limit,
        string prefix)
    {
        LobbyOptions options = context.RequestServices
            .GetRequiredService<IOptions<LobbyOptions>>()
            .Value;
        string key = $"{prefix}:{ClientIp.Resolve(context, options)}";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit(options),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }
}
