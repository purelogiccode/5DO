# 5DO Wiki

**5DO** is a modern, source-available **C#/.NET 10** emulator for the **3DO
Interactive Multiplayer**. It combines a deterministic managed emulation core with an
Avalonia desktop app, a headless CLI, and a reproducible test harness.

> **v1.0.0 is released.** Download the Windows, Linux, or macOS bundle from
> [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0).
> No BIOS ROMs or game images are included. Supply legally obtained files.

## Download

There is **one bundle per system**, and each bundle contains both the desktop app and
the CLI:

| System | Bundle |
|---|---|
| Windows x64 | `5do-win-x64.zip` |
| Linux x64 | `5do-linux-x64.tar.gz` |
| macOS x64 | `5do-osx-x64.tar.gz` |

The apps are single-file executables. A private .NET 10 runtime is included once under
`runtime/` and shared by both apps, so no separate .NET install is required.

## Start here

- [[Installation]] - requirements, first run, data folders.
- [[Quick-Start]] - the shortest path from download to gameplay.
- [[FAQ]] - BIOS, discs, releases, saves, platforms, and licensing.
- [[Troubleshooting]] - common failures and fixes.
- [[Compatibility]] - the 594-disc sweep and known limitations.
- [[Building-from-Source]] - build, test, and publish commands.
- [[Contributing]] - issues, pull requests, and evidence.
- [[Legal]] - license, donationware, and trademark rules.

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

- **v1.0.0** documented-compatibility release, published on GitHub.
- 594 disc images swept; after triage, 593 loadable/rendered, 1 known no-video case,
  0 not-loadable rows.
- 1,473 tests in the current test assembly.
- Windows x64 validated; Linux x64 full suite and CLI/frontend smoke passed (GPU
  presentation preview/best-effort); macOS x64 bundle is built and runtime validation
  is post-1.0.

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
