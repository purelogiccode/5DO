using System.Text.Json.Serialization;

namespace FiveDo.Lobby.Protocol;

/// <summary>Source-generated JSON contracts for the lobby API and WebSocket.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreateRoomRequest))]
[JsonSerializable(typeof(JoinRoomRequest))]
[JsonSerializable(typeof(CreateRoomResponse))]
[JsonSerializable(typeof(JoinRoomResponse))]
[JsonSerializable(typeof(RoomConnectionInfo))]
[JsonSerializable(typeof(RoomStatus))]
[JsonSerializable(typeof(RoomPlayerStatus))]
[JsonSerializable(typeof(RoomPlayerStatus[]))]
[JsonSerializable(typeof(LobbyEndpoint))]
[JsonSerializable(typeof(LobbyError))]
[JsonSerializable(typeof(SignalMessage))]
[JsonSerializable(typeof(LobbyHealth))]
public sealed partial class LobbyJsonContext : JsonSerializerContext
{
}
