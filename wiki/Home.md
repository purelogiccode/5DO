# 5DO Wiki

**5DO** is a modern, source-available **C#/.NET 10** emulator for the **3DO
Interactive Multiplayer**. It combines a deterministic managed emulation core with an
Avalonia desktop app, a headless CLI, and a reproducible test harness.

> **v1.2.2 is released.** Download the Windows, Linux, or macOS bundle from
> [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.2.2).
> No BIOS ROMs or game images are included. Supply legally obtained files.

## Download

There is **one bundle per system and architecture**, and each bundle contains both the
desktop app and the CLI:

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.2.2-win-x64.zip` |
| Windows arm64 | `5do-1.2.2-win-arm64.zip` |
| Linux x64 | `5do-1.2.2-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.2.2-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.2.2-osx-x64.tar.gz` |
| macOS arm64 | `5do-1.2.2-osx-arm64.tar.gz` |

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
- [[Release-1.2.2]] - what changed in the current release.

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

- **v1.2.2** documented-compatibility release, published on GitHub.
- 653 games across 1,366 library images; three audio-only CHDs catalogued.
- 594 disc images swept; after triage, 593 loadable/rendered, 1 known no-video case,
  0 not-loadable rows.
- 1,808 tests in the current test assembly.
- Windows x64 validated; Linux x64 full suite and CLI/frontend smoke passed (GPU
  presentation preview/best-effort); macOS and arm64 bundles are cross-published and
  architecture-validated, but runtime validation is post-1.0.

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
