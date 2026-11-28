# 5DO 1.20.0

**Released:** 2026-11-28

5DO 1.20.0 is the lobby prototype feature release. An emulator-agnostic ASP.NET Core 11
service provides room and join codes, WebSocket signaling, per-room TCP control relays,
UDP NAT punch coordination, and UDP relay fallback. The 5DO CLI reaches it with
`5do netplay host|join --lobby <url>`, and `FiveDo.LobbyProbe` exercises the same contract
without a disc or BIOS.

## Download

| System | File |
|---|---|
| Windows 10/11 x64 | [`5do-1.20.0-win-x64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-win-x64.zip) |
| Windows on ARM | [`5do-1.20.0-win-arm64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-win-arm64.zip) |
| macOS on Apple Silicon | [`5do-1.20.0-osx-arm64.dmg`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-osx-arm64.dmg) |
| Debian/Ubuntu/Mint x64 | [`5do_1.20.0_amd64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do_1.20.0_amd64.deb) |
| Debian/Ubuntu/Mint arm64 | [`5do_1.20.0_arm64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do_1.20.0_arm64.deb) |
| Fedora/RHEL/openSUSE x64 | [`5do-1.20.0-1.linux.x86_64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-1.linux.x86_64.rpm) |
| Fedora/RHEL/openSUSE arm64 | [`5do-1.20.0-1.linux.aarch64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-1.linux.aarch64.rpm) |
| Any Linux, no install (x64) | [`5do-1.20.0-linux-x86_64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-linux-x86_64.AppImage) |
| Any Linux, no install (arm64) | [`5do-1.20.0-linux-aarch64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-linux-aarch64.AppImage) |
| Any Linux, sandboxed (x64) | [`5do-1.20.0-linux-x86_64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-linux-x86_64.flatpak) |
| Any Linux, sandboxed (arm64) | [`5do-1.20.0-linux-aarch64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-linux-aarch64.flatpak) |
| Any Linux, portable folder | [`5do-1.20.0-linux-x64.tar.gz`](https://github.com/purelogiccode/5DO/releases/download/v1.20.0/5do-1.20.0-linux-x64.tar.gz) (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is embedded
in the self-contained single-file apphosts. Extract the archive before running it.
`SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Lobby service

- **Room API:** `POST /v1/rooms`, `GET /v1/rooms/{code}`,
  `POST /v1/rooms/{code}/join`, and `DELETE /v1/rooms/{code}` with the host bearer token.
- **Signaling:** `GET /v1/signal` upgrades to a WebSocket and reports presence, readiness,
  and transport changes.
- **Relays:** each room allocates a TCP control relay and a UDP relay. The UDP rendezvous
  on port 5700 coordinates direct punching; peers fall back to the relay when punching
  fails.
- **Public service:** `https://lobby.purelogiccode.com` for the API and signaling, with
  `lobby-direct.purelogiccode.com` (DNS-only) for the TCP/UDP relay endpoints.
- **CLI:** `5do netplay host <disc> --bios <bios> --lobby <url>` and
  `5do netplay join <code> <disc> --bios <bios> --lobby <url>`. Use `--lobby-relay` to
  force the relay, `--lobby-direct` to require direct punching, and `--lobby-timeout` to
  set the direct-punch deadline (default 3 seconds).
- **Probe:** `FiveDo.LobbyProbe host|join --lobby <url>` validates the room, signaling,
  control relay, and UDP relay without an emulator.

The service supports two players and keeps rooms in memory. Host and slot tokens are
bearer secrets; relay payloads are opaque and not encrypted. The app enforces 5 room
creates, 20 joins, 10 signaling upgrades, and 120 total requests per IP per minute, with
`/healthz` exempt. The Cloudflare edge adds a cache-bypass rule and an origin-secret
header; the Free plan cannot enforce the requested 30-requests/1-minute rate rule, so the
app limits remain the enforcement point.

## Verification

- Full Release suite **2,688/2,688**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING` (0 results), and `mkdocs build --strict` clean.
- The public service was validated end to end on 2026-11-27: room create/join/delete
  returned `201/200/204`, WebSocket signaling returned `101` and a `welcome` message, the
  TCP control relay paired on `lobby-direct:40000`, and `FiveDo.LobbyProbe --relay`
  exchanged 7 of 8 UDP ping/pong replies over the relay path; both peers exited `0`.
- A direct-origin request without `X-FiveDo-Origin-Secret` returned `403`; Cloudflare
  requests returned `200` with `Cache-Control: no-store` and `cf-cache-status: DYNAMIC`.
- The 1.19.0 direct-IP netplay evidence remains valid: a 36,000-frame CLI host/join matched
  `0x342EA767` / audio `0xB6AB50D0`, and desktop host/join both matched their CLI peers.

## Known issues

- The lobby is a prototype. Rooms and tokens are in memory only; restarting the service
  drops every room.
- The desktop lobby UI and XGrid client integration are not included. Use the CLI
  `--lobby` option or `FiveDo.LobbyProbe`.
- Relay data is not encrypted, and rooms are limited to two players.
- There is no room persistence, account system, moderation, or automated
  origin-certificate renewal.
- The Cloudflare Free plan cannot enforce the requested edge rate rule; the app-level
  limits are the enforcement point.
- Direct-IP netplay still has no authentication or encryption; use a trusted network.
