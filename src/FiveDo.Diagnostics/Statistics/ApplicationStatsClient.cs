using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Diagnostics.Statistics;

/// <summary>
/// Records one application-launch hit in the ApplicationStats service.
/// </summary>
/// <remarks>
/// <para>
/// The call is best-effort and runs on a background task so startup is never blocked. Only
/// the application id and version are sent; no user, machine, or game data is included.
/// </para>
/// <para>
/// The service rate-limits to one call per hour per IP and application. A 429 or any other
/// failure is logged at Debug and ignored; stats failures never surface as warnings or bug
/// reports. Set <c>FIVEDO_STATS=0</c> to opt out; CI and test runs are disabled automatically.
/// </para>
/// </remarks>
public static class ApplicationStatsClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static Task? _pending;

    /// <summary>Starts a background launch-statistics request unless disabled.</summary>
    public static void TrackLaunch()
    {
        if (!IsEnabled() || Volatile.Read(ref _pending) is not null)
        {
            return;
        }

        var task = TrackLaunchAsync(CreateOptions(), Http);
        _ = Interlocked.CompareExchange(ref _pending, task, null);
    }

    /// <summary>Waits briefly for the pending launch request so short CLI runs can finish it.</summary>
    /// <param name="timeout">Maximum time to wait.</param>
    public static void WaitForPending(TimeSpan timeout)
    {
        Task? pending = Volatile.Read(ref _pending);
        if (pending is null)
        {
            return;
        }

        try
        {
            _ = pending.Wait(timeout);
        }
        catch (AggregateException)
        {
            // The request is best-effort and already logs its own failure.
        }
    }

    /// <summary>Sends one launch hit. Exposed to tests with an injected HTTP handler.</summary>
    /// <param name="options">Endpoint, credentials, and application identity.</param>
    /// <param name="httpClient">HTTP client to use.</param>
    /// <returns>A task that completes when the request finishes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    internal static async Task TrackLaunchAsync(ApplicationStatsOptions options, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        try
        {
            var payload = new ApplicationStatsPayload(options.ApplicationId, options.Version);
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            request.Content = JsonContent.Create(payload, ApplicationStatsJsonContext.Default.ApplicationStatsPayload);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Serilog.Log.Debug(
                    "Application stats request returned HTTP {Status}.",
                    (int)response.StatusCode);
                return;
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (body.Contains("\"error\"", StringComparison.OrdinalIgnoreCase))
            {
                Serilog.Log.Debug("Application stats request was rejected: {Body}.", body);
                return;
            }

            Serilog.Log.Debug(
                "Application stats recorded for {ApplicationId} {Version}.",
                options.ApplicationId,
                options.Version);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or InvalidOperationException
            or NotSupportedException)
        {
            Serilog.Log.Debug(exception, "Application stats request failed.");
        }
    }

    /// <summary>Gets a value indicating whether launch statistics are enabled.</summary>
    internal static bool IsEnabled()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("FIVEDO_STATS"),
                "0",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsTruthy("CI") && !IsTruthy("GITHUB_ACTIONS") && !IsTruthy("TF_BUILD");
    }

    private static ApplicationStatsOptions CreateOptions()
    {
        var applicationId = Environment.GetEnvironmentVariable("FIVEDO_STATS_APPLICATION_ID");
        var apiKey = Environment.GetEnvironmentVariable("FIVEDO_STATS_API_KEY");
        var endpoint = Environment.GetEnvironmentVariable("FIVEDO_STATS_ENDPOINT");
        return new ApplicationStatsOptions
        {
            ApplicationId = string.IsNullOrWhiteSpace(applicationId)
                ? ApplicationStatsOptions.DefaultApplicationId
                : applicationId,
            ApiKey = string.IsNullOrWhiteSpace(apiKey)
                ? ApplicationStatsOptions.DefaultApiKey
                : apiKey,
            Endpoint = string.IsNullOrWhiteSpace(endpoint)
                ? ApplicationStatsOptions.DefaultEndpoint
                : endpoint,
        };
    }

    private static bool IsTruthy(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }
}
