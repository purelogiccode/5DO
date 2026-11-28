# Lobby service (VPS prototype)

**Status:** prototype, 2026-11-28. The lobby is an ASP.NET Core 11 service that runs on
a Windows VPS. It is emulator-agnostic and is shared by 5DO and XGrid; each emulator
keeps its own state, handshake, and frame-sync protocol.

The service does four jobs:

1. Register rooms and human join codes over HTTP.
2. Exchange room presence and transport state over WebSockets.
3. Discover public UDP endpoints and coordinate NAT hole punching.
4. Relay the emulator control channel over TCP and the emulator data channel over UDP
   when a direct path is blocked.

It never interprets save states, frame hashes, input packets, or guest network frames.

## 1. What runs where

| Component | Runs on | Responsibility |
| --- | --- | --- |
| `FiveDo.Lobby` | Windows VPS | Room registry, join codes, WebSocket signaling, TCP control relay, UDP rendezvous, UDP relay |
| `FiveDo.Lobby.Protocol` | Both sides | Shared HTTP/WebSocket models, UDP datagram codec, TCP relay handshake |
| 5DO | Player PC | TCP state transfer, UDP lockstep input and hashes, desktop lobby dialog and `--lobby` client path |
| XGrid | Player PC | Its own handshake and guest-network framing over the same control/data relays |
| `FiveDo.LobbyProbe` | Any test machine | Headless room/control/UDP probe without a disc or BIOS |

The VPS sees only opaque bytes after the relay handshake. It does not need a 3DO BIOS,
an Xbox BIOS, a game image, or any emulator dependency.

## 2. Windows VPS quick start

### 2.1 Publish and install as a Windows service

The server targets `net11.0` and uses Kestrel. The install script publishes a
self-contained `win-x64` single-file executable, writes production settings, creates the
`FiveDoLobby` service, and opens the firewall ports.

```powershell
# Elevated PowerShell, from the repository root.
.\packaging\windows\install-lobby-service.ps1 `
    -PublicHost lobby.example.com `
    -RelayHost lobby-direct.example.com `
    -HttpPort 8080 `
    -UdpPort 5700 `
    -Force
```

The VPS does **not** need the .NET runtime when the script publishes self-contained.
The build machine needs the .NET 11 SDK.

Manual equivalent:

```powershell
dotnet publish src\FiveDo.Lobby\FiveDo.Lobby.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=false `
    -o C:\FiveDoLobby

sc.exe create FiveDoLobby binPath= "C:\FiveDoLobby\FiveDo.Lobby.exe" start= auto
sc.exe start FiveDoLobby
```

Publishing with an explicit RID can add RID-specific entries to the `packages.lock.json`
files in a source checkout. For a deployment copy that is expected; if you publish from
your development tree, restore those files afterwards or publish from a clean copy.

`appsettings.Production.json` beside the executable overrides the checked-in defaults:

```json
{
  "Urls": "http://0.0.0.0:8080",
  "Lobby": {
    "PublicHost": "lobby.example.com",
    "RelayHost": "lobby-direct.example.com",
    "UdpPort": 5700,
    "ControlRelayPortStart": 40000,
    "ControlRelayPortEnd": 40999,
    "UdpRelayPortStart": 41000,
    "UdpRelayPortEnd": 41999,
    "RoomLifetimeMinutes": 30,
    "RoomIdleTimeoutMinutes": 10,
    "MaxRooms": 500,
    "EnableRelay": true
  }
}
```

Set `PublicHost` to the public HTTP/WebSocket address. Set `RelayHost` to a DNS-only
address that points directly at the VPS for the TCP/UDP relay ports; when it is empty,
`PublicHost` is reused. When both are empty, the service uses the HTTP request host, which
works for LAN tests but is fragile behind a proxy.

### 2.2 Firewall

| Port | Protocol | Purpose |
| --- | --- | --- |
| 8080 | TCP | HTTP room API and WebSocket signaling |
| 5700 | UDP | Rendezvous, registration, peer exchange |
| 40000-40999 | TCP | Per-room control/state relay |
| 41000-41999 | UDP | Per-room opaque data relay |

The install script creates inbound rules for exactly these ranges. Change both the
application settings and the firewall rules if you use different ports.

### 2.3 Health and logs

```powershell
Invoke-RestMethod http://127.0.0.1:8080/healthz
Get-Service FiveDoLobby
Get-WinEvent -LogName Application -MaxEvents 50 |
    Where-Object ProviderName -like '*FiveDo*'
```

The service logs room creation, join, relay pairing, and cleanup through the standard
ASP.NET Core logger.

### 2.4 Update and uninstall

```powershell
# Republish and replace the service.
.\packaging\windows\install-lobby-service.ps1 -PublicHost lobby.example.com -Force

# Stop and remove the service; keep files and firewall rules.
.\packaging\windows\uninstall-lobby-service.ps1

# Also remove the firewall rules and the install directory.
.\packaging\windows\uninstall-lobby-service.ps1 -RemoveFirewallRules -RemoveFiles
```

### 2.5 Company subdomain on IIS + Cloudflare

The existing `purelogiccode.com` IIS site is a Razor Pages storefront. Do not add the
lobby as a page or area inside that app: the lobby owns UDP/TCP relay listeners, a
WebSocket endpoint, and an always-running process. Run it as a **separate IIS site on the
same VPS** instead:

```powershell
.\packaging\windows\install-lobby-iis.ps1 `
    -HostName lobby.purelogiccode.com `
    -RelayHost lobby-direct.purelogiccode.com `
    -CertificateThumbprint <thumbprint> `
    -Force
```

The script publishes the lobby, creates the `FiveDoLobby` app pool and IIS site, writes
`appsettings.Production.json`, adds the relay firewall rules, and preserves the existing
website. Without `-CertificateThumbprint` it creates a temporary HTTP binding on port 80;
use that only for staging.

When the VPS does not have the source tree, publish on the build machine and run the
package installer on the server:

```powershell
# Build machine
dotnet publish src\FiveDo.Lobby\FiveDo.Lobby.csproj -c Release --no-restore `
    -p:DebugType=None -p:DebugSymbols=false -o artifacts\lobby\iis-publish
tar -czf artifacts\lobby\fivedo-lobby-iis.tar.gz -C artifacts\lobby\iis-publish .
scp artifacts\lobby\fivedo-lobby-iis.tar.gz administrator@<vps>:C:/Deploy/
scp packaging\windows\install-lobby-iis-package.ps1 administrator@<vps>:C:/Deploy/

# VPS (elevated)
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Deploy\install-lobby-iis-package.ps1 `
    -PackagePath C:\Deploy\fivedo-lobby-iis.tar.gz `
    -HostName lobby.purelogiccode.com `
    -RelayHost lobby-direct.purelogiccode.com `
    -CertificateThumbprint <thumbprint> `
    -Force
```

DNS:

| Name | Type | Proxy | Purpose |
| --- | --- | --- | --- |
| `lobby.purelogiccode.com` | A → VPS IP | Proxied (orange) | HTTPS room API and WebSocket |
| `lobby-direct.purelogiccode.com` | A → VPS IP | **DNS only (grey)** | TCP control relay and UDP rendezvous/relay |

Cloudflare does not proxy arbitrary UDP or the high TCP relay ports. The relay hostname
must be DNS-only; otherwise players will punch and relay against Cloudflare instead of the
VPS. The lobby reports these two names separately through `PublicHost` and `RelayHost`.

TLS:

- The existing `purelogiccode.com` certificate covers `purelogiccode.com`, `www`, and
  `mail`, not `lobby`. Issue a certificate that covers
  `lobby.purelogiccode.com` and install it in `LocalMachine\My`.
- The easiest option for the proxied host is a **Cloudflare Origin Certificate**:
  Cloudflare dashboard -> select `purelogiccode.com` -> **SSL/TLS** -> **Origin
  Server** -> **Create Certificate** -> include `*.purelogiccode.com` and
  `purelogiccode.com` -> choose RSA -> create -> download the certificate and private
  key. The private key is shown only once; save it immediately. If an older origin
  certificate exists but the key was lost, create a new one.
- Convert the PEM pair to a PFX for IIS, import it, and rebind
  `FiveDoLobby` to the new thumbprint. The install script accepts the thumbprint
  through `-CertificateThumbprint`.
- The Cloudflare Origin CA is not in the public browser trust store; that is expected.
  Use this certificate only on the Cloudflare-proxied `lobby` host, not on the DNS-only
  `lobby-direct` host.
- Set Cloudflare SSL/TLS to **Full (strict)** once the origin certificate is installed.
  Flexible SSL is a staging-only fallback.
- WebSockets work through Cloudflare and IIS; the IIS WebSocket feature is already
  enabled on the verified VPS. The application calls `UseWebSockets`.

Firewall rules on the VPS:

```powershell
New-NetFirewallRule -DisplayName 'FiveDo Lobby UDP rendezvous' -Direction Inbound `
    -Action Allow -Protocol UDP -LocalPort 5700 -Profile Any
New-NetFirewallRule -DisplayName 'FiveDo Lobby TCP control relay' -Direction Inbound `
    -Action Allow -Protocol TCP -LocalPort 40000-40999 -Profile Any
New-NetFirewallRule -DisplayName 'FiveDo Lobby UDP relay' -Direction Inbound `
    -Action Allow -Protocol UDP -LocalPort 41000-41999 -Profile Any
```

Verify after the site starts:

```powershell
curl.exe -s https://lobby.purelogiccode.com/healthz
```

The existing storefront remains at `https://www.purelogiccode.com/`; the lobby is an
independent app pool and content root (`C:\inetpub\purelogiccode.lobby`).

**Validated deployment (2026-11-27):** a Windows Server VPS with IIS and Cloudflare
hosted `lobby.purelogiccode.com` (proxied) and `lobby-direct.purelogiccode.com`
(DNS-only). A 600-frame 5DO lobby session ran through the VPS relay with matching
host/join frame hash `0x110E7DC5` and audio hash `0x035F6315`, 0 reconnects, and no
failures. The same control path was exercised by `FiveDo.LobbyProbe`. Direct NAT between
the Azure host and a home network also passed. The hardened deployment returned five
room creates and then `429` with `Retry-After`; the health probe bypassed the limiter;
the 600-frame session still matched after hardening. The origin lock is also live:
Cloudflare requests return `200`, direct-origin requests without the secret return `403`,
and direct requests with the secret return `200`.

### 2.6 Standalone HTTPS

The prototype can also run as a standalone Kestrel service. It listens on HTTP so the UDP
and TCP endpoints are easy to test. Put IIS, Caddy, nginx, or another TLS terminator in
front of the HTTP endpoint before exposing join codes to the internet:

- Terminate TLS on `443` and proxy WebSockets to `http://127.0.0.1:8080`.
- Keep the public `PublicHost` set to the TLS host name.
- The UDP rendezvous and relay ports are independent of TLS and must still be opened.
- Use a real certificate; do not use a self-signed certificate for players.

## 3. 5DO usage

The desktop **Netplay** dialog supports both direct IP and the online lobby. Open the
disc and BIOS, start the game, then:

- **Host (desktop):** **Netplay > Host Session...**, select **Use the online lobby**,
  leave the lobby URL at `https://lobby.purelogiccode.com`, and select **Create lobby
  room**. Share the six-character room code. **Transport** selects automatic punching
  with relay fallback, forced relay, or direct only. The host closes the room when
  **Stop Session** is selected.
- **Join (desktop):** **Netplay > Join Session...**, select **Use the online lobby**,
  enter the room code, and select **Join lobby room**.

Both peers must use the same disc, BIOS, and behavior-affecting settings. The desktop uses
its saved settings; a CLI peer must match them (for example, add `--high-res` when the
desktop has **High resolution** enabled, or use the same `--profile`).

The 5DO CLI can also create or join a lobby room directly.

Host:

```powershell
5do netplay host game.chd --bios bios/panafz1.bin `
    --lobby http://lobby.example.com:8080 `
    --frames 3600
```

The host prints a join code, for example:

```text
5DO netplay lobby: room X7JTX4; share the join code with player 2.
```

Join:

```powershell
5do netplay join X7JTX4 game.chd --bios bios/panafz1.bin `
    --lobby http://lobby.example.com:8080 `
    --frames 3600
```

Options:

| Option | Meaning |
| --- | --- |
| `--lobby <url>` | Absolute `http` or `https` lobby URL |
| `--lobby-name <name>` | Display name shown in room status |
| `--lobby-relay` | Force the TCP/UDP relay path |
| `--lobby-direct` | Require direct punching; no relay fallback |
| `--lobby-timeout <seconds>` | Direct-punch deadline before relay fallback (default 3) |

The host uses the lobby TCP relay for state transfer and the UDP rendezvous for input and
hash traffic. The join does the same. If direct punching does not produce a game packet
within the deadline, both sides switch to the relay automatically. `--lobby-relay` is
useful for validating the relay path; `--lobby-direct` is useful for debugging NAT
behavior.

The resulting JSON includes `lobby`, `roomCode`, and `transport` in addition to the
direct-IP fields.

## 4. Emulator-agnostic control plane

Rooms carry the following tags:

| Field | Example | Meaning |
| --- | --- | --- |
| `emulator` | `5do` or `xgrid` | Emulator family |
| `protocolVersion` | `1` | The emulator-facing handshake version |
| `contentId` | disc or title id | Opaque content identity |
| `contentHash` | optional | Optional content fingerprint |
| `maxPlayers` | `2` | Prototype supports two slots |
| `playerSlot` | `0` or `1` | Host or join |

The lobby rejects a join when `emulator`, `protocolVersion`, `contentId`, or a supplied
`contentHash` does not match the room. The emulator still performs its own full state and
content validation; the lobby check is only an early UX rejection.

### 4.1 HTTP API

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/healthz` | Health, room count, UDP port, uptime |
| `POST` | `/v1/rooms` | Create a room and return the host token |
| `POST` | `/v1/rooms/{code}/join` | Join a room and return a slot token |
| `GET` | `/v1/rooms/{code}` | Public room status |
| `DELETE` | `/v1/rooms/{code}` | Close a room with the host bearer token |
| `GET` | `/v1/signal` | WebSocket signaling upgrade |

Create:

```http
POST /v1/rooms
Content-Type: application/json

{
  "emulator": "5do",
  "protocolVersion": 1,
  "contentId": "T2K-USA",
  "contentHash": "0x1234567890abcdef",
  "displayName": "Alice",
  "maxPlayers": 2
}
```

The response contains `room.joinCode`, `hostToken`, `room.udpRoomId`, and these
endpoints:

```json
{
  "room": {
    "udpRoomId": "1c55f1a4cd7ed5d1",
    "joinCode": "YRERN9",
    "emulator": "5do",
    "protocolVersion": 1,
    "contentId": "T2K-USA",
    "signal": { "host": "lobby.example.com", "port": 8080 },
    "signalPath": "/v1/signal",
    "controlRelay": { "host": "lobby.example.com", "port": 40000 },
    "udpRendezvous": { "host": "lobby.example.com", "port": 5700 },
    "udpRelay": { "host": "lobby.example.com", "port": 41000 },
    "expiresAt": "2026-11-27T18:00:00Z"
  },
  "hostToken": "857574888d93464cb3d1a39d0e05583e"
}
```

Join uses the same metadata plus an optional `slotToken` for a reconnect. It returns
`room`, `slotToken`, and `slot`.

Errors are structured:

```json
{ "code": "content_mismatch", "message": "The room content identity does not match." }
```

### 4.2 WebSocket signaling

Connect to `ws(s)://<host>:<port>/v1/signal?room=<joinCode>&token=<slotToken>`.

Server messages:

| `type` | Meaning |
| --- | --- |
| `welcome` | Initial room snapshot and assigned slot |
| `peer-joined` | The other slot joined |
| `peer-left` | The other slot left |
| `transport` | A peer reported `auto`, `direct`, or `relay` |
| `ready` | A peer reported readiness |
| `closed` | The room closed |
| `pong` | Reply to a client `ping` |
| `error` | Invalid or oversized client message |

Client messages:

```json
{ "type": "ready" }
{ "type": "transport", "mode": "direct" }
{ "type": "ping" }
{ "type": "leave" }
```

The 5DO CLI does not require the WebSocket; it uses HTTP for room setup and the relays
for traffic. Desktop shells and XGrid can use the socket for presence and transport
status.

### 4.3 TCP control relay

Each room gets one TCP relay port. The relay is byte-transparent after a small
pre-handshake, so 5DO's framed control channel and XGrid's handshake/state payloads both
work unchanged.

Each peer connects and sends a 32-byte header:

```text
0   "5DLC"
4   version (1)
5   player slot (0 or 1)
6-7 reserved
8-15 room id (little-endian)
16-31 slot token (GUID bytes)
```

The relay validates the room id and token, pairs one slot 0 with one slot 1, and sends
both an 8-byte preamble:

```text
0   "5DLP"
4   version (1)
5-7 reserved
```

After the preamble the relay copies bytes in both directions. The host and join must
both send the connect header before either reads the preamble. A reconnect opens a new
pair; the room and its relay port stay alive.

### 4.4 UDP rendezvous and relay

The rendezvous datagram is 48 bytes, IPv4 only:

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 4 | magic `5DOL` |
| 4 | 1 | version `1` |
| 5 | 1 | type |
| 6 | 1 | player slot |
| 7 | 1 | transport mode (`0` auto, `1` direct, `2` relay) |
| 8 | 8 | room id, little-endian |
| 16 | 16 | slot token, or zero |
| 32 | 8 | nonce, little-endian |
| 40 | 4 | peer IPv4 address, network order |
| 44 | 2 | peer port, little-endian |
| 46 | 2 | relay port, little-endian |

Types:

| Type | Direction | Meaning |
| --- | --- | --- |
| `Register` | client to 5700 | Register or refresh the game socket's public endpoint |
| `RegisterAck` | server to client | Registered; includes the peer endpoint if known |
| `Peer` | server to client | Peer public endpoint and punch nonce |
| `Punch` | client to peer | Open the NAT mapping toward the peer |
| `RelayHello` | client to relay port | Register the game socket at the relay |
| `RelayAck` | relay to client | Relay registration accepted |
| `Bye` | client to server | Leave the room |
| `Keepalive` | client to 5700 | Refresh registration |

The client sends `Register` from the same UDP socket that carries game traffic. The
server records the public source endpoint. When both slots are registered, each client
receives the other's endpoint and a punch nonce, then sends `Punch` packets directly to
the peer. If both NATs are cone-like, the game packets flow directly.

If no direct game packet arrives within the deadline, the client sends `RelayHello` to
the room's UDP relay port and sends game traffic to the relay. The relay records each
source endpoint from `RelayHello` and forwards every non-lobby datagram unchanged to the
other slot. It never parses the emulator payload.

## 5. XGrid integration (later, in the XGrid repository)

Do not edit the XGrid tree from a 5DO change. The shared contract is the HTTP/WebSocket
API, the TCP relay handshake, and the UDP datagram format above. When XGrid integrates:

1. Tag rooms with `emulator: "xgrid"` and an XGrid handshake `protocolVersion`.
2. Use `contentId` for the XBE title id or image identity.
3. Keep the XGrid handshake/state payload on the TCP control relay.
4. Keep guest Ethernet/UDP framing on the UDP direct/relay path. The relay is
   byte-transparent and preserves datagram boundaries.
5. Reuse the same auto/direct/relay decision: direct first, relay after the deadline.
6. Keep XGrid's own state transfer, frame sync, and hashing out of the lobby.

The local XGrid shell currently has no netplay UI, so the first XGrid step is a headless
client against `FiveDo.LobbyProbe` and the documented relay before any UI work.

## 6. Headless probe

`tools/FiveDo.LobbyProbe` exercises the control plane without a disc or BIOS. It creates
or joins a room, opens the WebSocket, pairs the TCP relay, registers UDP endpoints, and
exchanges `PING`/`PONG` datagrams.

Host:

```powershell
dotnet run --project tools\FiveDo.LobbyProbe -c Release -- `
    host --lobby http://127.0.0.1:8080 `
    --emulator xgrid --content probe-test --pings 20 --interval 500
```

Join:

```powershell
dotnet run --project tools\FiveDo.LobbyProbe -c Release -- `
    join --lobby http://127.0.0.1:8080 --code <JOIN_CODE> `
    --emulator xgrid --content probe-test
```

Use `--relay` to force the UDP relay, `--direct` to require punching, and `--no-ws` to
skip WebSocket signaling. The summary prints the path (`Direct` or `Relay`), local and
peer endpoints, and packet counts.

## 7. Security and limitations

This is a prototype, not a production matchmaking service.

- Rooms and tokens are in memory only; restarting the service drops every room.
- Tokens are bearer secrets. Use HTTPS, do not put tokens in logs, and do not share the
  host token with the joining player.
- There is no account system, moderation, or abuse reporting yet. Rate limits and
  per-IP quotas are described in §7.1.
- The UDP protocol is IPv4 only. IPv6 and dual-stack are future work.
- Exactly two player slots are supported.
- The relay forwards packets without encryption or integrity checks. Emulator protocols
  must provide their own authentication/integrity if they need it.
- The origin IP is reachable without Cloudflare unless you restrict it. Add IIS IP
  Security/Cloudflare IP allowlisting or Cloudflare Authenticated Origin Pulls before
  treating the origin as private. `TrustCloudflareHeaders` is only trustworthy when
  direct origin access is blocked; otherwise a caller can spoof `CF-Connecting-IP`.
- Relay traffic counts against VPS bandwidth. Size the plan for the expected number of
  simultaneous sessions.
- The service is designed for a small number of rooms (`MaxRooms` default 500). Each
  relayed room consumes one TCP and one UDP port.
- `PublicHost` must be the address players can reach. A private LAN address in the room
  response will not work over the internet.

### 7.1 Rate limits and quotas

| Setting | Default | Effect |
| --- | --- | --- |
| `MaxRooms` | 500 | Global live-room cap |
| `MaxRoomsPerIp` | 5 | Live rooms created by one client IP |
| `RoomCreatePerMinute` | 5 | Room-create requests per IP per minute |
| `RoomJoinPerMinute` | 20 | Join requests per IP per minute |
| `SignalPerMinute` | 10 | WebSocket upgrades per IP per minute |
| `GlobalRequestsPerMinute` | 120 | All HTTP requests per IP per minute |
| `MaxUdpRelayBytesPerSecond` | 1,000,000 | Per-room UDP relay budget; `0` disables |
| `MaxControlRelayMinutes` | 10 | Maximum TCP control relay pair lifetime; `0` disables |
| `MaxRendezvousDatagramsPerSecond` | 5,000 | Global rendezvous datagram budget; `0` disables |
| `MaxRequestBytes` | 65,536 | HTTP request body cap |

HTTP limits return `429` with a JSON body and `Retry-After`. The health endpoint bypasses
all limiters so a monitor is never rate-limited.

`TrustCloudflareHeaders` controls whether `CF-Connecting-IP` is used for the IP
partition. Enable it only when the site is reachable exclusively through Cloudflare; if
the origin is directly reachable, a client can spoof that header. The IIS deployment
scripts set it to `true` by default.

The UDP relay drops packets above the per-room byte budget. The TCP control relay closes
pairs after the configured lifetime. These are abuse caps, not traffic shaping; size them
for the highest expected emulator rate.

### 7.2 Logs and monitoring

- IIS writes HTTP access logs under `C:\inetpub\logs\LogFiles\W3SVC<site-id>`. Find the
  lobby site id with `appcmd list site FiveDoLobby`.
- In-process ASP.NET Core errors and warnings are in the Windows `Application` event log:

```powershell
Get-WinEvent -LogName Application -MaxEvents 50 |
    Where-Object { $_.ProviderName -match 'IIS|ASP.NET|Runtime' }
```

- Run the health probe from Task Scheduler every 5 minutes:

```powershell
.\packaging\windows\Test-LobbyHealth.ps1
```

  It exits `0` when `/healthz` reports `status: ok` and `1` otherwise. Point an external
  uptime monitor at `https://lobby.purelogiccode.com/healthz` as the first alert.
- Do not use `Test-NetConnection lobby-direct.purelogiccode.com -Port 40000` as a health
  check: relay listeners exist only while a room is active.
- Cloudflare Analytics shows request volume and error rates. Keep **Full (strict)** TLS
  enabled after the origin certificate is installed.

### 7.3 Origin lock

The origin IP can be reached without Cloudflare unless it is locked down. The app
supports an origin-secret header that blocks direct-origin HTTP/WebSocket requests.

Cloudflare setup:

1. **Rules → Transform Rules → Modify Request Header → Create rule**
2. Expression: `http.host eq "lobby.purelogiccode.com"`
3. Set static header `X-FiveDo-Origin-Secret` to a random value of at least 32
   characters (64 hex characters works well).
4. Deploy the rule.

Store the same value on the VPS and install with:

```powershell
.\install-lobby-iis-package.ps1 `
    -PackagePath C:\Deploy\fivedo-lobby-iis.tar.gz `
    -HostName lobby.purelogiccode.com `
    -RelayHost lobby-direct.purelogiccode.com `
    -CertificateThumbprint <thumbprint> `
    -OriginSecretFile C:\Deploy\lobby-origin-secret.txt `
    -TrustCloudflareHeaders `
    -Force
```

The app returns `403 {"code":"forbidden"}` when the header is missing or wrong.
Loopback `/healthz` requests are exempt so the local health probe keeps working.
Enable the app-side secret only after the Cloudflare rule is live; otherwise public
requests will be rejected.

Verify:

```powershell
# Through Cloudflare: 200
Invoke-RestMethod https://lobby.purelogiccode.com/healthz

# Direct origin without the header: 403
curl.exe -k --resolve lobby.purelogiccode.com:443:107.174.146.107 `
    https://lobby.purelogiccode.com/healthz
```

### 7.4 Edge cache and rate limits

5DO clients select the production lobby with
`--lobby https://lobby.purelogiccode.com`; the room response advertises
`lobby-direct.purelogiccode.com` for the TCP/UDP relay endpoints.

The `purelogiccode.com` zone (currently the **Free** plan) carries these Cloudflare rules
for the lobby:

- **Cache bypass** (`http_request_cache_settings`, rule `FiveDo Lobby no-cache`):
  `http.host eq "lobby.purelogiccode.com" and (starts_with(http.request.uri.path, "/v1/") or http.request.uri.path eq "/healthz")`
  with `cache: false`. This covers the room API, WebSocket signaling, and health endpoint.
  Cloudflare may report `cf-cache-status: DYNAMIC` for a bypassed dynamic response; it must
  never report `HIT`.
- **Rate limiting**: the requested 30-requests/1-minute rule requires the Host field and a
  60-second window, which the Free plan does not offer (Free allows the Path field only, a
  10-second window, and one rule). The app-level limits in §7.1 remain the enforcement
  point: room-create 5/min, join 20/min, signal 10/min, global 120/min, with `/healthz`
  exempt. The exact edge rule requires Pro or higher.

Do not enable Bot Fight Mode or Under Attack Mode for this hostname; both can challenge
the 5DO/XGrid API clients. The zone is on `security_level: medium` with Bot Fight Mode off.

**Validated 2026-11-27** through `https://lobby.purelogiccode.com`: room create/join/delete
returned `201/200/204`, WebSocket signaling returned `101` and a `welcome` message, the TCP
control relay paired on `lobby-direct.purelogiccode.com:40000`, and
`FiveDo.LobbyProbe --relay` completed with 7 of 8 UDP ping/pong replies over the relay path
(UDP loss is expected); both peers exited `0`. A direct-origin request without
`X-FiveDo-Origin-Secret` returned `403`, and `CF-Connecting-IP` partitioning is enabled by
`Lobby:TrustCloudflareHeaders=true` in `appsettings.Production.json`.

## 8. Troubleshooting

| Symptom | Check |
| --- | --- |
| `room_not_found` | Join code case, room expiry, service restart |
| `content_mismatch` | Same disc/title, BIOS, and tuning; same `contentHash` |
| Control relay connects but never pairs | Both players used the same room; host and join both sent the 32-byte header |
| UDP path stays `Auto` | Both players registered from the game socket; UDP 5700 and the relay range are open |
| Direct fails, relay works | Symmetric NAT or restrictive firewall; expected fallback |
| Relay works but game desyncs | The lobby is transparent; investigate the emulator's own state/hash path |
| Browser WebSocket upgrade fails | Reverse proxy must forward `Upgrade`/`Connection` headers |

For local validation without a VPS:

```powershell
dotnet run --project src\FiveDo.Lobby -c Debug --urls http://127.0.0.1:8080
dotnet test tests\FiveDo.Tests\FiveDo.Tests.csproj --filter "FullyQualifiedName~FiveDo.Tests.Lobby"
```
