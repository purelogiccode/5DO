# What's new in 5DO 1.19.0

**Released:** 2026-11-27

5DO 1.19.0 adds **direct-IP two-player netplay**. One player hosts from the desktop or
CLI, the other joins, and the host transfers a compressed machine state before both
instances exchange per-frame controller input. The same core checks frame and audio hashes
so a divergence stops the session instead of silently drifting.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.19.0-win-x64.zip` |
| Windows on ARM | `5do-1.19.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.19.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.19.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.19.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.19.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.19.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable folder | `5do-1.19.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Each bundle contains both the desktop app and the CLI, and the .NET 11 runtime is embedded
in the self-contained single-file apphosts. Extract the archive before running it.
`SHA256SUMS.txt` covers all five bundles and the Linux packages;
`SHA256SUMS-linux.txt` covers the Linux installers. No `.asc` signatures or keyring are
published.

## Netplay

- Host from **Netplay > Host Session...** or `5do netplay host <disc> --bios <bios>`.
- Join from **Netplay > Join Session...** or `5do netplay join <host> <disc> --bios <bios>`.
- The host transfers a compressed state over TCP (about 650 KB), validates the frame hash
  on the join, then both peers exchange input and hashes over UDP.
- Input delay is configurable (2-4 frames on a LAN, 6-10 for 50-100 ms latency). The host
  sets the delay and the join uses it.
- Lost input packets are recovered by an eight-entry redundant window and explicit input
  requests. Reconnect resumes from the host's current state; the join sends its last
  confirmed frame/hash in the request.
- The desktop and CLI both support host/join. A 36,000-frame two-process run with different
  P1/P2 scripts matched the single-machine reference; desktop host and join were each
  validated against a CLI peer with matching frame/audio hashes.

There is **no lobby, matchmaking, NAT traversal, authentication, or encryption**. Use
netplay on a trusted LAN or a direct connection, and do not expose the port to the public
Internet without a VPN or firewall rules you understand. Two players and joypads are
supported; pointer devices and more than two players are out of scope.

## Other changes

- The desktop locks out pause, reset, rewind, fast-forward, state load, frame skip, and
  run-ahead while a netplay session is active, so both peers execute every frame.
- The session step runs at the start of each emulated frame (`BeforeFrame`) rather than the
  V1 PBUS read, so a frame that skips V1 cannot leave the session behind.
- `--netplay-host` / `--netplay-join <host>` automate the desktop dialog for acceptance
  runs; `5do netplay` accepts `--high-res`, `--true-2x`, and `--no-audio-hash` diagnostics.
- The 1.18.x MDS/MDX, CCD/IMG/SUB, MP4, interpolation, void-bar, live-clock, and FFmpeg
  installer features are unchanged.

## Verification

- Full Release suite **2,677/2,677**; build 0 warnings, `dotnet format`,
  `jb inspectcode --severity=WARNING` (0 results), `mkdocs build --strict`, and frontend
  smoke clean.
- 36,000-frame CLI host/join with different P1/P2 scripts: final frame `0x342EA767`, audio
  `0xB6AB50D0`, matching the single-machine reference.
- Reconnect: host resumed at frame 304; both peers finished `0xA328CCCC` / audio
  `0x8B40B3CB`.
- Desktop host + CLI join and desktop join + CLI host both completed 600 frames with
  matching frame/audio hashes; desktop-host reconnect resumed at frame 7367.
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

- [Installation](https://purelogiccode.github.io/5DO/Installation.html)
- [User guide](https://purelogiccode.github.io/5DO/UserGuide.html#netplay-direct-ip)
- [Netplay probe and evidence](https://purelogiccode.github.io/5DO/NetplayProbe.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)
- [Full changelog](https://github.com/purelogiccode/5DO/blob/master/CHANGELOG.md)
- [License](https://github.com/purelogiccode/5DO/blob/master/LICENSE) and
  [third-party notices](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md)
