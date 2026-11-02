# Security Policy

## Supported versions

| Version | Supported          |
|---------|--------------------|
| `1.0.0-rc.1` pre-release | :white_check_mark: |
| Older pre-release builds | :x: |

There is no stable 1.0 release yet. Security fixes target the current pre-release
source tree and the latest published pre-release artifacts. The desktop frontend and
CLI publish for Windows, Linux, and macOS (x64/arm64); Windows x64 is the validated
platform.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Use GitHub's private vulnerability reporting:

<https://github.com/purelogiccode/5DO/security/advisories/new>

If that form is unavailable, contact the maintainer through the repository's
[security page](https://github.com/purelogiccode/5DO/security) and a private
channel will be arranged.

Please include:

- the affected version and OS/architecture;
- a minimal reproducer (synthetic disc image, BIOS, command line, or input sequence);
- the observed impact and any known mitigations;
- whether you are willing to be credited in the advisory.

Do not attach copyrighted BIOS images or game discs. Describe how to reproduce the
issue with a synthetic fixture where possible.

## What to expect

- **Acknowledgement** within 3 business days.
- **Triage** within 10 business days, including a severity assessment (CVSS).
- **Fix and advisory** coordinated with you; credit is given unless you prefer to
  remain anonymous.
- **Disclosure** after a patched release is available, or after 90 days, whichever
  comes first, unless an extension is agreed.

## Scope

In scope:

- the emulator core (ARM60 CPU, Clio, XBUS, DSP, MADAM/CEL, SPORT, VDLP, memory);
- storage readers and parsers (ISO, BIN, CUE, CHD, Opera file system);
- the desktop frontend, CLI, input handling, save states, and configuration files;
- supply-chain issues in the build, test, packaging, and release workflows.

Out of scope:

- BIOS ROMs and game images (they are not distributed by this repository);
- vulnerabilities in third-party dependencies (report them upstream; we track them
  with Dependabot);
- issues that require local write access to the application or its data directory;
- missing hardening without a demonstrated impact;
- social engineering and spam.

## Security measures in this repository

The repository is public. The following protections are enabled:

- **Private vulnerability reporting** through GitHub Security Advisories.
- **Dependabot alerts** and automated security updates.
- **Dependabot version updates** for NuGet, GitHub Actions, and documentation Python
  packages (`.github/dependabot.yml`).
- **NuGet audit** checks direct and transitive packages during restore/build
  (`NuGetAuditMode=all` in `Directory.Build.props`); a known vulnerable package fails
  the build.
- **CodeQL code scanning** for C#, GitHub Actions, and Python
  (`.github/workflows/codeql.yml`).
- **Secret scanning** with push protection.
- **Dependency review** on pull requests (`.github/workflows/dependency-review.yml`).
- **OpenSSF Scorecard** analysis with published results and SARIF upload
  (`.github/workflows/scorecard.yml`).
- **Least-privilege CI**: workflow tokens are restricted to the permissions each job
  needs, and checkouts do not persist credentials.
- **Pinned actions**: GitHub Actions are pinned to full commit SHAs.
- **No secrets or copyrighted disc data** are committed to the repository.
- **Builds and packaging** run in GitHub Actions from the repository source.

Repository administrators should keep branch protection or a ruleset on `master`
requiring the CI, docs, CodeQL, and dependency-review checks before merge.

## Supported platforms

Windows x64 is the validated platform. Linux and macOS builds are published but are
not yet fully runtime-validated. A security report that only affects an unvalidated
platform is still welcome; include the exact platform, runtime, and renderer.
