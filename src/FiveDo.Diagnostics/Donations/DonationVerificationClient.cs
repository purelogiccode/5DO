using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Diagnostics.Donations;

/// <summary>
/// Checks the PureLogicCode website for a completed donation linked to an email and
/// this application.
/// </summary>
/// <remarks>
/// The request is only sent when the user asks to verify a donation or when the
/// desktop shell refreshes a previously saved donation email. The email is never
/// included in log messages.
/// </remarks>
public static class DonationVerificationClient
{
    /// <summary>The donation page used by the desktop shell.</summary>
    public const string DonationPageUrl = "https://www.purelogiccode.com/donate";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Builds the donation page URL with the application and optional email.</summary>
    /// <param name="email">Saved donation email, or <see langword="null"/> to omit it.</param>
    /// <returns>The donation page URI.</returns>
    public static Uri CreateDonationPageUri(string? email)
    {
        var url = $"{DonationPageUrl}?app={Uri.EscapeDataString(DonationVerificationOptions.DefaultApplicationSlug)}";
        if (!string.IsNullOrWhiteSpace(email))
        {
            url += $"&email={Uri.EscapeDataString(email.Trim())}";
        }

        return new Uri(url, UriKind.Absolute);
    }

    /// <summary>Verifies whether the supplied email has a completed donation for 5DO.</summary>
    /// <param name="email">Email address used on the donation page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The verification result. Network failures are reported as unavailable.</returns>
    public static Task<DonationVerificationResult> VerifyAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        VerifyAsync(email, new DonationVerificationOptions(), Http, cancellationToken);

    /// <summary>Sends one verification request. Exposed to tests with an injected handler.</summary>
    /// <param name="email">Email address used on the donation page.</param>
    /// <param name="options">Endpoint, credentials, and application identity.</param>
    /// <param name="httpClient">HTTP client to use.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The verification result. Network failures are reported as unavailable.</returns>
    internal static async Task<DonationVerificationResult> VerifyAsync(
        string email,
        DonationVerificationOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(email))
        {
            return new DonationVerificationResult(DonationVerificationStatus.NotDonated);
        }

        try
        {
            var payload = new DonationVerificationRequest
            {
                Email = email.Trim(),
                ApplicationSlug = options.ApplicationSlug,
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            request.Content = JsonContent.Create(
                payload,
                DonationVerificationJsonContext.Default.DonationVerificationRequest);
            request.Headers.TryAddWithoutValidation("X-API-KEY", options.ApiKey);

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogFailure(response.StatusCode);
                return new DonationVerificationResult(DonationVerificationStatus.Unavailable);
            }

            DonationVerificationResponse? result = await response.Content
                .ReadFromJsonAsync(
                    DonationVerificationJsonContext.Default.DonationVerificationResponse,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
            {
                Serilog.Log.Warning("The donation verification API returned an empty response.");
                return new DonationVerificationResult(DonationVerificationStatus.Unavailable);
            }

            return result.Donated
                ? new DonationVerificationResult(
                    DonationVerificationStatus.Donated,
                    result.TotalAmount,
                    result.LastDonationAtUtc)
                : new DonationVerificationResult(DonationVerificationStatus.NotDonated);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            Serilog.Log.Information(exception, "The donation verification request timed out.");
            return new DonationVerificationResult(DonationVerificationStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            Serilog.Log.Information(exception, "Could not reach the donation verification service.");
            return new DonationVerificationResult(DonationVerificationStatus.Unavailable);
        }
        catch (JsonException exception)
        {
            Serilog.Log.Warning(exception, "The donation verification API returned invalid JSON.");
            return new DonationVerificationResult(DonationVerificationStatus.Unavailable);
        }
        catch (InvalidOperationException exception)
        {
            Serilog.Log.Warning(exception, "The donation verification request could not be created.");
            return new DonationVerificationResult(DonationVerificationStatus.Unavailable);
        }
    }

    private static void LogFailure(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            Serilog.Log.Warning("The donation verification API rejected the configured API key.");
            return;
        }

        if (statusCode == HttpStatusCode.NotFound ||
            statusCode == HttpStatusCode.TooManyRequests ||
            code >= 500)
        {
            // A missing product or a temporary outage is not an application bug.
            Serilog.Log.Information(
                "Donation verification is temporarily unavailable. Status: {StatusCode}.",
                code);
            return;
        }

        Serilog.Log.Warning("Donation verification failed with status {StatusCode}.", code);
    }
}
