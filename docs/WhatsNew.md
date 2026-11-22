# What's new in 5DO 1.17.0

**Released:** 2026-11-22

5DO 1.17.0 adds a rolling autosave with validated startup resume and direct ZIP/7z
containers for ISO, BIN, and CUE+BIN images. It retains the 1.16.1 CD-DA diagnostics and
the 1.16.0 physical CD-DA/read-error work.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.17.0-win-x64.zip` |
| Windows on ARM | `5do-1.17.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.17.0-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.17.0_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.17.0-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.17.0-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.17.0-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.17.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Rolling autosave and startup resume

The desktop keeps one autosave under `<save directory>/autosave/`, separate from the
numbered manual slots:

- It is written on **pause**, **clean exit**, and a configurable interval (1-60 minutes,
  default 5). Manual slots are never overwritten.
- On startup the last disc reopens and the autosave is restored only when its disc game
  ID and effective BIOS MD5 match. A renamed, re-burned, or different disc is refused
  with an actionable message instead of loading the wrong save.
- **Settings > Emulation > Autosave** controls writing, the interval, and startup
  resume. **Emulation > Resume Autosave / Delete Autosave** expose the file on demand.
- RetroAchievements hardcore mode suppresses both autosave writes and restores.

Windows desktop acceptance: clean exit wrote frame 792, relaunch restored frame 792 and
continued to 1532, disabled mode left the file unchanged, a modified disc and a different
BIOS were refused, and a 60-second interval wrote frame 3655.

## ZIP/7z disc containers

A `.zip` or `.7z` path can be opened anywhere a disc path is accepted:

- One ISO/BIN or one CUE+BIN set per archive. CUE `FILE` entries resolve relative to the
  CUE, including subfolders.
- Entries are decompressed in memory (1.5 GB per-entry limit) rather than to a temporary
  directory. Larger images should be extracted.
- CHD, CDI, and NRG must be extracted; those containers have their own compression and
  random-access layouts.
- Encrypted and multi-volume archives are refused with guidance and are never partially
  read. PREGAP, POSTGAP, INDEX 00, and WAVE CUEs are rejected with extraction guidance.
- The game library, drag/drop, recent discs, playlist editor, file pickers, and every CLI
  disc command accept the archive path.

Theme Park parity: the extracted ISO, a ZIP of that ISO, and a 7z CUE+BIN set produced
the same 600-frame hash `0x110E7DC5`, PC `0x0000596C`, and identical PNG SHA-256.

## Still included from 1.16.1 and 1.16.0

- `disc audio` extracts Red Book tracks to WAV from an image or a host optical drive.
- `disc verify` prints progress and uses the native track total for mixed-mode discs.
- Physical CD-DA works through the drive TOC and raw CDDA reads; the reporter's Red Book
  CD produced a non-silent 164.28 s track.
- Damaged or failing disc reads are reported to the guest instead of stopping emulation.
- The FZ-10 audio-only BIOS player transport remains **STOP** (SYS-01, matching 4DO); use
  `disc audio` for the direct CD-DA test.

## Verification

- 2,595/2,595 tests, build 0 warnings/0 errors, `dotnet format`, `jb inspectcode`, and
  `mkdocs build --strict` clean.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.17.0>; release run
  <https://github.com/purelogiccode/5DO/actions/runs/PENDING>. The five downloaded
  bundles match `SHA256SUMS.txt`.
- Windows x64 bundle reports 1.17.0, `selftest` 15/15, and
  `FRONTEND SMOKE OK 1.17.0`; a ZIP/7z disc opens and verifies.
- Linux x64 AppImage/tarball smoke, detached `.asc`, and versioned `.zsync`/`.upd_info`.

## Known issues

- A ZIP/7z holds one image or CUE+BIN set. Entries are decompressed in memory with a
  1.5 GB per-entry limit, and CHD/CDI/NRG, encrypted, and multi-volume archives must be
  extracted or decrypted first.
- The FZ-10 audio-only BIOS player transport remains at STOP (SYS-01). Use `disc audio`
  to verify the drive's CD-DA path.
- `--opera-soft-reset` remains opt-in/experimental; `--opera-dipir` still blocks the
  reporter's boot and needs a CSTAT/DIPIR transcript.
