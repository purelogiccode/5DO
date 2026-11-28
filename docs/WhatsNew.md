# What's new in 5DO 1.20.0

**Released:** 2026-11-28

5DO 1.20.0 adds the **emulator-agnostic VPS lobby prototype**. A host creates a room and
shares the six-character join code; the joining player connects through WebSocket
signaling, then the two peers punch UDP directly or fall back to the TCP/UDP relays. The
service is emulator-agnostic, so XGrid can use the same room contract.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.20.0-win-x64.zip` |
| Windows on ARM | `5do-1.20.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.20.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.20.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.20.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.20.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.20.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable folder | `5do-1.20.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is embedded
in the self-contained single-file apphosts. Extract the archive before running it.
`SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Lobby

- Create a room:

  ```text
  5do netplay host game.chd --bios bios/panafz1.bin --lobby https://lobby.purelogiccode.com
  ```

- Join with the code printed by the host:

  ```text
  5do netplay join X7JTX4 game.chd --bios bios/panafz1.bin --lobby https://lobby.purelogiccode.com
  ```

- The room response supplies the signaling host, TCP control relay, UDP rendezvous, and
  UDP relay endpoints. The client does not need any relay configuration.
- `--lobby-relay` forces the relay path, `--lobby-direct` requires direct punching, and
  `--lobby-timeout` sets the direct-punch deadline (default 3 seconds).
- `FiveDo.LobbyProbe` exercises the same room, WebSocket, TCP relay, and UDP relay
  contract without a disc or BIOS.
- The public health endpoint is `https://lobby.purelogiccode.com/healthz`.

The prototype supports two players and in-memory rooms. Tokens are bearer secrets, relay
data is not encrypted, and there is no desktop lobby UI, XGrid client integration, room
persistence, or account system yet. The service enforces 5 room creates, 20 joins,
10 signaling upgrades, and 120 total requests per IP per minute; `/healthz` is exempt.

## Netplay and other changes

- The 1.19.0 direct-IP netplay remains: desktop **Netplay > Host/Join Session**, the
  `5do netplay host|join` CLI, TCP compressed-state transfer, UDP delay-based lockstep,
  reconnect, and frame/audio hash checks.
- The 1.18.x feature set remains: read-only Alcohol MDS/MDX and CloneCD CCD/IMG/SUB
  images, optional FFmpeg-based H.264/AAC MP4 recording, one-click FFmpeg installation,
  high-refresh frame interpolation, patterned GPU void bars, and live CPU-clock changes.
- Rolling autosave, ZIP/7z disc containers, physical CD-DA, the Steam Deck layout, Arcade
  CRT, RetroAchievements, TAS movies, rewind/run-ahead, cheats, and the library are
  unchanged.

## Verification

- Full Release suite **2,688/2,688**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING`, and `mkdocs build --strict` clean.
- The public service was validated end to end on 2026-11-27: room create/join/delete
  returned `201/200/204`, WebSocket signaling returned `101` with a `welcome` message,
  the TCP control relay paired, and the UDP relay exchanged 7/8 probe pings.
- Cloudflare runs the lobby host proxied with Full (strict), a cache-bypass rule, and an
  origin-secret header; `lobby-direct` is DNS-only for relay traffic.

## Known issues

- The lobby is a prototype; restarting the service drops every room.
- The desktop lobby UI and XGrid client integration are not included.
- Relay data is not encrypted, and rooms are limited to two players.
- The Cloudflare Free plan cannot enforce the requested 30-requests/1-minute edge rule;
  the app-level limits above are the enforcement point.
- No room persistence, moderation, account system, or automated origin-certificate
  renewal.
