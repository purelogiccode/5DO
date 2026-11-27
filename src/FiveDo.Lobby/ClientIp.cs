using System;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace FiveDo.Lobby;

/// <summary>Resolves the client address used for quotas and rate-limit partitions.</summary>
internal static class ClientIp
{
    /// <summary>Resolves the client IP, optionally trusting Cloudflare's edge header.</summary>
    /// <param name="context">HTTP context.</param>
    /// <param name="options">Lobby options.</param>
    /// <returns>A stable partition key.</returns>
    public static string Resolve(HttpContext context, LobbyOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (options.TrustCloudflareHeaders)
        {
            string forwarded = context.Request.Headers["CF-Connecting-IP"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded) && IPAddress.TryParse(forwarded, out _))
            {
                return forwarded;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
