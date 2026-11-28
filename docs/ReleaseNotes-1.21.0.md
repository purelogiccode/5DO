# 5DO 1.21.0

**Released:** 2026-11-28

5DO 1.21.0 is the desktop lobby feature release. The 1.20.0 lobby service and CLI are now
available from **Netplay > Host Session...** and **Join Session...**: create or join a
room with a six-character code, choose automatic direct punching with relay fallback,
forced relay, or direct only, close the room on **Stop Session**, and reconnect through
the stored room token. The 1.19.0 direct-IP netplay and the 1.18.x feature set remain.

## Download

| System | File |
|---|---|
| Windows 10/11 x64 | [`5do-1.21.0-win-x64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-win-x64.zip) |
| Windows on ARM | [`5do-1.21.0-win-arm64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-win-arm64.zip) |
| macOS on Apple Silicon | [`5do-1.21.0-osx-arm64.dmg`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-osx-arm64.dmg) |
| Debian/Ubuntu/Mint x64 | [`5do_1.21.0_amd64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do_1.21.0_amd64.deb) |
| Debian/Ubuntu/Mint arm64 | [`5do_1.21.0_arm64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do_1.21.0_arm64.deb) |
| Fedora/RHEL/openSUSE x64 | [`5do-1.21.0-1.linux.x86_64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-1.linux.x86_64.rpm) |
| Fedora/RHEL/openSUSE arm64 | [`5do-1.21.0-1.linux.aarch64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-1.linux.aarch64.rpm) |
| Any Linux, no install (x64) | [`5do-1.21.0-linux-x86_64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-linux-x86_64.AppImage) |
| Any Linux, no install (arm64) | [`5do-1.21.0-linux-aarch64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-linux-aarch64.AppImage) |
| Any Linux, sandboxed (x64) | [`5do-1.21.0-linux-x86_64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-linux-x86_64.flatpak) |
| Any Linux, sandboxed (arm64) | [`5do-1.21.0-linux-aarch64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-linux-aarch64.flatpak) |
| Any Linux, portable folder | [`5do-1.21.0-linux-x64.tar.gz`](https://github.com/purelogiccode/5DO/releases/download/v1.21.0/5do-1.21.0-linux-x64.tar.gz) (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is embedded
in the self-contained single-file apphosts. Extract the archive before running it.
`SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Desktop lobby

- **Host:** open the disc and BIOS, start the game, then **Netplay > Host Session...**,
  check **Use the online lobby**, leave the URL at `https://lobby.purelogiccode.com`, and
  select **Create lobby room**. The dialog shows the six-character room code.
- **Join:** **Netplay > Join Session...**, check **Use the online lobby**, enter the code,
  and select **Join lobby room**.
- **Transport:** Automatic negotiates direct UDP punching and falls back to the relay;
  Force relay uses the relay only; Direct only fails rather than relaying.
- **Stop and reconnect:** **Stop Session** closes the host room. A drop offers reconnect,
  which reuses the stored room and token.
- **Automation:** `--netplay-host` and `--netplay-join <code>` accept `--netplay-lobby
  <url>`; `--netplay-code-out <path>` writes the hosted code and `--netplay-result <path>`
  writes the session result.

Both peers must use the same BIOS, disc, and behavior-affecting settings. The desktop uses
its saved settings; a CLI peer must match them, for example with `--high-res` when **High
resolution** is enabled.

## CLI lobby

The 1.20.0 CLI flow is unchanged:

```text
5do netplay host game.chd --bios bios/panafz1.bin --lobby https://lobby.purelogiccode.com
5do netplay join X7JTX4 game.chd --bios bios/panafz1.bin --lobby https://lobby.purelogiccode.com
```

`--lobby-relay` forces the relay, `--lobby-direct` requires direct punching, and
`--lobby-timeout` sets the direct-punch deadline. `FiveDo.LobbyProbe` exercises the room,
signaling, TCP relay, and UDP relay without a disc.

## Fixes and hardening

- **Malformed lobby data:** invalid room ids, host/slot tokens, and out-of-range slots
  now produce a dialog error instead of escaping the dialog's exception handling. Added
  regression coverage.
- **Lobby stop timeout:** closing a host room now times out after five seconds, so
  **Stop Session** cannot hang when the lobby is unreachable.
- **Invalid library timestamp:** the scan stores an out-of-range Win32 file time as
  unknown and continues, with a `LibraryCacheTests` regression.
- **macOS release packaging:** bundle hashing retries while the disk image closes,
  avoiding a rare CI failure.

## Verification

- Full Release suite **2,694/2,694**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING` (0 results), and `mkdocs build --strict` clean.
- Desktop host + CLI join (room `F9EH4J`) and CLI host + desktop join (room `B5K46B`)
  each completed 600 frames through the public service with `ok=true`, direct transport,
  final frame hash `820256197`, and audio hash `56582933`.
- A packaged Windows x64 bundle hosted room `YAZ5DP` and joined it with the bundled CLI:
  600 frames, `ok=true`, direct transport, frame hash `820256197`, audio `56582933`.
- The 1.20.0 lobby service remains live at `https://lobby.purelogiccode.com` and was
  validated end to end before this release.

## Known issues

- The lobby is a prototype. Rooms and tokens are in memory only; restarting the service
  drops every room.
- Rooms are limited to two players. Relay data is not encrypted; treat room and host
  tokens as bearer secrets.
- XGrid client integration, room persistence, matchmaking, moderation, and automated
  origin-certificate renewal remain.
- Direct-IP netplay still has no authentication or encryption; use a trusted network.
