# What's new in 5DO 1.21.0

**Released:** 2026-11-28

5DO 1.21.0 brings the online lobby to the desktop app. **Netplay > Host Session...** and
**Join Session...** now offer **Use the online lobby**, so two players can create or join
a room by six-character code without configuring ports or IP addresses. The host chooses
automatic direct punching with relay fallback, forced relay, or direct only. **Stop
Session** closes the host room, and a dropped session reconnects through the stored room
token. The direct-IP host/join path from 1.19.0 is unchanged.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.21.0-win-x64.zip` |
| Windows on ARM | `5do-1.21.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.21.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.21.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.21.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.21.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.21.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable folder | `5do-1.21.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is embedded
in the self-contained single-file apphosts. Extract the archive before running it.
`SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Desktop lobby

1. Open your disc, select the BIOS, and start the game.
2. **Netplay > Host Session...**, check **Use the online lobby**, leave the lobby URL at
   `https://lobby.purelogiccode.com`, and select **Create lobby room**.
3. Share the six-character room code.
4. On the other PC, **Netplay > Join Session...**, check **Use the online lobby**, enter
   the code, and select **Join lobby room**.
5. **Transport** selects Automatic (direct, then relay), Force relay, or Direct only.

Both peers must use the same BIOS, disc, and behavior-affecting settings. The desktop uses
its saved settings; a CLI peer must match them, for example with `--high-res` when **High
resolution** is enabled. The CLI keeps the same `--lobby` flow:

```text
5do netplay host game.chd --bios bios/panafz1.bin --lobby https://lobby.purelogiccode.com
5do netplay join X7JTX4 game.chd --bios bios/panafz1.bin --lobby https://lobby.purelogiccode.com
```

## Fixes and hardening

- Malformed lobby data now surfaces as a dialog error instead of an unhandled
  exception, with regression coverage; closing a host room times out after five seconds
  when the lobby is unreachable.
- The library scan tolerates an out-of-range Win32 timestamp per file instead of failing
  the scan; the existing missing-native startup check and OpenGL framebuffer fallback
  remain covered.
- The macOS bundle-hashing step retries while the disk image closes, avoiding a rare
  release-pipeline failure.

## Other changes

- The 1.20.0 lobby service, protocol client, `--lobby` CLI options, and
  `FiveDo.LobbyProbe` remain available.
- The 1.19.0 direct-IP netplay remains: TCP compressed-state transfer, UDP lockstep,
  reconnect, and frame/audio hash checks.
- The 1.18.x feature set remains: MDS/MDX and CCD/IMG/SUB disc images, optional
  FFmpeg-based H.264/AAC MP4 recording, one-click FFmpeg installation, high-refresh frame
  interpolation, patterned GPU void bars, and live CPU-clock changes.

## Verification

- Full Release suite **2,694/2,694**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING`, and `mkdocs build --strict` clean.
- Desktop host + CLI join and CLI host + desktop join each completed 600 frames through
  `https://lobby.purelogiccode.com` with `ok=true`, direct transport, and matching
  frame/audio hashes.
- A packaged Windows x64 bundle hosted a room and joined it with the bundled CLI,
  completing 600 frames with `ok=true`.

## Known issues

- The lobby is a prototype; restarting the service drops every room. Rooms are limited to
  two players, and relay data is not encrypted. XGrid client integration, room
  persistence, and matchmaking remain.
