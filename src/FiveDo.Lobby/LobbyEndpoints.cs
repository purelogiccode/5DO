using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FiveDo.Lobby;

/// <summary>HTTP room management endpoints.</summary>
public static class LobbyEndpoints
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    /// <summary>Maps the lobby HTTP API.</summary>
    /// <param name="app">Application builder.</param>
    public static void MapLobbyEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _ = app.MapGet("/healthz", GetHealth).DisableRateLimiting();
        _ = app.MapPost("/v1/rooms", CreateRoomAsync).RequireRateLimiting("room-create");
        _ = app.MapPost("/v1/rooms/{code}/join", JoinRoomAsync).RequireRateLimiting("room-join");
        _ = app.MapGet("/v1/rooms/{code}", GetRoom);
        _ = app.MapDelete("/v1/rooms/{code}", DeleteRoom).RequireRateLimiting("room-create");
    }

    private static IResult GetHealth(
        RoomManager rooms,
        UdpLobbyService udp)
    {
        var health = new LobbyHealth(
            "ok",
            rooms.Count,
            rooms.MaxRooms,
            udp.Port,
            Math.Max(0, (DateTimeOffset.UtcNow - StartedAt).TotalSeconds));
        return Results.Json(health, LobbyJsonContext.Default.LobbyHealth);
    }

    private static async Task<IResult> CreateRoomAsync(
        HttpContext context,
        RoomManager rooms,
        IOptions<LobbyOptions> options,
        UdpLobbyService udp)
    {
        CreateRoomRequest? request;
        try
        {
            request = await JsonSerializer
                .DeserializeAsync(
                    context.Request.Body,
                    LobbyJsonContext.Default.CreateRoomRequest,
                    context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return Error(HttpStatusCode.BadRequest, "invalid_json", exception.Message);
        }

        if (request is null)
        {
            return Error(HttpStatusCode.BadRequest, "invalid_json", "A JSON request body is required.");
        }

        try
        {
            string clientIp = ClientIp.Resolve(context, options.Value);
            Room room = rooms.CreateRoom(request, DateTimeOffset.UtcNow, clientIp);
            string publicHost = ResolvePublicHost(context, options.Value);
            string relayHost = ResolveRelayHost(context, options.Value);
            int signalPort = ResolveSignalPort(context);
            var response = new CreateRoomResponse
            {
                Room = room.ToConnectionInfo(publicHost, signalPort, udp.Port, relayHost),
                HostToken = room.HostToken,
            };
            context.Response.Headers.Location = $"/v1/rooms/{room.JoinCode}";
            return Results.Json(
                response,
                LobbyJsonContext.Default.CreateRoomResponse,
                statusCode: StatusCodes.Status201Created);
        }
        catch (LobbyApiException exception)
        {
            return Error(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    private static async Task<IResult> JoinRoomAsync(
        string code,
        HttpContext context,
        RoomManager rooms,
        IOptions<LobbyOptions> options,
        UdpLobbyService udp)
    {
        JoinRoomRequest? request;
        try
        {
            request = await JsonSerializer
                .DeserializeAsync(
                    context.Request.Body,
                    LobbyJsonContext.Default.JoinRoomRequest,
                    context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return Error(HttpStatusCode.BadRequest, "invalid_json", exception.Message);
        }

        if (request is null)
        {
            return Error(HttpStatusCode.BadRequest, "invalid_json", "A JSON request body is required.");
        }

        try
        {
            RoomJoin join = rooms.Join(code, request, DateTimeOffset.UtcNow);
            string publicHost = ResolvePublicHost(context, options.Value);
            string relayHost = ResolveRelayHost(context, options.Value);
            int signalPort = ResolveSignalPort(context);
            var response = new JoinRoomResponse
            {
                Room = join.Room.ToConnectionInfo(publicHost, signalPort, udp.Port, relayHost),
                SlotToken = join.SlotToken,
                Slot = join.Slot,
            };
            return Results.Json(response, LobbyJsonContext.Default.JoinRoomResponse);
        }
        catch (LobbyApiException exception)
        {
            return Error(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    private static IResult GetRoom(string code, RoomManager rooms)
    {
        if (!rooms.TryGetRoom(code, out Room room))
        {
            return Error(HttpStatusCode.NotFound, "room_not_found", "The join code was not found.");
        }

        return Results.Json(
            room.Snapshot(DateTimeOffset.UtcNow),
            LobbyJsonContext.Default.RoomStatus);
    }

    private static IResult DeleteRoom(
        string code,
        HttpContext context,
        RoomManager rooms)
    {
        if (!AuthenticationHeaderValue.TryParse(
                context.Request.Headers.Authorization.ToString(),
                out AuthenticationHeaderValue? authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Error(HttpStatusCode.Unauthorized, "unauthorized", "A bearer host token is required.");
        }

        try
        {
            return rooms.CloseRoom(code, authorization.Parameter)
                ? Results.NoContent()
                : Error(HttpStatusCode.NotFound, "room_not_found", "The join code was not found.");
        }
        catch (LobbyApiException exception)
        {
            return Error(exception.StatusCode, exception.Code, exception.Message);
        }
    }

    private static string ResolvePublicHost(HttpContext context, LobbyOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PublicHost))
        {
            return options.PublicHost;
        }

        string host = context.Request.Host.Host;
        return string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
    }

    private static string ResolveRelayHost(HttpContext context, LobbyOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RelayHost))
        {
            return options.RelayHost;
        }

        return ResolvePublicHost(context, options);
    }

    private static int ResolveSignalPort(HttpContext context)
    {
        return context.Request.Host.Port ?? (context.Request.IsHttps ? 443 : 80);
    }

    private static IResult Error(HttpStatusCode statusCode, string code, string message)
    {
        return Results.Json(
            new LobbyError(code, message),
            LobbyJsonContext.Default.LobbyError,
            statusCode: (int)statusCode);
    }
}
