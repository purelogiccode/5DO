# Frequently Asked Questions

## General

### What is 5DO?

5DO is a source-available C# 15/.NET 11 emulator for the Panasonic 3DO Interactive
Multiplayer. It includes an Avalonia desktop app, a headless CLI, a deterministic
save-state and TAS format, and a test/differential harness.

### Is 5DO a fork of 4DO or FreeDO?

5DO is a C# port derived from the FreeDO 3DO emulation core. 4DO and Opera/libopera
are forks of FreeDO; their source and behavior are used as references. 5DO is not a
drop-in replacement for 4DO and does not load 4DO `.state` files.

### Is 5DO "open source"?

The source is publicly available, but the license is the **FreeDO modified GNU LGPL**,
which is not a standard OSI license and includes additional restrictions. 5DO is
**donationware**: non-commercial, source-available, and funded by voluntary donations.
The 2026-11-25 `REL-13` review retained those terms: written rights-holder permission has
not been obtained and a clean-room replacement has not been performed for this release.
Relicensing 5DO's original code alone would not remove restrictions on code derived from
FreeDO/4DO/Opera. The 4DO game database is a separate **GPL-2.0** data component with its
complete text in [`GPL-2.0.txt`](GPL-2.0.txt). Official Debian/Ubuntu/Fedora/openSUSE
archives and Flathub are not eligible under the retained terms. Read
[Licensing](Licensing.md) and the
[project license](https://github.com/purelogiccode/5DO/blob/master/LICENSE).

### Why is it called 5DO?

The project name is a play on 3DO. C# identifiers cannot start with a digit, so the
solution and namespace prefix use **FiveDo**.

## Getting started

### Does 5DO include a BIOS or games?

No. You must supply a legally obtained 3DO BIOS and legally obtained disc images. The
repository never includes BIOS ROMs, game images, or extracted game assets. 5DO uses
the real BIOS boot path; the Opera-style BIOS-less HLE setup is not used and is out of
scope for 1.x.

### Where do I get a BIOS?

Dump it from a 3DO console you own, or obtain it through a lawful source in your
jurisdiction. Do not ask for or share copyrighted BIOS files in issues, discussions, or
pull requests.

### Which disc formats are supported?

- ISO (2048-byte sectors)
- BIN (2352-byte sectors)
- CUE+BIN, including multi-track and CD-DA layouts
- CHD, including parent/child images
- DiscJuggler CDI and Nero NRG (read-only)
- Alcohol MDS/MDF and MDS v2/MDX (read-only)
- CloneCD CCD/IMG/SUB (read-only; the optional `.sub` is ignored)
- ISO, BIN, and CUE+BIN inside ZIP and 7z containers
- M3U/M3U8 playlists for multi-disc games

MDS images with a user password prompt, and CloneCD images with scrambled data tracks,
are rejected with an actionable message. Real 3DO MDS/CCD boot-hash parity is still
pending representative images (`SYS-29`).

### How do I open a game from a launcher?

```text
FiveDo.exe "{disc}" --bios "{bios}" --fullscreen
```

A positional disc path or `--disc <path>` opens and auto-starts the game. See
[Installation](Installation.md) and the [User guide](UserGuide.md).

### Is there a download?

Yes - **v1.18.1** is available on
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.18.1). There are
five bundles: Windows x64/arm64 and Linux x64/arm64 ZIP/tar.gz archives, plus a macOS
arm64 DMG. Each bundle contains both the desktop app and the CLI, and the .NET 11
runtime is embedded, so no separate runtime install is required. Intel macOS is not
supported. Linux also has `.deb`, `.rpm`, AppImage, and Flatpak downloads. You can also
build from source.

## Compatibility

### How compatible is 5DO?

The compatibility matrix covers **378 game-test-set titles**: **375 are playable under
the binary beyond-title rule**, and the three exceptions are non-retail/add-on images
with documented root causes. Against the peer lists, 5DO has **0 `Behind` and 0
`Regression` rows** and is a strict superset of the **252-title peer union** (4DO **167**,
Phoenix **233**). The separate 594-image loadability sweep covers **593 render**, **1
known boot failure** (`Crash 'n Burn` Japan), and **0 not-loadable rows**. This is a
**documented-compatibility** project, not a claim of full 3DO library compatibility;
longer full-library soak testing is planned as `TEST-09`, and true 2x/GPU CEL work
remains tracked as `VID-20`/`VID-21`.

See [Compatibility](Compatibility.md), [Game compatibility list](GameCompatibilityList.md),
and [Known issues](KnownIssues.md).

### Why does a game show a black screen?

The first frame can be black while the game loads its own OS or initializes video.
Some titles need a different BIOS or more than 3,000 frames. Try:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios-auto bios --frames 12000 `
  --frame-out last.png
```

Check the [compatibility results](GameCompatibilityResults.md) and
[Troubleshooting](Troubleshooting.md) before filing a report.

### Does 5DO support PAL games?

Yes. NTSC, PAL1 (320×288@50), and PAL2 (384×288@50) modes are implemented, with
automatic region selection, per-game overrides, and 50 Hz audio pacing. PAL support is
still being validated across the library.

### Does 5DO support 4K?

5DO renders CELs natively at 320×240, with an opt-in 640×480 presentation mode
(`--high-res`, an exact 2x nearest upscale) and an experimental true 2× CEL path
(`--true-2x`, experimental; Jurassic Park Interactive and Family Feud are the validated
references). 4K is **output upscaling**, not internal 4K CEL rendering. The
OpenGL/Vulkan presenters can output 4K with nearest, sharp-bilinear, bicubic, CRT, xBR,
FSR, HQx, and MMPX filters.

### Can I play at more than 60 fps?

The 3DO itself runs at 59.94 Hz NTSC (50 Hz PAL), and 5DO keeps that emulation rate so
gameplay, physics, and audio stay correct. A 120/144 Hz display can re-present the latest
frame more often, which lowers presentation latency but does not create new motion
frames. **Settings > Video > Frame interpolation** (off by default) additionally blends
the last two completed frames for smoother motion. It requires a fixed display rate above
60 Hz and is disabled with VRR; gameplay, input sampling, the audio clock, save states,
and frame/state/audio hashes stay at the native rate. The visible output lags the newest
field by up to one field period, so turn it off if you prefer the lowest possible latency.

### Is there netplay or achievements?

Achievements are implemented, including per-frame evaluation, hardcore rules, a durable
offline unlock queue, leaderboards, and rich presence. Hardcore credit is awaiting
RetroAchievements client approval (`REL-14`). Netplay is in progress (`NET-01`): the
loopback probe passed, but host/join is not yet available. A full
debugger UI, BIOS-less HLE boot, custom shaders, Discord Rich Presence, and cloud saves
are out of scope for 1.x. Linux packages and an unsigned Windows installer scaffold
exist; the Windows installer is not a published release asset.

## Saves, cheats, and TAS

### Are save states compatible with 4DO?

No. 5DO uses a native `.tdos` format. Format v3 is current; v1 and v2 states remain
readable. Best-effort `.4dosav` import/export is available through
`state import-4do` and `state export-4do`, but native 4DO state compatibility is not
guaranteed. State compatibility across 5DO builds is documented in
[Save states](SaveStates.md).

### Can I rewind or fast-forward?

Yes. Rewind is on by default with a compressed 30–300 second window. Press `R` to
rewind one second or `Shift+F9` to step back one frame. Hold `Tab` or tap `Space` to
fast-forward at 2×–10×; tap `Space` again to return to normal speed. Audio is muted
while fast-forwarding.

### Where are saves, screenshots, and logs?

Under the 5DO data directory shown in **Settings > Paths** or **About**, unless portable
mode is active. Typical Windows location:

```text
%LOCALAPPDATA%\5DO\
```

See [Configuration](Configuration.md) for the full layout.

### Can I record a speedrun or TAS?

Yes. 5DO records deterministic `.5domovie` files with per-frame PBUS packets, an
optional starting state, replay/verify/branch commands, and a desktop movie editor.
See [TAS movies](Tas.md). For video capture, the current release supports screenshots,
GIF recording, WAV audio capture, and H.264/AAC MP4 recording through an external FFmpeg
(`FIVEDO_FFMPEG`, `PATH`, or the built-in **Configuration > Install FFmpeg** download).
MKV output is not exposed yet.

### Does 5DO support cheats?

Yes. Per-game raw/typed/conditional/button codes are supported in the desktop manager
and the `cheat` CLI. See [Cheats](Cheats.md).

## Platforms and performance

### Does 5DO run on Linux and macOS?

Yes. Self-contained bundles are published for **Windows x64/arm64**, **Linux
x64/arm64**, and **macOS arm64**; the .NET 11 runtime is embedded. Windows x64 and
Linux x64 are the validated desktop targets, and macOS arm64 is validated through
Vulkan/MoltenVK. Windows arm64 and Linux arm64 pass native CLI/frontend runtime smoke;
their GPU/audio hardware evidence remains limited by the available hosted runners.
Please report platform-specific findings with a diagnostics bundle.

### What are the performance targets?

The core targets 60 fps at 4K output with presentation-only scaling. On the reference
machine, Road Rash's 3D race measures 4.20 ms/frame total (p99 7.40 ms), and 4K
sharp-bilinear measures GPU p99 0.089 ms / present p99 0.208 ms on an RTX 4070. See
[Performance](Performance.md).

### Do I need a discrete GPU?

No. OpenGL 3.3 or GLES 3.0/3.1 is used when available, with a CPU fallback. CRT, xBR,
FSR, HQx, MMPX, and bicubic filters are opt-in; xBR, FSR, HQx, and MMPX are GPU passes,
but larger intermediate targets and weak integrated GPUs can still cost more.

### Does 5DO collect telemetry?

No. The app does not send telemetry. Diagnostics bundles are created locally and
scrub user paths and account names before you choose to attach one to a report. See
[Privacy](Privacy.md).

## Contributing and support

### How do I report a bug?

Use the bug report issue form. Include:

- 5DO version, OS, architecture, renderer, and audio backend;
- the exact command line or UI steps;
- BIOS MD5 and disc format (never upload the files);
- expected and actual behavior;
- a privacy-scrubbed diagnostics bundle and, if possible, a minimal input script or
  movie.

### How do I contribute?

Read [Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md),
the [Developer guide](DeveloperGuide.md), and [Build and test](BuildAndTest.md). Start
small: documentation fixes, test cases, and reproducible compatibility reports are all
valuable.

### Where can I ask questions?

Use [GitHub Discussions](https://github.com/purelogiccode/5DO/discussions) for questions
and ideas. Use issues for reproducible bugs and concrete feature requests. Report
security problems privately through the [security policy](https://github.com/purelogiccode/5DO/blob/master/SECURITY.md).

### Why was my issue closed?

Compatibility reports without a version, BIOS/disc details, or reproduction steps are
hard to act on. A maintainer may close an issue that duplicates a documented known
issue or that cannot be reproduced. The templates and
[Troubleshooting](Troubleshooting.md) page list the required evidence.
