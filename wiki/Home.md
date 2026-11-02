# 5DO Wiki

**5DO** is a modern, source-available **C#/.NET 10** emulator for the **3DO
Interactive Multiplayer**. It combines a deterministic managed emulation core with an
Avalonia desktop app, a headless CLI, and a reproducible test harness.

> **Pre-1.0:** Windows x64 is the validated platform. No BIOS ROMs or game images are
> included. Supply legally obtained files.

## Start here

- [[Installation]] — requirements, first run, data folders.
- [[Quick-Start]] — the shortest path from clone to gameplay.
- [[FAQ]] — BIOS, discs, releases, saves, platforms, and licensing.
- [[Troubleshooting]] — common failures and fixes.
- [[Compatibility]] — the 594-disc sweep and known limitations.
- [[Building-from-Source]] — build, test, and publish commands.
- [[Contributing]] — issues, pull requests, and evidence.
- [[Legal]] — license, donationware, and trademark rules.

## Full documentation

The complete documentation site is
**<https://purelogiccode.github.io/5DO/>**.

Useful direct links:

- [User guide](https://purelogiccode.github.io/5DO/UserGuide.html)
- [CLI reference](https://purelogiccode.github.io/5DO/CliReference.html)
- [Configuration](https://purelogiccode.github.io/5DO/Configuration.html)
- [Compatibility notes](https://purelogiccode.github.io/5DO/Compatibility.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)
- [Developer guide](https://purelogiccode.github.io/5DO/DeveloperGuide.html)

## Project status

- `1.0.0-rc.1` source tree; the v1.0 release is deliberately deferred.
- 594 disc images swept; after triage, 593 loadable/rendered, 1 known no-video case,
  0 not-loadable rows.
- 1,473 tests in the current test assembly.
- Windows x64 validated; Linux and macOS publish outputs are produced but not fully
  runtime-validated.

See [Status](https://purelogiccode.github.io/5DO/Status.html) and
[Roadmap](https://purelogiccode.github.io/5DO/Roadmap.html).

## Community

- [GitHub Discussions](https://github.com/purelogiccode/5DO/discussions)
- [Issues](https://github.com/purelogiccode/5DO/issues)
- [Contributing](https://github.com/purelogiccode/5DO/blob/master/CONTRIBUTING.md)
- [Security policy](https://github.com/purelogiccode/5DO/blob/master/SECURITY.md)

5DO is donationware: non-commercial, source-available software. Donations do not grant
commercial rights. "3DO" and "Panasonic" are trademarks of their respective owners.
