# 5DO Wiki

**5DO** is a modern, source-available **C#/.NET 10** emulator for the **3DO
Interactive Multiplayer**. It combines a deterministic managed emulation core with an
Avalonia desktop app, a headless CLI, and a reproducible test harness.

> **v1.7.1 is released.** Download the Windows, Linux, or macOS bundle from
> [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.7.1).
> No BIOS ROMs or game images are included. Supply legally obtained files.

## Download

There is **one bundle per system and architecture**, and each bundle contains both the
desktop app and the CLI:

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.7.1-win-x64.zip` |
| Windows arm64 | `5do-1.7.1-win-arm64.zip` |
| Linux x64 | `5do-1.7.1-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.7.1-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.7.1-osx-x64.dmg` |
| macOS arm64 | `5do-1.7.1-osx-arm64.dmg` |

The apps are framework-dependent single-file executables. Install the **.NET 10
Runtime** first from <https://dotnet.microsoft.com/download/dotnet/10.0>; the bundles do
**not** include the runtime.

## Start here

- [[Installation]] - requirements, first run, data folders.
- [[Quick-Start]] - the shortest path from download to gameplay.
- [[FAQ]] - BIOS, discs, releases, saves, platforms, and licensing.
- [[Troubleshooting]] - common failures and fixes.
- [[Compatibility]] - the 594-disc sweep, library counts, and known limitations.
- [[Building-from-Source]] - build, test, and publish commands.
- [[Contributing]] - issues, pull requests, and evidence.
- [[Legal]] - license, donationware, and trademark rules.
- [[Release-1.7.1]] - what changed in the current release.

## Full documentation

The complete documentation site is
**<https://purelogiccode.github.io/5DO/>**.

Useful direct links:

- [User guide](https://purelogiccode.github.io/5DO/UserGuide.html)
- [CLI reference](https://purelogiccode.github.io/5DO/CliReference.html)
- [Configuration](https://purelogiccode.github.io/5DO/Configuration.html)
- [Compatibility notes](https://purelogiccode.github.io/5DO/Compatibility.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)
- [What's new](https://purelogiccode.github.io/5DO/WhatsNew.html)
- [Developer guide](https://purelogiccode.github.io/5DO/DeveloperGuide.html)

## Project status

- **v1.7.1** documented-compatibility release, published on GitHub; macOS ships as a DMG.
- 414 unique titles across 1,371 library images; the 378-title game test set has 375
  playable titles under the binary beyond-title rule, with 0 `Behind`/`Regression` rows.
- The earlier 594-disc sweep has 593 loadable/rendered, 1 known no-video case, and 0
  not-loadable rows.
- 2,125 tests in the current test assembly.
- Windows x64 validated; Linux x64 full suite and CLI/frontend smoke passed (GPU
  presentation preview/best-effort); macOS x64/arm64, Linux arm64, and Windows arm64
  pass the native CLI/frontend runtime smoke.

See [Status](https://purelogiccode.github.io/5DO/Status.html),
[What's new](https://purelogiccode.github.io/5DO/WhatsNew.html), and
[Roadmap](https://purelogiccode.github.io/5DO/Roadmap.html).

## Community

- [GitHub Discussions](https://github.com/purelogiccode/5DO/discussions)
- [Issues](https://github.com/purelogiccode/5DO/issues)
- [Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md)
- [Security policy](https://github.com/purelogiccode/5DO/blob/master/SECURITY.md)

5DO is donationware: non-commercial, source-available software. Donations do not grant
commercial rights. "3DO" and "Panasonic" are trademarks of their respective owners.
