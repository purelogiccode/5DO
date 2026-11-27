# 5DO 1.19.0

**Released:** 2026-11-27

5DO 1.19.0 is the direct-IP netplay feature release. Two players can host and join from
the desktop or CLI; the host transfers a compressed machine state over TCP, then both
instances exchange per-frame controller input and frame/audio hashes over UDP. Reconnect
resumes from the host's current state.

## Download

| System | File |
|---|---|
| Windows 10/11 x64 | [`5do-1.19.0-win-x64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-win-x64.zip) |
| Windows on ARM | [`5do-1.19.0-win-arm64.zip`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-win-arm64.zip) |
| macOS on Apple Silicon | [`5do-1.19.0-osx-arm64.dmg`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-osx-arm64.dmg) |
| Debian/Ubuntu/Mint x64 | [`5do_1.19.0_amd64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do_1.19.0_amd64.deb) |
| Debian/Ubuntu/Mint arm64 | [`5do_1.19.0_arm64.deb`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do_1.19.0_arm64.deb) |
| Fedora/RHEL/openSUSE x64 | [`5do-1.19.0-1.linux.x86_64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-1.linux.x86_64.rpm) |
| Fedora/RHEL/openSUSE arm64 | [`5do-1.19.0-1.linux.aarch64.rpm`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-1.linux.aarch64.rpm) |
| Any Linux, no install (x64) | [`5do-1.19.0-linux-x86_64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-linux-x86_64.AppImage) |
| Any Linux, no install (arm64) | [`5do-1.19.0-linux-aarch64.AppImage`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-linux-aarch64.AppImage) |
| Any Linux, sandboxed (x64) | [`5do-1.19.0-linux-x86_64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-linux-x86_64.flatpak) |
| Any Linux, sandboxed (arm64) | [`5do-1.19.0-linux-aarch64.flatpak`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-linux-aarch64.flatpak) |
| Any Linux, portable folder | [`5do-1.19.0-linux-x64.tar.gz`](https://github.com/purelogiccode/5DO/releases/download/v1.19.0/5do-1.19.0-linux-x64.tar.gz) (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is embedded
in the self-contained single-file apphosts. Extract the archive before running it.
`SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Netplay

- **Desktop:** **Netplay > Host Session...** or **Join Session...**, with port, input-delay,
  and wait-timeout fields, automatic reconnect prompts, and session status.
- **CLI:** `5do netplay host <disc> --bios <bios>` and
  `5do netplay join <host> <disc> --bios <bios>`, with `--port`, `--delay`,
  `--input-script`, `--reconnect-timeout`, `--high-res`, and `--json`.
- Host: player 1 and the listening TCP/UDP port. Join: player 2.
- The host sends a compressed state (about 650 KB) and the state frame hash over TCP. The
  join validates the state before running.
- Per-frame input and cumulative frame/audio hashes travel over UDP. Eight redundant input
  entries and explicit input requests recover short and burst packet loss.
- Reconnect: the join sends its last confirmed frame/hash; the host sends its current state
  and both resume at that frame.
- Input delay 2-4 frames is suitable for a LAN; 6-10 frames covers roughly 50-100 ms
  one-way latency.

There is no lobby, matchmaking, NAT traversal, authentication, or encryption. Use a
trusted LAN or a direct connection, and do not expose the port to the public Internet
without a VPN or firewall rules you understand. Two players and joypads are supported;
pointer devices and more than two players are out of scope.

## Verification

- Full Release suite **2,677/2,677**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING` (0 results), `mkdocs build --strict`, and frontend
  smoke clean.
- 36,000-frame CLI host/join with different P1/P2 scripts: final frame `0x342EA767`, audio
  `0xB6AB50D0`, matching the single-machine reference from the probe.
- Reconnect: host state at frame 304, join resumed with 896 frames; both finished
  `0xA328CCCC` / audio `0x8B40B3CB`.
- Desktop host + CLI join and desktop join + CLI host both completed 600 frames with
  matching frame/audio hashes. Desktop-host reconnect resumed at frame 7367.
- The probe and CLI reports are committed under `docs/data/netplay-*.json`; the protocol
  and measurements are in [NetplayProbe.md](NetplayProbe.md).
- CI builds the five bundles and Linux packages and runs the Windows/Linux CLI and
  frontend smokes.

## Known issues

- Netplay has no authentication or encryption; use a trusted network.
- Pause, rewind, fast-forward, state load, frame skip, and run-ahead are disabled during a
  session. Lobby, NAT traversal, more than two players, and pointer devices are not
  included.
- A two-window desktop smoke with two physical controllers remains recommended
  post-release hardening.
- MDS/CCD real-image parity remains `SYS-29`; preparing a large split/MDX image can pause
  the desktop while it is joined or decoded.

## Links

- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.19.0>; release run
  <https://github.com/purelogiccode/5DO/actions/runs/45960321450>.
- [Installation](https://purelogiccode.github.io/5DO/Installation.html)
- [User guide](https://purelogiccode.github.io/5DO/UserGuide.html#netplay-direct-ip)
- [Netplay probe](https://purelogiccode.github.io/5DO/NetplayProbe.html)
- [What's new](https://purelogiccode.github.io/5DO/WhatsNew.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)
- [Full changelog](https://github.com/purelogiccode/5DO/blob/master/CHANGELOG.md)
- [License](https://github.com/purelogiccode/5DO/blob/master/LICENSE) and
  [third-party notices](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md)
