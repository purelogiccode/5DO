# Security Policy

## Supported versions

| Version | Supported          |
|---------|--------------------|
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

Only the latest 1.0.x release receives security fixes. The desktop frontend and CLI
ship for Windows, Linux, and macOS (x64/arm64).

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Use GitHub's private vulnerability reporting:

<https://github.com/purelogiccode/3DOEmulator/security/advisories/new>

If that form is unavailable, contact the maintainer through the repository's
[security page](https://github.com/purelogiccode/3DOEmulator/security) and a private
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

- **Dependabot** alerts and automated security updates are enabled.
- **Dependabot version updates** cover NuGet packages and GitHub Actions weekly
  (`.github/dependabot.yml`).
- **NuGet audit** checks direct and transitive packages during restore/build
  (`NuGetAuditMode=all` in `Directory.Build.props`); a known vulnerable package fails
  the build.
- **Least-privilege CI**: the workflow token is restricted to `contents: read`, and
  checkouts do not persist credentials.
- **Pinned actions**: GitHub Actions are pinned to full commit SHAs.
- **No secrets or copyrighted disc data** are committed to the repository.
- **Builds and packaging** run in GitHub Actions from the repository source.

## Repository settings note

This repository is currently private. GitHub code scanning, secret scanning,
dependency review, and branch protection require GitHub Advanced Security, a paid
plan, or a public repository. Enable them when the repository is made public or
upgraded:

- CodeQL code scanning (default setup or a `.github/workflows/codeql.yml` workflow);
- secret scanning with push protection;
- dependency review on pull requests;
- a branch protection rule or ruleset for `master` requiring CI and review.
