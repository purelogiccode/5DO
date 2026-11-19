# 5DO 1.13.2

**Released:** 2026-11-19

5DO 1.13.2 is a bug-fix release: movie verification matches the recorded evidence again,
and a partial extraction explains itself instead of crashing inside Skia. It also carries
the CHDSharp 1.7.1 dependency update.

Download from [GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.13.2).

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.13.2-win-x64.zip` |
| Windows on ARM | `5do-1.13.2-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.13.2-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint | `5do_1.13.2_amd64.deb` (arm64: `5do_1.13.2_arm64.deb`) |
| Fedora, RHEL, openSUSE | `5do-1.13.2-1.linux.x86_64.rpm` (arm64: `...aarch64.rpm`) |
| Any Linux, no install | `5do-1.13.2-linux-x86_64.AppImage` (arm64: `...aarch64.AppImage`) |
| Any Linux, sandboxed | `5do-1.13.2-linux-x86_64.flatpak` (arm64: `...aarch64.flatpak`) |
| Any Linux, portable | `5do-1.13.2-linux-x64.tar.gz` (arm64: `5do-1.13.2-linux-arm64.tar.gz`) |

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS-*.txt` list hashes; `5do-archive-keyring.gpg` is the key they use.

## Fixed

- **Movie verification stops one frame early.** A frame boundary without a V1 input
  sample made a 600-frame recording store 599 packets, so verification reported
  `frames 598 != 599`. The movie header now records the emulated frame span, playback
  runs it, and verification compares it. The recorded disc and BIOS are resolved
  automatically (or reported with an actionable message), and `movie record --disc`
  works like `movie play --disc`. A 600-frame disc movie and a 300-frame `--state-in`
  branch verify with exit 0.
- **A partial extraction crashes inside Skia.** Running the apphost without
  `libSkiaSharp`/`libHarfBuzzSharp` now shows a **5DO cannot start** dialog naming the
  missing files and explaining how to extract the archive, instead of a
  `SkiaSharp.SKImageInfo` crash or a bug report (issues #69923/#69924).

## Changed

- **CHDSharp 1.7.1** replaces 1.7.0; the lock files were refreshed and
  `dotnet restore --locked-mode` passes.

## Verification

- 2,449/2,449 tests, `dotnet format`, `jb inspectcode`, and `mkdocs --strict`.
- The published Linux arm64 and Windows arm64 1.13.1 assets were validated on real ARM64
  hardware; see [Compatibility](https://purelogiccode.github.io/5DO/Compatibility.html)
  §10.5–§10.6.
- CI builds the five bundles and the Linux packages, installs the `.deb`/`.rpm`, runs the
  sandboxed Flatpak `selftest`, and verifies the signed APT/RPM mirror trees.

## Known limits

- macOS Retina 2x presentation is still unvalidated (no HiDPI host).
- The bundled OpenAL Soft runtime needs the Microsoft Visual C++ 2015–2022 ARM64
  redistributable on a clean Windows image; `REL-11` ships or detects it next.
- No hosted package repository or Flathub listing yet.
- Intel macOS is unsupported.
