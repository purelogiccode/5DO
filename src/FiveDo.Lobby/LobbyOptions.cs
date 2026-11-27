using System;
using System.Diagnostics.CodeAnalysis;

namespace FiveDo.Lobby;

/// <summary>Configuration for the in-memory lobby service.</summary>
public sealed class LobbyOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Lobby";

    /// <summary>
    /// Gets or sets the public host name advertised to clients. When empty, the HTTP
    /// request host is used. Set this when the service is behind a reverse proxy.
    /// </summary>
    public string PublicHost { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DNS-only host used by the TCP control relay and UDP endpoints.
    /// When empty, <see cref="PublicHost"/> is used. Use a host that is not behind a
    /// Cloudflare proxy because the relay ports are not HTTP.
    /// </summary>
    public string RelayHost { get; set; } = string.Empty;

    /// <summary>Gets or sets the UDP rendezvous port.</summary>
    public int UdpPort { get; set; } = 5700;

    /// <summary>Gets or sets the first TCP control relay port. Zero lets the OS choose.</summary>
    public int ControlRelayPortStart { get; set; } = 40000;

    /// <summary>Gets or sets the last TCP control relay port.</summary>
    public int ControlRelayPortEnd { get; set; } = 40999;

    /// <summary>Gets or sets the first UDP data relay port. Zero lets the OS choose.</summary>
    public int UdpRelayPortStart { get; set; } = 41000;

    /// <summary>Gets or sets the last UDP data relay port.</summary>
    public int UdpRelayPortEnd { get; set; } = 41999;

    /// <summary>Gets or sets the absolute room lifetime in minutes.</summary>
    public int RoomLifetimeMinutes { get; set; } = 30;

    /// <summary>Gets or sets the idle room timeout in minutes.</summary>
    public int RoomIdleTimeoutMinutes { get; set; } = 10;

    /// <summary>Gets or sets the maximum number of live rooms.</summary>
    public int MaxRooms { get; set; } = 500;

    /// <summary>Gets or sets the maximum live rooms created by one client IP.</summary>
    public int MaxRoomsPerIp { get; set; } = 5;

    /// <summary>Gets or sets whether <c>CF-Connecting-IP</c> is trusted for quotas and rate limits.</summary>
    [SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Bound from configuration and read by the rate limiter.")]
    public bool TrustCloudflareHeaders { get; set; }

    /// <summary>Gets or sets the request header injected by a Cloudflare Transform Rule.</summary>
    public string OriginSecretHeader { get; set; } = "X-FiveDo-Origin-Secret";

    /// <summary>
    /// Gets or sets the shared origin secret. When empty, direct-origin requests are not
    /// rejected; set it only after the Cloudflare Transform Rule is live.
    /// </summary>
    public string OriginSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets room creation requests allowed per IP per minute.</summary>
    public int RoomCreatePerMinute { get; set; } = 5;

    /// <summary>Gets or sets room join requests allowed per IP per minute.</summary>
    public int RoomJoinPerMinute { get; set; } = 20;

    /// <summary>Gets or sets WebSocket signaling upgrades allowed per IP per minute.</summary>
    public int SignalPerMinute { get; set; } = 10;

    /// <summary>Gets or sets all HTTP requests allowed per IP per minute.</summary>
    public int GlobalRequestsPerMinute { get; set; } = 120;

    /// <summary>Gets or sets the UDP relay byte budget per room per second. Zero disables the cap.</summary>
    public int MaxUdpRelayBytesPerSecond { get; set; } = 1_000_000;

    /// <summary>Gets or sets the maximum TCP control relay lifetime in minutes. Zero disables the cap.</summary>
    public int MaxControlRelayMinutes { get; set; } = 10;

    /// <summary>Gets or sets the rendezvous datagram budget per second. Zero disables the cap.</summary>
    public int MaxRendezvousDatagramsPerSecond { get; set; } = 5_000;

    /// <summary>Gets or sets the maximum HTTP request body size in bytes.</summary>
    public int MaxRequestBytes { get; set; } = 64 * 1024;

    /// <summary>Gets or sets whether TCP and UDP relays are allocated for new rooms.</summary>
    public bool EnableRelay { get; set; } = true;

    /// <summary>Validates the configuration.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a value is out of range.</exception>
    public void Validate()
    {
        if (UdpPort is < 0 or > 65535)
        {
            throw new InvalidOperationException("Lobby:UdpPort must be 0-65535.");
        }

        ValidateRange(ControlRelayPortStart, ControlRelayPortEnd, "ControlRelay");
        ValidateRange(UdpRelayPortStart, UdpRelayPortEnd, "UdpRelay");
        if (RoomLifetimeMinutes <= 0 || RoomIdleTimeoutMinutes <= 0)
        {
            throw new InvalidOperationException("Room lifetimes must be positive.");
        }

        if (MaxRooms <= 0 || MaxRoomsPerIp <= 0)
        {
            throw new InvalidOperationException("Lobby:MaxRooms and MaxRoomsPerIp must be positive.");
        }

        if (RoomCreatePerMinute <= 0
            || RoomJoinPerMinute <= 0
            || SignalPerMinute <= 0
            || GlobalRequestsPerMinute <= 0)
        {
            throw new InvalidOperationException("Lobby HTTP rate limits must be positive.");
        }

        if (MaxUdpRelayBytesPerSecond < 0
            || MaxControlRelayMinutes < 0
            || MaxRendezvousDatagramsPerSecond < 0
            || MaxRequestBytes is < 1024 or > 1024 * 1024)
        {
            throw new InvalidOperationException("Lobby relay or request limits are out of range.");
        }

        if (!string.IsNullOrEmpty(OriginSecret))
        {
            if (string.IsNullOrWhiteSpace(OriginSecretHeader) || OriginSecretHeader.Length > 64)
            {
                throw new InvalidOperationException("Lobby:OriginSecretHeader is invalid.");
            }

            if (OriginSecret.Length is < 32 or > 512)
            {
                throw new InvalidOperationException("Lobby:OriginSecret must be 32-512 characters.");
            }
        }
    }

    private static void ValidateRange(int start, int end, string name)
    {
        if (start is < 0 or > 65535
            || end is < 0 or > 65535
            || (start != 0 && (end == 0 || end < start)))
        {
            throw new InvalidOperationException(
                $"Lobby:{name}PortStart/End must be a valid port range, or start 0 for OS allocation.");
        }
    }
}
