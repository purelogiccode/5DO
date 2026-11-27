using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FiveDo.Lobby;

/// <summary>Periodically removes expired rooms and releases their relay ports.</summary>
public sealed class RoomCleanupService : BackgroundService
{
    private readonly RoomManager _rooms;
    private readonly ILogger<RoomCleanupService> _logger;

    /// <summary>Initializes the cleanup service.</summary>
    /// <param name="rooms">Room registry.</param>
    /// <param name="logger">Logger.</param>
    public RoomCleanupService(RoomManager rooms, ILogger<RoomCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(logger);
        _rooms = rooms;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                int removed = _rooms.RemoveExpired(DateTimeOffset.UtcNow);
                if (removed > 0)
                {
                    LobbyLog.RoomsRemoved(_logger, removed);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LobbyLog.RoomCleanupFailed(_logger, exception);
            }
        }
    }
}
