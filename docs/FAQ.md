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
Read [Licensing](Licensing.md) and the
[project license](https://github.com/purelogiccode/5DO/blob/master/LICENSE).

### Why is it called 5DO?

The project name is a play on 3DO. C# identifiers cannot start with a digit, so the
solution and namespace prefix use **FiveDo**.

## Getting started

### Does 5DO include a BIOS or games?

No. You must supply a legally obtained 3DO BIOS and legally obtained disc images. The
repository never includes BIOS ROMs, game images, or extracted game assets.

### Where do I get a BIOS?

Dump it from a 3DO console you own, or obtain it through a lawful source in your
jurisdiction. Do not ask for or share copyrighted BIOS files in issues, discussions, or
pull requests.

### Which disc formats are supported?

- ISO (2048-byte sectors)
- BIN (2352-byte sectors)
- CUE+BIN, including multi-track and CD-DA layouts
- CHD, including parent/child images
- M3U/M3U8 playlists for multi-disc games

### How do I open a game from a launcher?

```text
FiveDo.Frontend.Avalonia.exe "{disc}" --bios "{bios}" --fullscreen
```

A positional disc path or `--disc <path>` opens and auto-starts the game. See
[Installation](Installation.md) and the [User guide](UserGuide.md).

### Is there a download?

Yes - **v1.9.0** is available on
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.9.0). There is one
bundle per system and architecture (Windows x64/arm64, Linux x64/arm64, macOS
x64/arm64). Windows/Linux bundles are ZIP/tar.gz; macOS is a DMG. Each bundle contains
both the desktop app and the CLI. Install the **.NET 11 Runtime** first; the bundles do
not include it. You can also build from source.

## Compatibility

### How compatible is 5DO?

The compatibility matrix covers **378 game-test-set titles**: **375 are playable under
the binary beyond-title rule**, and the three exceptions are non-retail/add-on images
with documented root causes. Against the peer lists, 5DO has **0 `Behind` and 0
`Regression` rows** and is a strict superset of the **252-title peer union** (4DO **167**,
Phoenix **233**). The separate 594-image loadability sweep covers **593 render**, **1
known boot failure** (`Crash 'n Burn` Japan), and **0 not-loadable rows**. This is a
**documented-compatibility** project, not a claim of full 3DO library compatibility;
full visual review and high-resolution CEL research remain post-1.6.

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

5DO renders CELs natively at 320×240 (and an opt-in 640×480 high-resolution mode). 4K
is **output upscaling**, not internal 4K CEL rendering. The OpenGL/Vulkan presenters
can output 4K with nearest, sharp-bilinear, bicubic, CRT, xBR, FSR, HQx, and MMPX filters.

### Is there netplay or achievements?

No. Netplay, achievements, a full debugger UI, installers, and a guest CPU JIT are
post-1.0 or explicitly out of scope for v1.

## Saves, cheats, and TAS

### Are save states compatible with 4DO?

No. 5DO uses a native `.tdos` format. Format v3 is current; v1 and v2 states remain
readable. State compatibility across 5DO builds is documented in
[Save states](SaveStates.md).

### Can I rewind or fast-forward?

Yes. Rewind is on by default with a compressed 30–300 second window. Press `R` to
rewind one second or `Shift+F9` to step back one frame. Hold `Tab` to fast-forward at
2×–10×; audio is muted while fast-forwarding.

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
See [TAS movies](Tas.md).

### Does 5DO support cheats?

Yes. Per-game raw/typed/conditional/button codes are supported in the desktop manager
and the `cheat` CLI. See [Cheats](Cheats.md).

## Platforms and performance

### Does 5DO run on Linux and macOS?

Builds are published for Windows, Linux, and macOS on x64 as framework-dependent
bundles; users install the .NET 11 runtime. **Windows x64 is the validated target.**
Linux and macOS runtime validation is ongoing; please report issues with a diagnostics
bundle.

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
