# 5DO 1.17.0

**Released:** 2026-11-22

5DO 1.17.0 is the autosave and archive-container release. It adds a rolling autosave
with validated startup resume and direct ZIP/7z handling for ISO, BIN, and CUE+BIN
images. The CD-DA diagnostics, physical CD-DA, and disc read-error work from 1.16.x is
unchanged.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.17.0-win-x64.zip` |
| Windows on ARM | `5do-1.17.0-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.17.0-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.17.0_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.17.0_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.17.0-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.17.0-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.17.0-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.17.0-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.17.0-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.17.0-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.17.0-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Rolling autosave and startup resume (UX-37)

The desktop keeps one autosave in its own directory:

```text
<save directory>/
  autosave/
    slot-00.tdos
    slot-00.json
    slot-00.png
```

- Written on pause, clean exit, and a configurable interval. The default interval is
  five minutes; Settings clamps it to 1-60 minutes.
- On startup the last disc reopens and the autosave restores only when the state header's
  disc game ID and effective BIOS MD5 match the current machine. A renamed, re-burned, or
  different disc is refused with an actionable message and the machine is not touched.
- Numbered manual slots are never overwritten. **Emulation > Resume Autosave** loads the
  file on demand and **Delete Autosave** removes only the `autosave/` files.
- `AutoSaveEnabled` and `AutoResumeEnabled` are separate settings. RetroAchievements
  hardcore mode suppresses both writing and restoring.
- The interval timer uses the shell's one-second tick and does not add work to the
  emulation thread.

Desktop acceptance on Windows x64 (Theme Park, FZ-1 BIOS):

- Clean exit wrote frame 792.
- Relaunch restored frame 792 and started emulation; the next clean exit wrote frame
  1532, so play continued from the restored state.
- With both options disabled, the autosave file was unchanged and no restore/write
  appeared in the log.
- A modified disc (`48b76311` vs `33e5afe4`) and a different BIOS (`51f2f43a` vs
  `f47264dd`) were both refused with the documented messages.
- A 60-second interval wrote frame 3655 while emulation was running.

## ZIP/7z disc containers (SYS-27)

A `.zip` or `.7z` path is accepted anywhere a disc path is accepted:

- The archive must contain one ISO/BIN image or one CUE+BIN set. CUE `FILE` entries are
  resolved relative to the CUE entry, including subfolders.
- Entries are decompressed in memory through SharpCompress with a 1.5 GB per-entry limit.
  Larger images should be extracted.
- CHD, CDI, and NRG entries are rejected with extraction guidance because those
  containers already have their own compression/random-access layout.
- Encrypted and multi-volume archives are refused with an actionable message and are
  never partially read. PREGAP, POSTGAP, INDEX 00, and WAVE CUEs are also refused with
  extraction guidance.
- The game library, drag/drop, recent discs, playlist editor, file pickers, and the CLI
  (`run`, `info`, `disc ...`, `fs ...`) accept the archive path.

Parity evidence: Theme Park from the extracted ISO, a ZIP of that ISO, and a 7z CUE+BIN
set produced the same 600-frame hash `0x110E7DC5`, PC `0x0000596C`, and identical PNG
SHA-256.

## Retained from 1.16.x

- `disc audio` extracts Red Book tracks to WAV from CUE/CHD images and host optical
  drives.
- `disc verify` prints progress and uses the native track total for mixed-mode discs.
- Physical CD-DA works through the drive TOC and raw CDDA reads. The issue #8 reporter's
  Red Book CD listed six audio tracks and extracted track 1 as a 164.28 s WAV with peak
  31659 and RMS 7248.66 (0 clipped).
- Damaged or failing disc reads are reported to the guest instead of stopping emulation.
- The FZ-10 audio-only BIOS player transport remains **STOP** (SYS-01, matching 4DO); use
  `disc audio` for the direct CD-DA test.

## Verification

- 2,595/2,595 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, `jb inspectcode` at warning severity, and
  `mkdocs build --strict`.
- CI covers Windows, Linux, and macOS, the NativeAOT apphosts, trimmed publishes, Linux
  packages, and all five bundles.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.17.0>; release run
  <https://github.com/purelogiccode/5DO/actions/runs/PENDING> passed the five bundle
  jobs, both AppImage/Flatpak package jobs, signing, and the publish job. The five
  downloaded bundles match `SHA256SUMS.txt`.
- Windows x64 bundle: 1.17.0, `selftest` 15/15, `FRONTEND SMOKE OK 1.17.0`, and a ZIP/7z
  disc opens and verifies.
- Linux x64 AppImage: FUSE reports 1.17.0, writes `FRONTEND SMOKE OK 1.17.0`, the
  detached `.asc` verifies, and the embedded `.upd_info`/`.zsync` target the versioned
  image.

## Known issues

- A ZIP/7z holds one image or CUE+BIN set. Entries are decompressed in memory with a
  1.5 GB per-entry limit; CHD/CDI/NRG, encrypted, and multi-volume archives must be
  extracted or decrypted first.
- The FZ-10 audio-only BIOS player still lists the TOC but leaves the transport at
  **STOP** (SYS-01, matching 4DO). Use `disc audio` to verify the drive's CD-DA path.
- `--opera-soft-reset` remains opt-in/experimental; `--opera-dipir` still blocks the
  reporter's boot and needs a CSTAT/DIPIR transcript.
