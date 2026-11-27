# Project Status and Next Steps

**2026-11-27, v1.19.0 feature release:** direct-IP two-player netplay ships in the
desktop and CLI. **Netplay > Host Session** and **Join Session** transfer a compressed
machine state over TCP, then exchange per-frame PBUS input, frame hashes, and cumulative
audio hashes over UDP; `5do netplay host|join` exposes the same core. Reconnect resumes
from the host's current state. A 36,000-frame two-process run with different P1/P2 scripts
matched the single-machine reference (`0x342EA767`), and desktop host and join were each
validated against a CLI peer with matching hashes. The suite is **2,677/2,677**. The tag
workflow <https://github.com/purelogiccode/5DO/actions/runs/45960321450> published 17
assets; both checksum manifests verify with 0 mismatches. The published win-x64 bundle
reports 1.19.0, `selftest` 15/15, hash `0x110E7DC5` / PC `0x00059960`, netplay hashes
`0x110E7DC5` / `0x035F6315`, and `FRONTEND SMOKE OK 1.19.0`; its 600-frame MP4 is
10.010 s H.264/AAC with DAR 4:3. See [ReleaseNotes-1.19.0.md](ReleaseNotes-1.19.0.md),
[NetplayProbe.md](NetplayProbe.md), and [UserGuide.md](UserGuide.md#netplay-direct-ip).

**2026-11-26, v1.18.1 patch release:** MP4 recording no longer requires a manual
FFmpeg setup. **Configuration > Install FFmpeg**, the MP4 prompt, and
`5do ffmpeg install` download a pinned FFmpeg build (6.1.1 Windows, 7.0.2 Linux, 6.0
macOS), verify its SHA-256, test H.264/AAC support, and install it beside 5DO when
writable (otherwise in the user data `tools/` folder). The downloader is explicit and
HTTPS-only; FFmpeg is still not bundled. The Windows arm64 build uses the x64 binary under
Windows emulation. New tests cover the pinned source table and checksum rejection. The
CLI install was validated on Windows x64, Linux x64, and macOS arm64; Windows MP4
recording used the installed binary. The 1.18.1 suite was **2,651/2,651** (see
[TestCorpus.md](TestCorpus.md) 1.1cy). The tag workflow
<https://github.com/purelogiccode/5DO/actions/runs/45916148140> published 17 assets; both
checksum manifests verify with 0 mismatches. The published win-x64 bundle reports 1.18.1,
`selftest` 15/15, hash `0x110E7DC5` / PC `0x00059960`, `FRONTEND SMOKE OK 1.18.1`, and
installed FFmpeg 6.1.1 beside the CLI; its 600-frame MP4 is 10.010 s H.264/AAC with DAR
4:3. See [ReleaseNotes-1.18.1.md](ReleaseNotes-1.18.1.md).

**2026-11-26, NET-01 direct-IP netplay (unreleased):** **Netplay > Host Session** and
**Join Session** transfer a compressed machine state over TCP, then exchange per-frame
input and frame/audio hashes over UDP. The CLI exposes `5do netplay host|join`. A
36,000-frame two-process run with different P1/P2 scripts matched the single-machine
reference (`0x342EA767`, audio `0xB6AB50D0`) in about 154 s; reconnect resumed at frame
304 and finished with matching host/join hashes and audio. The probe measured about
**16.8 KB/s**, 240.7 fps, and a 650 KB state (38 ms save / 24 ms load); 50 ms one-way
latency and every-third-input-packet loss passed. The suite is **2,677/2,677**. Desktop
host and join were each validated against a CLI peer with matching frame/audio hashes; an
optional session token and a two-window physical-controller smoke remain post-release
hardening. The active plan tracks **12 open items**. Lobby, NAT traversal, authentication,
encryption, more than two players, and pointer devices are out of scope.
See [NetplayProbe.md](NetplayProbe.md), the [User Guide](UserGuide.md#netplay-direct-ip),
and [TestCorpus.md](TestCorpus.md) 1.1da.

**2026-11-26, v1.18.0 feature release (published):** the release adds read-only
Alcohol MDS/MDX and CloneCD CCD/IMG/SUB images, optional H.264/AAC MP4 recording through
an external FFmpeg with Low/Medium/High quality, high-refresh frame interpolation,
patterned GPU void bars on OpenGL/Direct3D 11/Vulkan, and live CPU-clock changes that
apply at the next frame boundary. Windows desktop/CLI, Linux x64 CLI, and macOS arm64 CLI
validated the MP4 path; the full suite is **2,644/2,644**. The active plan tracks
**13 open items**. The tag workflow
<https://github.com/purelogiccode/5DO/actions/runs/45855166556> published 17 assets; both
checksum manifests verify with 0 mismatches. The published win-x64 bundle reports 1.18.0,
`selftest` 15/15, the 600-frame hash `0x110E7DC5` / PC `0x00059960`, a 10.010 s MP4 with
DAR 4:3, and `FRONTEND SMOKE OK 1.18.0`. See
[ReleaseNotes-1.18.0.md](ReleaseNotes-1.18.0.md).

**2026-11-26, MP4 video recording:** `UX-38` is complete. The desktop **File > Record
MP4...** menu and CLI `--record-mp4` use an external FFmpeg (`FIVEDO_FFMPEG`, apphost
folder, or `PATH`), record the presented frame and DSP/CD audio mix, and mux H.264/AAC
with Low/Medium/High quality (`--record-quality` / Settings > Video > MP4 quality).
Windows desktop (17.601 s) and CLI, Linux CLI, and macOS CLI (60.06 s) produced H.264/AAC
files at 60000/1001 with non-silent audio; Linux Low/Medium/High sizes were 1.72/2.97/5.19 MB.
FFmpeg is not bundled. The active plan now tracks **13 open items**; the suite is
**2,641/2,641**; build, `dotnet format`, and `mkdocs build --strict` are clean.

**2026-11-26, patterned GPU bars and live CPU clock:** `UX-33` and `SYS-25` are complete.
The selected void pattern (Solid, Checker, Grid, Scanline, FourDO, Bumps, or Metal) now
renders in the GPU letterbox/pillarbox bars on OpenGL, Direct3D 11, and Vulkan, and the
CPU-clock setting applies at the next frame boundary without recreating the machine.
Checker and FourDO were verified on OpenGL, Checker on Direct3D 11 and Vulkan, and six
`LiveClockTests` prove the live switch matches a clock override of the same pre-switch
state, a post-switch state round-trip is deterministic, stock runs are unchanged, and all
component clocks follow the machine. At that point the active plan tracked **14 open
items**; the suite was **2,638/2,638**, with build, `dotnet format`, and `mkdocs build
--strict` clean.

**2026-11-26, high-refresh frame interpolation:** `VID-22` is complete. **Settings > Video
> Frame interpolation** blends the two most recent completed fields at the presentation
rate; it requires a fixed rate above 60 Hz and is disabled with VRR. Road Rash on the
RTX 4070 / 164 Hz host held 59.4-60.4 fps with 148-167 Hz presentation on OpenGL, 132 Hz
on Vulkan, and 83 Hz on Direct3D 11, `audio ... u0 d0`, and ~3 ms input-to-submit latency;
the toggle-off run presented at 167 Hz without interpolation. The emulated machine, audio
clock, frame/state/audio hashes, and save states are unchanged; blended frames use the
15-bit direct-color path and the visible output lags the newest field by up to one field.
`PLAT-19` native Wayland Vulkan is parked in plan §4 until Avalonia officially supports
Wayland, so the active plan now tracks **16 open items**. The suite is **2,627/2,627**;
build, `dotnet format`, and `mkdocs build --strict` are clean; scoped InspectCode on the
changed Video, Frontend, and Host projects is clean (the full-solution run still hits the
environment's JetBrains `FileSystemWatcher` cleanup crash).

**2026-11-26, MDS/CCD readers and post-1.17.1 plan review:** the license review is
complete. 5DO **retains the FreeDO modified GNU LGPL** as non-commercial donationware;
the 4DO game database is resolved as a separate **GPL-2.0** data component with its
complete text in `docs/GPL-2.0.txt`. Official Debian/Ubuntu/Fedora/openSUSE archives and
Flathub remain **not eligible**. The post-decision documentation sweep is complete, so
`REL-13` and `REL-12` are closed and the active plan tracks `REL-14` RetroAchievements
hardcore approval (request submitted; awaiting external review), `TEST-09` a full-library
soak (all 1,371 images at 10,000 frames; all 414 unique titles at 30,000 frames), `UX-38`
video recording, and `SYS-29` representative real 3DO MDS/CCD validation. `SYS-29`'s
read-only MDS/MDX and CloneCD readers are implemented with synthetic data/audio tests and a
real MDS v2 compressed-fixture smoke; only representative real 3DO MDS/CCD boot-hash parity
remains. `SYS-28` is closed by the issue #8 re-burn evidence: the 1.17.0 CD-R boot logged
no warnings or read errors over more than 3,000 frames, `disc verify` read
157,996/157,996 sectors with 0 errors, and the Red Book audio CD extracted without a core
stop. The README, Roadmap, FAQ, Compatibility, KnownIssues, ReleaseLegalReview,
THIRD-PARTY-NOTICES, Licensing, GameDatabase, LinuxDistribution, Installation, Installers,
BuildAndTest, ReferenceStrategy, UserGuide, Performance, OpenALSoft, ReleaseChecklist, and
the wiki Legal page were corrected to the retained-license state.

**2026-11-25, v1.17.1 bug-fix release:** the release fixes the malformed-ZIP library
refresh (#70180), routes unobserved `OpenGL FBO` compositor failures to the CPU presenter
(#70015, #70377), and waits five seconds before reporting a slow emulation stop while
filing one report instead of two (#70299, #70300). Linux releases now publish
`SHA256SUMS.txt` and `SHA256SUMS-linux.txt` only; detached `.asc` signatures and the GPG
keyring were removed. Avalonia 12.2.0, CHDSharp 1.7.2, Meziantou.Analyzer 3.0.342,
mkdocs-material 9.8.2, and the current action pins are included. The suite is
**2,602/2,602**; build, `dotnet format`, `jb inspectcode`, and `mkdocs build --strict`
are clean. The same release bundles were exercised on Windows x64, Windows arm64,
Linux x64, Linux arm64, and macOS arm64 with Wolfenstein 3D (USA) and `panafz1.bin`:
all produced frame hash `0x110E7DC5`, PC `366944`, game ID
`3dfcdbbff14ffb59e157e27f18d9df7b`, and identical 600-frame PNG SHA-256;
`selftest` 15/15 and the frontend smoke passed on every target (the pre-release bundles
carry the 1.17.0 version string; the release workflow rebuilds the same sources as
1.17.1). Windows x64
OpenGL/Direct3D 11 probes pass on the Radeon RX 580; Windows arm64 GL/ANGLE and
Direct3D 11 probes pass on the Microsoft Basic Render Driver. Published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.17.1>; release run
<https://github.com/purelogiccode/5DO/actions/runs/45690464736> published all 17 assets
with no signature assets, and both checksum manifests verify. Evidence:
[ReleaseNotes-1.17.1.md](ReleaseNotes-1.17.1.md),
[Compatibility.md](Compatibility.md) §10.10, and [KnownIssues.md](KnownIssues.md).

**2026-11-22, v1.17.0 release:** the autosave and archive-container release ships two
new features. **UX-37** keeps a rolling autosave under `states/autosave/`, written on
pause, clean exit, and a configurable 1-60 minute interval, and restores it at startup
only when the disc game ID and effective BIOS MD5 match; manual slots are never touched,
and hardcore mode suppresses both. **SYS-27** opens one ISO/BIN or CUE+BIN set directly
from a ZIP or 7z archive through SharpCompress, with relative CUE paths and clear
refusal of CHD/CDI/NRG, encrypted, multi-volume, and unsupported CUE layouts. The
Windows desktop acceptance wrote frame 792 on clean exit, restored 792 on relaunch and
continued to 1532, left the file unchanged with both options off, refused a modified
disc and a different BIOS, and wrote frame 3655 at the 60-second interval. Theme Park
from the extracted ISO, a ZIP of that ISO, and a 7z CUE+BIN set produced the same
600-frame hash `0x110E7DC5`, PC `0x0000596C`, and identical PNG SHA-256. The suite is
**2,595/2,595**; build, `dotnet format`, `jb inspectcode`, and `mkdocs build --strict`
are clean. The local win-x64 release candidate (`5do-1.17.0-win-x64.zip`, SHA-256
`d056dacd9a02860fe2cff3494570038392aadcdc744afa1c07ddf08d7d8599f1`) reports 1.17.0,
passes `selftest` 15/15, writes `FRONTEND SMOKE OK 1.17.0`, opens ZIP/7z images, and
includes the SharpCompress notice with no PDB/XML files. Published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.17.0>; release run
<https://github.com/purelogiccode/5DO/actions/runs/44982714233> passed all five bundle
jobs, both AppImage/Flatpak package jobs, signing, and the publish job. The five
downloaded bundles match `SHA256SUMS.txt`; the published win-x64 bundle reports 1.17.0,
`selftest` 15/15, `FRONTEND SMOKE OK 1.17.0`, and verifies a 7z CUE+BIN image. The x64
AppImage passes FUSE and `--appimage-extract-and-run`, reports 1.17.0, writes
`FRONTEND SMOKE OK 1.17.0`, verifies with its detached `.asc`, and its embedded
`.upd_info`/`.zsync` target the versioned 1.17.0 image. The Linux x64 tarball CLI reports
1.17.0, passes `selftest`, and verifies the 7z CUE+BIN image. Evidence:
[ReleaseNotes-1.17.0.md](ReleaseNotes-1.17.0.md),
[SaveStates.md](SaveStates.md) §10, [Compatibility.md](Compatibility.md) §2, and
[KnownIssues.md](KnownIssues.md).

**2026-11-21, v1.16.1 release:** the optical verification and CD-DA diagnostics patch
release adds `disc audio <disc> --track N --out trackN.wav` for extracting Red Book
tracks from a CUE/CHD image or a host optical drive, with automatic first-audio selection
and a clear data-only message. `disc verify` now prints opening/progress lines on
non-JSON runs and uses the native track total so mixed-mode audio does not report a short
disc. The release also records the full optical sweep: 1,366 CHDs / 496 unique game IDs
opened and track-probed, 496 unique-title representatives fully sector-verified with 0
errors, and a 26-case BIOS matrix. The issue #8 reporter's Way of the Warrior CD-R read
157,996/157,996 sectors with hash `0x84b2bef01d157a9f`, matching the CHD; that burn is
data-only and has no CD-DA tracks. On 2026-11-22 the reporter's Red Book audio CD passed
the hardware CD-DA test: `disc tracks E:` listed six audio tracks and `disc audio E:`
extracted track 1 as a 164.28 s WAV with peak 31659 and RMS 7248.66 (0 clipped), so the
Windows raw-CDDA path works on a real drive. All **672** Redump 3DO cue sheets are
single-track MODE1, so no known retail 3DO game has a CD-DA track; a music CD is the
correct CD-DA test, and the FZ-10 audio-only player transport remains SYS-01 only. The
suite is **2,579/2,579**; build, `dotnet format`, and `jb inspectcode` are clean.
Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.16.1>; release run
<https://github.com/purelogiccode/5DO/actions/runs/44962202917> passed all five bundle
jobs, both AppImage/Flatpak package jobs, signing, and the publish job. The five
downloaded bundles match `SHA256SUMS.txt`; the win-x64 bundle reports 1.16.1, `selftest`
15/15, writes `FRONTEND SMOKE OK 1.16.1`, and `disc audio` extracts a non-silent A&M
track. The x64 AppImage runs through FUSE, reports 1.16.1, passes the smoke, verifies
with its detached `.asc`, and its embedded `.upd_info`/`.zsync` target the versioned
1.16.1 image. Release evidence is in [ReleaseNotes-1.16.1.md](ReleaseNotes-1.16.1.md),
[Compatibility.md](Compatibility.md) §10.8-10.9, and [KnownIssues.md](KnownIssues.md).

**2026-11-21, SYS-27 ZIP/7z containers (post-1.16.1):** `ArchiveDiscImage` opens a ZIP
or 7z archive that contains one ISO/BIN or one CUE+BIN set, decompressing entries in
memory through SharpCompress (1.5 GB per-entry limit). CUE `FILE` paths resolve relative
to the CUE entry, including subfolders. CHD/CDI/NRG entries, encrypted archives, and
multi-volume archives are refused with extraction/decryption guidance. The archive path
works through `DiscImageFactory`, the game library, drag/drop, recent discs, the playlist
editor, file pickers, and the CLI (`run`, `info`, `disc ...`, `fs ...`). Theme Park
(`33e5afe454a7c4da20b419a09e2045e2`) from the extracted ISO, a ZIP of that ISO, and a 7z
CUE+BIN set produced the same 600-frame hash `0x110E7DC5`, PC `0x0000596C`, and identical
PNG SHA-256. `ArchiveDiscImageTests` covers ZIP ISO reads, 7z CUE+BIN data/audio tracks,
nested CUE paths, multiple-image/CHD/PREGAP rejection, encrypted archives, and unrelated
entries; `GameLibraryScannerTests` covers archive discovery. The suite is
**2,595/2,595** and InspectCode is clean.

**2026-11-21, UX-37 rolling autosave and auto-resume (post-1.16.1):** the desktop now
keeps one autosave under `<save directory>/autosave/`, written on pause, clean exit, and
a configurable 1-60 minute interval (default 5), without touching manual slots. On
startup the last disc reopens and the autosave restores only when its disc game ID and
effective BIOS MD5 match; a mismatch is refused with an actionable message.
`AutoSaveEnabled` and `AutoResumeEnabled` are separate settings, **Emulation > Resume
Autosave** and **Delete Autosave** expose the file on demand, and RetroAchievements
hardcore suppresses writes and restores. Settings schema 11 adds the three fields.
`AutoSaveManagerTests` covers the relaunch round-trip, disc/BIOS refusal, corrupt state,
rolling overwrite, and delete; the suite is **2,586/2,586** and InspectCode is clean.
The Windows desktop pass on 2026-11-22 wrote frame 792 on clean exit, restored it on
relaunch and continued to frame 1532, left the file unchanged with both options off,
refused a modified disc and a different BIOS, and wrote frame 3655 at the 60-second
interval.

**2026-11-21, v1.16.0 release:** the physical CD-DA and disc read-error release ships
CD-DA playback from a host optical drive, guest-reported disc read errors instead of a
faulted core, and guarded screenshot/thumbnail writes. `PhysicalDiscSource` implements
`ITrackedDiscSource`: the drive TOC (`IOCTL_CDROM_READ_TOC` on Windows,
`CDROMREADTOCHDR`/`CDROMREADTOCENTRY` on Linux) supplies data and audio tracks, raw
2352-byte Red Book frames use `IOCTL_CDROM_RAW_READ` in CDDA mode or
`CDROMREADAUDIO`, and CD-DA end addressing follows the physical track table rather than
the volume-header data limit. A failed host read (the issue #8 CRC report at sector
103777) is zero-filled, reported to the guest as MEI receive-ECC, and logged while the
transfer continues; the boot-time volume-header probe and CD-DA sample reads share the
guard. The suite is **2,576/2,576**; build, `dotnet format`, and `jb inspectcode` are
clean. Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.16.0>;
release run <https://github.com/purelogiccode/5DO/actions/runs/44917920441> passed all
five bundle jobs, both AppImage/Flatpak package jobs, signing, and the publish job. The
five downloaded bundles match `SHA256SUMS.txt`. The win-x64 bundle reports 1.16.0,
passes `selftest --json` with all 15 checks, and writes `FRONTEND SMOKE OK 1.16.0`; the
Linux x64 CLI/frontend smoke and the `.deb`/`.rpm` installs passed in CI. The issue #8
reporter's 1.16.0 test confirms the sector-103777 CRC is now a Warning and emulation
continues; the Way of the Warrior FMV pauses for the drive's recovery and then resumes.
A Red Book audio CD reaches the FZ-10 CD player with track 1 listed, but the transport
remains inert (SYS-01), and the opt-in DIPIR path still has to be off for a disc boot. The
hardware CD-DA check was completed on 2026-11-22 under 1.16.1 with the reporter's Red
Book audio CD (see above); the Windows TOC and cooked paths had been validated on a
mounted virtual optical drive. Evidence is recorded in
[ReleaseNotes-1.16.0.md](ReleaseNotes-1.16.0.md), [Compatibility.md](Compatibility.md)
§10.7, and [KnownIssues.md](KnownIssues.md).

**2026-11-21, 3DO collection optical sweep:** the local `J:\Panasonic 3DO` collection
has 1,366 CHDs and 496 unique game IDs. All 1,366 opened and every track's first,
middle, and last native sector read successfully; 496 unique-title representatives,
including all three audio-only discs, were fully sector-verified with **0 read errors**.
A 26-case BIOS matrix covered US FZ-1/FZ-10/GoldStar/Sanyo, JP FZ-10J plus the Kanji
BIOS2, PAL FZ-10E Anvil, patched FZ-10 + `3do_arcade_saot` for the Arcade titles, and
the audio-only discs; every case booted at 3,000 frames. The FZ-10 player lists the
audio-only TOC but its transport remains STOP (SYS-01, matching 4DO). Non-JSON
`disc verify` now prints opening and progress lines, the verifier uses the native track
total so mixed-mode audio does not report a short disc, and the new
`disc audio <disc> --track N --out trackN.wav` command extracts a CD-DA track to WAV
from an image or a physical drive. The issue #8 reporter's Way of the Warrior CD-R
verified at 157,996/157,996 sectors, hash `0x84b2bef01d157a9f`, matching the CHD; that
burn has only a data track, so CD-DA needs an audio CD. That audio-CD pass was completed
on 2026-11-22 under 1.16.1. Full suite **2,579/2,579**, build/format/InspectCode clean.
Evidence:
[TestCorpus.md](TestCorpus.md) §1.1ci and [Compatibility.md](Compatibility.md) §10.8.

**2026-11-21, v1.15.0 release:** the Steam Deck first-run, Arcade CRT, and physical
optical-drive release ships the Deck/gamescope welcome layout with **Scan** and
**Open Folder**, the stronger `ArcadeCrt` filter on OpenGL/GLES, Vulkan, Direct3D 11,
and the software fallback, and the issue #8 optical-drive discovery fix. It also fixes
the JIT soft-reset state overwrite and the single-joypad PBUS packet selection, centers
and sizes the welcome guide for its discovered-BIOS panel, and aligns the Direct3D
Arcade CRT clamps with the other backends. The suite is **2,561/2,561**; build,
`dotnet format`, and `jb inspectcode` are clean. Published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.15.0>; release run
<https://github.com/purelogiccode/5DO/actions/runs/44905930950> passed all five bundle
jobs, both AppImage/Flatpak package jobs, signing, and the publish job. The five
downloaded bundles match `SHA256SUMS.txt`. The win-x64 bundle reports 1.15.0, passes
`selftest` 15/15, the 600-frame Theme Park hash `0x110E7DC5` / PC `0x596C` and the
3,000-frame hash `0xAD4F370C` / PC `0x7AEF8`, and writes
`FRONTEND SMOKE OK 1.15.0`. The Linux x64 tarball reports 1.15.0, passes `selftest` and
the frontend smoke on hardware OpenGL 4.2, and reproduces the same 600/3,000-frame
hashes. The x64 AppImage runs through FUSE and `--appimage-extract-and-run`, reports
1.15.0, passes the frontend smoke, and its detached `.asc` verifies with the release
key. `appimageupdatetool -j` on the published 1.14.2 AppImage reported an update
available; `-d` fetched 52,845,048 bytes and produced a 1.15.0 AppImage whose SHA-256
matches `SHA256SUMS.txt`, and the updated image passed the 1.15.0 smoke. Evidence is
recorded in [ReleaseNotes-1.15.0.md](ReleaseNotes-1.15.0.md),
[Compatibility.md](Compatibility.md) §10.7, and [KnownIssues.md](KnownIssues.md).

**2026-11-21, v1.14.2 release:** the packaging and diagnostics bug-fix release ships the
app-local MSVC runtime in the Windows bundles, corrected AppImage update metadata,
cover-catalog HTTP fallback, and quieter bug reports for expected conditions. The five
bundles, both AppImages with `.zsync`, both Flatpaks, and the signed Linux packages are
built from the tagged source. The suite is **2,535/2,535**, `dotnet build` reports
0 warnings/0 errors, and `dotnet format` and `jb inspectcode` are clean. Published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.14.2>; release run
<https://github.com/purelogiccode/5DO/actions/runs/44861080545> passed all five bundle
jobs, both Linux package jobs, signing, and the publish job. The five downloaded bundles
match `SHA256SUMS.txt`; the published win-x64 CLI `selftest` and frontend smoke report
`FRONTEND SMOKE OK 1.14.2`. Post-release CI run
<https://github.com/purelogiccode/5DO/actions/runs/44863848266> passed on Windows, Linux,
and macOS. Evidence is recorded in
[ReleaseNotes-1.14.2.md](ReleaseNotes-1.14.2.md),
[Compatibility.md](Compatibility.md) §10.6, and [KnownIssues.md](KnownIssues.md).

**2026-11-21, Arcade CRT filter:** a new `ArcadeCrt` video filter adds a stronger
old-television/arcade-monitor look: thicker scanlines, an RGB triad mask, slight
phosphor fringing, warm 5x5 halation, more curvature, and a heavier vignette. It is
available on OpenGL/GLES, Vulkan, and Direct3D 11 and uses the existing CRT bloom
control; the software presenter applies the scanline/mask/vignette pass after bilinear
scaling. `--gl-probe-filter arcade-crt` and the Vulkan/Direct3D probe aliases pass with
`filterFallback: false`; captured frames show the effect on all three backends. The suite
is **2,547/2,547**.

**2026-11-21, physical optical-drive discovery (issue #8):** **File > Boot Optical
Drive...** no longer relies on `DriveInfo.IsReady`. A 3DO disc's custom filesystem is not
mountable by Windows, so the drive can have no ready volume or no drive letter even with
a disc inserted. `PhysicalDiscSource.GetOpticalDriveNames` now opens the optical device
and probes media with `IOCTL_STORAGE_CHECK_VERIFY2`, falls back to the CD-ROM geometry
request, maps letters through `QueryDosDevice`, and enumerates `\\.\CdRomN` for drives
without a letter. The picker labels those entries **no drive letter**, and the CLI,
startup path, and `doctor` accept the same form. Evidence: a 3DO ISO was rejected by
Windows `Mount-DiskImage` as an unreadable filesystem; a valid ISO mounted as a virtual
DVD drive, its drive letter was removed with `mountvol`, `doctor` then reported
`\\.\CdRom0`, and `info \\.\CdRom0` opened the device and read all 22 sectors. The issue
reporter then confirmed that burned CD-R games boot and play their FMV intro sequences,
closing the SYS-21 hardware `Done when` line. The suite is **2,558/2,558**.

**2026-11-21, optical-drive follow-ups:** two issue #8 regressions found after the CD-R
boot report are fixed. The JIT backend no longer lets an interrupted block's local
PC/CPSR overwrite a CLIO CSTAT soft reset; the reset resumes at address zero with
`R14_svc`/`SPSR_svc` intact. `InputDeviceSet.HasPeripheral` now examines only active
ports, so a single joypad with disabled ports 7-8 left at `None` uses the legacy 16-byte
PBUS packet instead of the Opera daisy chain, restoring keyboard and gamepad input.
`ArmJitSoftResetTests` covers the JIT reset path, and `InputManagerTests` covers both
packet-format choices. The Opera soft-reset path itself remains opt-in and still needs a
CSTAT/DIPIR boot transcript for full accuracy validation.

**2026-11-21, REL-10 Steam Deck first-run:** the welcome guide now detects
`SteamDeck`/`SteamOS`/`SteamGamepadUI`/gamescope and uses a 960x680 layout with 54 px
buttons, the BIOS folder path, **Scan**, and **Open Folder**. Discovery still prefers
the `5DO data folder/bios` location, and the BIOS requirement remains visible for
keyboard-less sessions. [Installation.md](Installation.md) documents Desktop Mode,
copying the BIOS, adding the AppImage/Flatpak as a non-Steam game, `--fullscreen`, and a
trackpad controller layout. The upstream EmuDeck/RetroDeck entry is the only remaining
REL-10 item. `DeckModeTests` covers environment detection and the larger layout.

**2026-11-21, REL-07 closed:** the published `5do-1.14.2-linux-x86_64.AppImage` reports
`gh-releases-zsync|purelogiccode|5DO|latest|5do-*-linux-x86_64.AppImage.zsync`, and its
`.zsync` URL points at the versioned AppImage. On the LAN Linux x64 host it runs through
FUSE and `--appimage-extract-and-run`, reports 1.14.2, passes the frontend smoke
(`FRONTEND SMOKE OK 1.14.2`) on hardware OpenGL 4.2, and its detached `.asc` verifies
with the release key. `appimageupdatetool -d` resolved the latest 1.14.2 `.zsync`,
`-j` reported no update, and a local 1.14.2-to-test-1.14.3 pass fetched 2,570 bytes over
an HTTP Range server and matched the target hash. CI does not embed an AppImage
signature, so detached `.asc` remains the signing format. Evidence:
[LinuxDistribution.md](LinuxDistribution.md) §5,
[ImplementationPlanArchive.md](ImplementationPlanArchive.md) §9.

**2026-11-20, REL-11 Windows app-local MSVC runtime:** the Windows bundles now ship
`vcruntime140.dll` and `msvcp140.dll` (plus `vcruntime140_1.dll` on x64) beside
`OpenAL32.dll`, closing the clean-image audio prerequisite. `package-bundle.ps1` resolves
the target-architecture files through `vswhere` (or an explicit `-VcRuntimeDirectory`) and
rejects missing or wrong-architecture DLLs; `build-bundle.yml` verifies the extracted
bundle. On the Windows 11 ARM64 VM with the VC++ redistributable uninstalled, the old
bundle reported `OpenAL: not found (OpenAL32.dll)` and the new 1.14.2 ARM64 bundle
reported `OpenAL: loaded OpenAL32.dll`; the x64 bundle also loaded its app-local runtime
under x64 emulation. `selftest --json` passed 15/15, and a 600-frame Theme Park run
produced frame hash `0x110E7DC5` and PC `22892`, matching Windows x64. A 60-frame
`--audio openal` run with `ALSOFT_DRIVERS=null` exited 0 with 44,144 samples,
and the desktop smoke reported `FRONTEND SMOKE OK 1.14.2`. The suite is
**2,535/2,535**; build, `dotnet format`, and `jb inspectcode` are clean. The VM was
deallocated after the pass. Evidence:
[Compatibility.md](Compatibility.md) §10.6, [KnownIssues.md](KnownIssues.md).

**2026-11-20, v1.14.1 release:** the deep-review bug-fix release aligns CHD Mode 2
native track sectors (2336-byte track data and disc-health/DAT hashing), drops stale
cropped-frame dimensions when auto-crop stops, and hardens persistence, malformed
settings, CD-ROM/state validation, and shutdown across the OpenAL, RetroAchievements,
Vulkan, Direct3D, and core workers. Writes for save slots, screenshots, NVRAM, BIOS,
movies, caches, and diagnostics are atomic with unique temporary names, and the new
2336-byte CUE/CHD round-trip test covers the native-sector and user-data paths. The
suite is **2,534/2,534**, `dotnet build` reports 0 warnings/0 errors, `dotnet format`
and `jb inspectcode` are clean, and CI passes on Windows, Linux, and macOS. Release
notes: [ReleaseNotes-1.14.1.md](ReleaseNotes-1.14.1.md). Published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.14.1>; release run
<https://github.com/purelogiccode/5DO/actions/runs/44829339302>, CI run
<https://github.com/purelogiccode/5DO/actions/runs/44827374563>, docs run
<https://github.com/purelogiccode/5DO/actions/runs/44827374615>, and platform runtime
validation <https://github.com/purelogiccode/5DO/actions/runs/44827374731> passed (the
Windows arm64 job was rerun after a runner-level PowerShell CLR crash). The five
published bundles match `SHA256SUMS.txt`; the win-x64 CLI `selftest` and frontend smoke
report `FRONTEND SMOKE OK 1.14.1`.

**2026-11-20, v1.14.0 release:** the high-resolution and input fix release makes
`--high-res` presentation-only (the guest stays at native 320x240 and VDLP performs an
exact 2x upscale for 320-wide modes), fixes the OpenGL `RG32UI` 480-line upload, and
completes the Gamegun defaults: retail American Laser Games titles keep a joypad on port
1, Demolition Man starts with the pad and connects the gun with F1, and the database
input devices now resolve for both the full disc checksum and the 8-character 4DO ID.
The legacy gun-only override migration now runs once instead of deleting an explicit
choice on every launch. Defaults are P to pause, Space to toggle fast-forward, F1 to
connect the Gamegun, and Alt+Enter for fullscreen. The suite is **2,475/2,475**,
`dotnet format` and `jb inspectcode` are clean, and a 300-frame Road Rash `--high-res`
run matches a 2x nearest upscale of the native frame with 0 differing pixels. Release
notes: [ReleaseNotes-1.14.0.md](ReleaseNotes-1.14.0.md). Published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.14.0>; release run
<https://github.com/purelogiccode/5DO/actions/runs/44781829378>, CI run
<https://github.com/purelogiccode/5DO/actions/runs/44774987267>, and docs run
<https://github.com/purelogiccode/5DO/actions/runs/44774985985> passed. The five
published bundles match `SHA256SUMS.txt`, and the win-x64 CLI `selftest` plus frontend
smoke report `FRONTEND SMOKE OK 1.14.0`.

**2026-11-19, v1.13.2 release:** the bug-fix release ships the `SYS-26`
movie-verification fix and the `UX-36` partial-extraction preflight, with CHDSharp 1.7.1
and refreshed lock files. The suite is **2,449/2,449**, `dotnet format` and
`jb inspectcode` are clean, and `dotnet restore --locked-mode` passes; CI publishes the
five bundles and the signed Linux assets. Release notes:
[ReleaseNotes-1.13.2.md](ReleaseNotes-1.13.2.md).

**2026-11-19, startup preflight for partial extractions:** issues #69923/#69924 reported
`SkiaSharp.SKImageInfo` / `Dll was not found` from a bundle run out of `Downloads`. The
desktop frontend now verifies that `libSkiaSharp` and `libHarfBuzzSharp` sit beside the
apphost before Avalonia starts, then shows a **5DO cannot start** message naming the
missing files and the extraction steps (Windows dialog; stderr for automated runs) and
exits 1 without logging a fatal or filing a bug report. Verified against a self-contained
win-x64 publish: the apphost alone gets the message, and the complete folder passes the
smoke test. Evidence: `NativeRuntimeCheckTests` (6), [Troubleshooting.md](Troubleshooting.md)
§1, README bundle notes.

**2026-11-19, SYS-26 fixed:** `movie verify` no longer stops one frame early. The movie
header now records the emulated frame span (`EmulatedFrames`, absent on older movies), so
a 600-frame recording verifies its 599 packets over 600 frames, and verification compares
that span instead of the packet counter. Playback and verification resolve the recorded
disc by file name beside the movie or in the working directory and the BIOS by MD5 in the
default folders, failing with an actionable message instead of silently replaying without
them; `movie record --disc` now behaves like `movie play --disc`. A 600-frame Theme Park
movie and a 300-frame `--state-in` branch verify with exit 0, the suite is
**2,443/2,443**, and `dotnet format` is clean. Evidence: [Tas.md](Tas.md) §4,
[ImplementationPlanArchive.md](ImplementationPlanArchive.md) §12.

**2026-11-19, Windows arm64 hardware validation:** the published `win-arm64` ZIP ran on
an Azure Cobalt 100 VM (Windows 11 Pro 25H2 build 26200, ARM64, no GPU). The SHA-256
matches, every apphost and native DLL is an ARM64 PE, the suite is **2,439/2,439**, and
the 600-frame hash `0x110E7DC5`, the 3,000-frame hash `0xAD4F370C` with PC `0x0007AEF8`,
and the Road Rash 20,000-frame autopilot hash `0x898FAD6E` match Windows x64 and Linux
arm64 exactly. The CLI, audio capture, trace, doctor, state resume, movie record/play,
CHD conversion, portable mode, and update check pass; the desktop renders through
ANGLE/GL ES 3.0 at 61.5 fps and covers fullscreen; `--gl-probe` and `--d3d-probe` pass
while Vulkan is absent on this VM. The bundled OpenAL DLL needs the VC++ 2015-2022 ARM64
redistributable on a clean image; recorded in [KnownIssues.md](KnownIssues.md). Evidence:
[Compatibility.md](Compatibility.md) §10.6.

**2026-11-19, Linux arm64 hardware validation:** the published `linux-arm64` release
artifacts ran on ARM64 hardware for the first time. On an Azure `Standard_D2ps_v6`
(Neoverse-N2, aarch64, Ubuntu 24.04.5, no GPU) the 1.13.1 bundle matches its published
SHA-256, passes **2,439/2,439** tests with .NET 11.0.100, and reproduces the Windows x64
frame hashes exactly (600 frames `0x110E7DC5`, 3,000 frames `0xAD4F370C`, PC
`0x0007AEF8`). The CLI `selftest`/`bench`/`bios`/`info`/`trace`/`doctor` paths, audio
capture, 4DO state roundtrip, CHD conversion, and the Road Rash autopilot pass; the
desktop shell renders under XFCE (software and Vulkan/lavapipe) at 59.9 fps, and the
AppImage, `.deb`, `.rpm`, and Flatpak packages install and run on the host. llvmpipe
OpenGL still times out and `movie verify` stops one frame early on every platform; both
are recorded in [KnownIssues.md](KnownIssues.md). Evidence:
[Compatibility.md](Compatibility.md) §10.5.

**2026-11-18, v1.13.1 release:** a naming and documentation update. The RPM, AppImage, and
Flatpak assets now include the version and `linux` in their file names
(`5do-1.13.1-1.linux.x86_64.rpm`, `5do-1.13.1-linux-x86_64.AppImage`,
`5do-1.13.1-linux-x86_64.flatpak`), while the `.deb` keeps the Debian convention, and
every release page opens with a per-platform download table. `jb inspectcode` is clean.

**2026-11-18, v1.13.0 release:** 5DO 1.13.0 ships the native macOS Metal surface, the
four Phoenix/4DO presentation options (audio channel swap, named void patterns, GPU void
color, and the emulation-health badge), and a signed Linux release chain. The release is
published at <https://github.com/purelogiccode/5DO/releases/tag/v1.13.0> with the five
bundles, both Flatpaks, both AppImages, the `.deb`/`.rpm` sets, `5do-archive-keyring.gpg`,
a signed `SHA256SUMS-linux.txt`, and a detached `.asc` for every Linux artifact. The
suite is **2,439/2,439**; CI builds and signs both architectures, installs the
`.deb`/`.rpm` and the Flatpak bundles, runs the sandboxed `selftest`, and verifies the
signed APT/RPM mirror trees. Release key: ed25519 `Pure Logic Code (5DO releases)`,
fingerprint `E69FF266D1FB01D3411578775C4C1E8EAFD44075`, expires 2029-11-18; the
fingerprint and the rotation rules are in [LinuxDistribution.md](LinuxDistribution.md)
§4. Release notes: [ReleaseNotes-1.13.0.md](ReleaseNotes-1.13.0.md).

**2026-11-18, native macOS Metal surface:** **macOS arm64 now presents through a native
`CAMetalLayer` surface (`VK_EXT_metal_surface`), with no per-frame CPU readback.** On a
rented NoMac Apple M5 Pro (Virtual, macOS 26.6.2, MoltenVK 1.4.363) `--vk-window-probe`
reports `ok: true`, 60.1 fps at 1080p FIFO, and 0 renderer bytes/frame; a 320x240
Accurate capture matches the CPU oracle with 0 different pixels. The desktop shell plays
Wolfenstein 3D at 59.9 fps and follows live resize and fullscreen through the same
surface. The full suite is **2,429/2,429**, and CLI `selftest` plus frontend
`--smoke-test` pass. The offscreen MoltenVK presenter remains the fallback. macOS OpenGL
is still not a presentation path (Avalonia has no macOS OpenGL control); `--gl-probe`
fails fast and points at `--vk-probe`. The macOS presentation matrix (windowed, resize,
fullscreen, 320x240 capture, 4K offscreen) is validated at 1x; the remaining macOS work
is the `PLAT-17` Retina 2x row, which the rented host's 1x-only display cannot exercise,
and `PLAT-05` (physical gamepad). Evidence:
[Compatibility.md](Compatibility.md) §10.4, [Vulkan.md](Vulkan.md),
[TestCorpus.md](TestCorpus.md) §1.1co and §1.1ct.

**2026-11-17, v1.12.0 macOS GPU release:** macOS arm64 renders through Vulkan/MoltenVK
on Metal. The v1.12.0 desktop shell used the offscreen presenter (since replaced by the
native surface above), reported `Vulkan (Metal)`, and played Wolfenstein 3D at
59.8-60.1 fps; all eight filters pass the Vulkan probe without fallback. The full suite
was **2,394/2,394**, CLI `selftest` and frontend `--smoke-test` passed, and the
native `.dmg`/`.pkg`/`.tar.gz` smoke-tested from both the extracted tarball and the
mounted DMG with matching SHA-256. The macOS audio live-switch fix passes all three audio
tests after outputs were made to share one OpenAL context per device. The build now runs
the net8.0 shader compiler on the installed .NET 9/10/11 runtime, so only the .NET 11 SDK
is required for builds. Release bundles are self-contained: the .NET 11 runtime is
embedded and no separate runtime install is required. Intel macOS support was dropped in
1.12.0; Apple Silicon is the only supported macOS target.
[KnownIssues.md](KnownIssues.md), [ReleaseNotes-1.12.0.md](ReleaseNotes-1.12.0.md).

**v1.12.0 release evidence:** five self-contained bundles and `SHA256SUMS.txt` are
published at <https://github.com/purelogiccode/5DO/releases/tag/v1.12.0>. Release run
<https://github.com/purelogiccode/5DO/actions/runs/44141799807> passed all five bundle
jobs and the publish job; every published SHA-256 entry verified locally after download
(win-x64 113.9 MB, win-arm64 108 MB, linux-x64 106.5 MB, linux-arm64 99.9 MB,
osx-arm64 117 MB). CI run
<https://github.com/purelogiccode/5DO/actions/runs/44144837632>, platform runtime run
<https://github.com/purelogiccode/5DO/actions/runs/44141793058>, docs run
<https://github.com/purelogiccode/5DO/actions/runs/44141793084>, and CodeQL run
<https://github.com/purelogiccode/5DO/actions/runs/44141793190> passed; the macOS
bundle job verified the bundled Vulkan/MoltenVK runtime and self-contained apphosts.

**2026-11-17, REL-07/REL-09 (next release):** **Linux x64 and arm64 now have the full
bundle set.** `linux-packages.yml` builds `5do-x86_64.AppImage` and
`5do-aarch64.AppImage` with zsync information on their native runners, plus
`5do-x86_64.flatpak` and `5do-aarch64.flatpak` from the published apphosts. The manifest
(`packaging/linux/io.github.purelogiccode.FiveDO.yml`) repackages the self-contained
apphosts, disables stripping, and declares the network, X11/Wayland, PulseAudio, device,
home, `/media` + `/run/media`, and screen-saver permissions. CI installs each bundle and runs
`5do-cli selftest --json` inside the sandbox; the 2026-11-18 run
<https://github.com/purelogiccode/5DO/actions/runs/44324226502> built
`5do-x86_64.flatpak` and `5do-aarch64.flatpak` on their own architectures, installed them
with `flatpak install --user`, and passed the version check and the sandboxed selftest.
The runbook is in [LinuxDistribution.md](LinuxDistribution.md).

**2026-11-17, REL-04..REL-07:** **Linux packages and release signing are implemented and
wired into the release pipeline.** `packaging/linux/` now builds `.deb`/`.rpm` packages
with AppStream metadata, 256 px icons, translated desktop entries, and a self-contained
dependency list (the old `dotnet-runtime-11.0` dependency is gone), plus an x64 AppImage
with zsync update information. `sign-artifacts.sh` adds detached `.asc` signatures and a
published `5do-archive-keyring.gpg`. The packages are published as **GitHub release
assets**; users install them with `sudo apt install ./5do_<version>_amd64.deb` or
`sudo dnf install ./5do-<version>-1.x86_64.rpm`, and no package repository is hosted.
`build-apt-repo.sh`/`build-rpm-repo.sh` build signed `apt`/`dnf` trees for operators who
mirror the files; the path is validated on Debian 13, Ubuntu 22.04.1, Linux Mint 22.3,
and Fedora 45 (2026-11-17, [LinuxDistribution.md](LinuxDistribution.md) §5.1 and
[TestCorpus.md](TestCorpus.md) §1.1cn). GNOME Software 46.0 on Linux Mint 22.3 renders the
installed package with its icon and screenshots, and a real ISO keeps its ISO MIME type
(2026-11-18, [LinuxDistribution.md](LinuxDistribution.md) §5.2). `linux-packages.yml` runs
from `release.yml` and installs both the `.deb` and the `.rpm` in CI to verify the desktop
integration and that the packaged apphosts start; CI builds the unsigned path on every
push.
**Validated 2026-11-17** by CI run
<https://github.com/purelogiccode/5DO/actions/runs/44193278546>: native x86_64/aarch64
builds, desktop/MIME/AppStream validation, `.deb` and `.rpm` install tests (both
apphosts start), AppImage self-check, and checksums. The run is unsigned because
`LINUX_GPG_PRIVATE_KEY` is not configured; the signing path activates on the first
tagged run with the secret. The runbook, verification steps, and announcement channels
are in [LinuxDistribution.md](LinuxDistribution.md). Linux arm64 packages are validated
on hardware (2026-11-19); the AUR/AppImageHub/itch.io channel listings stay open as
`REL-08`..`REL-10`.

**2026-11-17, UX-34/UX-35:** **BIOS discovery and `5do-cli doctor` remove the two biggest
Linux support round-trips.** `bios discover` searches `FIVEDO_BIOS_DIR`, `<data>/bios`,
`bios` next to the executable, `~/BIOS`, and `<Documents>/BIOS`, and the first-run guide
offers the best image with a one-click **Use this BIOS** button. `doctor` reports the
session (including Wayland-only detection), data-folder write access, disk space,
OpenGL/Vulkan/OpenAL loading, evdev and optical devices, settings access, and BIOS
discovery, with `--json` and `--strict`. The Linux bundle now also carries its desktop,
MIME, AppStream, and icon files under `packaging/`, and the desktop entry has a
**Play in fullscreen** action. Suite: **2,401/2,401**; format clean; frontend smoke
passes. Evidence: `BiosDiscoveryTests` (4), `DoctorReportTests` (3),
[Installation.md](Installation.md), [Diagnostics.md](Diagnostics.md).

**2026-11-16, VID-19:** **The true 2× CEL high-resolution prototype is opt-in and tested.**
The new `--true-2x` machine option (Settings > Emulation > **True 2x CEL fields**, per-game
**TrueHighResolution**, and a save-state field) keeps the four-plane VRAM layout but writes
even output lines to planes 0/1 and odd output lines to planes 2/3, so arbitrary CELs gain
real vertical detail at 640x480. Packed and scaled 2D CELs expand to 2x2 blocks; bitmap and
SPORT content remains line-doubled. Jurassic Park Interactive's Raptor Maze renders the full
corridor and raptor encounter (38.41% of pixels differ from a 2x nearest upscale), while
Family Feud is a clean 2x scale (0.00% difference). Native hashes are unchanged
(`0xD5D6A993` at 63,000), the full suite passes (2,367 tests), and true 2× costs about 30%
more emulation time (177 fps versus 232 fps, two-run average), still more
than 3x real time. **The OpenGL 480-line line-parameter upload bug caused the Road Rash
bottom rainbow; it was fixed on 2026-11-19.** The true 2× option is now available for
every title; Jurassic Park Interactive and Family Feud remain the visually validated
references, and unvalidated titles may misrender bitmap/SPORT or framebuffer-read
content. The legacy presentation-only `--high-res` mode remains available. Remaining
production work is bitmap/SPORT sampling, framebuffer-read semantics, and a full-collection
sweep under VID-01. Evidence: `benchmarks/diff/vid19-true2x.json`, `artifacts/vid19`,
[TestCorpus.md](TestCorpus.md) 1.1cl, and
[HighResolutionCelResearch.md](HighResolutionCelResearch.md) §1.

**2026-11-16, ACC-04:** **Jurassic Park Interactive now reaches in-park gameplay.**
The earlier stop on the Security Tracking map was input scripting, not CEL accounting or
rendering: the Operations console is a pointer interface, and the old script never moved
the pointer hand onto a guest or confirmed a destination. The new
`jurassic-park-raptor-maze.txt` selects Robert Muldoon, chooses the Rest Room, and starts
the **Raptor Maze** at 63,000 frames; the raptor encounter and **SECURITY BREACH** outcome
render at 65,000/65,500 and the map returns at 66,000. 5DO and OperaCore frame hashes
match at 40,000/42,000/57,000/62,000/63,000. The first observed visual divergence is the
65,000 checkpoint: the default FreeDO CEL model is ahead of the reference's static
corridor, while `--cel-timing opera` matches the reference hash `0xD5D6A993` exactly. ACC-04 closes as an
input-scripting fix plus a default-CEL-pacing residual (§4 of the plan); the row stays
`Playable` until the driving/Spitter paths are scripted and the default pacing is
resolved. Evidence: `artifacts/acc04`,
[TestCorpus.md](TestCorpus.md) 1.1ck, and
[DifferentialHarness.md](DifferentialHarness.md) §8.

**2026-11-15, v1.10.2 release:** **`SYS-24` is complete and the Windows OpenAL
runtime is fixed.** DiscJuggler `.cdi` and Nero `.nrg` images now open through the CLI,
File > Open Disc, and the game library. Two standalone readers, `CDISharp` and
`NRGSharp`, parse the trailing CDI descriptor and the NERO/NER5 chunk chain;
`CdiDiscImage` and `NrgDiscImage` normalize cooked 2048-byte, 2336-byte Mode 2, raw
2352-byte, and the 2353/2368/2448/2449-byte variants to the shared
`IDiscSource`/`ITrackedDiscSource` contract. Malformed images fail with typed errors.

Windows bundles now publish **`OpenAL32.dll`** beside `soft_oal.dll`. The publish target
previously copied only the NuGet package name, so Silk.NET loaded a system OpenAL build
and bug reports 69885-69891 failed with *The OpenAL context could not be made current*.
`package-bundle.ps1` and the bundle workflow reject an archive without the loader-named
runtime, and audio startup failures are logged with the failing path and ALC error
without being submitted as bug reports. The suite is **2,365 tests**; build, format,
and InspectCode are clean. Existing 1.9.x and 1.10.0/1.10.1 copies can copy `soft_oal.dll` to
`OpenAL32.dll` as a workaround.

**v1.10.2 release evidence:** the six bundles and `SHA256SUMS.txt` are published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.10.2>. Release run
<https://github.com/purelogiccode/5DO/actions/runs/43776600683> passed all six bundle
jobs and the publish job; all six hashes verify, and the assets are copied into
`src/FiveDo.Frontend.Avalonia/bin/Release`. CI run
<https://github.com/purelogiccode/5DO/actions/runs/43776600895> passed after a
transient win-x64 artifact-upload retry. Docs run
<https://github.com/purelogiccode/5DO/actions/runs/43778248201> deployed the Pages site
and wiki; `WhatsNew.html`, `ReleaseNotes-1.10.2.html`, and `wiki/Release-1.10.2` return
HTTP 200.

**2026-11-17, v1.11.0 release:** **Linux x64 is validated end to end and the
found defects are fixed.** On the bare-metal Linux Mint 22.3 host (Intel HD 4000, Mesa
25.2.8, 1920x1080 X11), hardware OpenGL 4.2 presents at 61 fps, Vulkan 1.2 presents
through X11 in the window probe (0 different pixels at 320x240) and the desktop shell
(no per-frame readback, resize supported), a 600 s real-device audio soak reports 0
dropped samples and 0 underruns, evdev gamepads drive the 3DO, display sleep is
inhibited while unpaused, and fullscreen/save/load/pause/screenshots/exit pass. The
release also adds the opt-in true 2x CEL path for Jurassic Park Interactive and Family
Feud and fixes the Linux gamepad ioctl encoding, the OpenAL PipeWire exit segfault, the
1x1 software/Vulkan screenshots, the noisy save-state version warning, and the macOS
Vulkan-to-OpenGL fallback. The suite is **2,393 tests**; build, format, and InspectCode
are clean.

**v1.11.0 release evidence:** the six bundles and `SHA256SUMS.txt` are published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.11.0>. Release run
<https://github.com/purelogiccode/5DO/actions/runs/44028488601> passed all six bundle
jobs and the publish job; all six hashes verify, and the assets are copied into
`src/FiveDo.Frontend.Avalonia/bin/Release`. CI run
<https://github.com/purelogiccode/5DO/actions/runs/44027199860>, docs run
<https://github.com/purelogiccode/5DO/actions/runs/44027199667>, platform runtime run
<https://github.com/purelogiccode/5DO/actions/runs/44027199636>, CodeQL, and Scorecard
all passed.

**2026-11-15 update:** **v1.10.1** adds RetroAchievements save-state progress
persistence, renames the desktop apphost to **`FiveDo`** (`FiveDo.exe` on Windows), and
cuts time to the first window with lazy audio, deferred BIOS/disc loading, background
cache loads, and ReadyToRun release bundles. Achievement hit counts, measured values,
delta/prior memory references, and active leaderboard attempts now live in a dedicated
`RetroAchievements` section of the `.tdos` save state and are restored only when the
game hash and every definition match; otherwise the runtime resets normally. The suite
is **2,344 tests**; build, format, and InspectCode are clean. The validation host reached
the first window in about **0.62 s** with the ReadyToRun bundle versus **1.09 s** with
the debug build. The shell also shows a localized **Loading game...** overlay until the
first visible frame, and the first Escape in a running fullscreen game now leaves
fullscreen and pauses in one step. See [WhatsNew.md](WhatsNew.md) and
[ReleaseNotes-1.10.1.md](ReleaseNotes-1.10.1.md).

**1.10.1 release update (2026-11-15):** **v1.10.1** is published on
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.10.1) as six
framework-dependent bundles (macOS as DMG) plus `SHA256SUMS.txt`. CI run
<https://github.com/purelogiccode/5DO/actions/runs/43760004443> passed every required
job, including the six bundle smoke jobs. Release run
<https://github.com/purelogiccode/5DO/actions/runs/43760492909> built and published the
bundles. Docs run <https://github.com/purelogiccode/5DO/actions/runs/43760004392>
deployed the Pages site and published the wiki. All six checksums verify; the assets are
copied into `src/FiveDo.Frontend.Avalonia/bin/Release`.

**1.10.0 update:** **v1.10.0** adds RetroAchievements as a managed in-tree feature.
The MIT rcheevos evaluation engine is ported to `FiveDo.Achievements`, 3DO disc hashing
is byte-identical to RAHasher 1.8.3 on real CHD images, and the desktop shell integrates
sign-in, game identification, achievements, hardcore rules, leaderboards, rich presence,
and OSD notifications. Achievement unlocks survive disconnects in a checksummed,
atomically written offline queue; hardcore mode blocks save-state loads, rewind, frame
advance, cheats, TAS/movie playback, input recording, and input tools while permitting
fast-forward and creating save states; casual-to-hardcore switches reset the game before
the hardcore session starts. Leaderboard entries are submitted only in hardcore, and
casual-only unlocks remain active in hardcore. The suite is **2,338 tests**; build,
format, and InspectCode are clean. Live login, patch, session, ping, hardcore-session,
and casual-award validation passed against the real server; leaderboard submission was
not exercised because the tested 3DO sets had no leaderboards. Hardcore credit is pending
RetroAchievements client approval. See [RetroAchievements.md](RetroAchievements.md) and
[RetroAchievementsCompliance.md](RetroAchievementsCompliance.md).

**1.10.0 release update (2026-11-15):** **v1.10.0** is published on
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.10.0) as six
framework-dependent bundles (macOS as DMG) plus `SHA256SUMS.txt`. CI run
<https://github.com/purelogiccode/5DO/actions/runs/43735222579> passed every required
job. The docs workflow deployed the Pages site and wiki. All six checksums verify; the
assets are copied into `src/FiveDo.Frontend.Avalonia/bin/Release`.

**2026-11-14 update:** **v1.9.1** is a localization and quality-of-life release. It adds
Brazilian Portuguese, French, Spanish, Italian, Japanese, Korean, and Simplified Chinese
to the English/German UI catalogs (`--locale`, `--list-locales`, Settings > General > UI
language), fixes switching the desktop back to English without a restart, and adds the
two-step Escape pause/quit shortcut. The full v1.9.0 feature set remains: external
metadata and media, physical optical-drive boot, Windows DirectInput/HID gamepads, the
controller preview, auto-crop, display-sleep inhibition, Lua input scripting, the input
timeline, and best-effort 4DO `.4dosav` import/export. The suite is **2,259 tests**;
build, format, and InspectCode are clean. SYS-21 and SYS-23 keep their `Done when` lines
open pending physical hardware evidence. See [Localization.md](Localization.md),
[Metadata.md](Metadata.md), [Tas.md](Tas.md) §8-9, [SaveStates.md](SaveStates.md) §9,
and [TestCorpus.md](TestCorpus.md) 1.1bw, 1.1bx, 1.1bz, and 1.1cd.

**Snapshot:** 2026-11-17, v1.11.0: Linux x64 is validated end to end on bare-metal hardware (hardware OpenGL 4.2, Vulkan X11 probe and desktop shell, 600 s real-device audio with 0 drops/underruns, evdev gamepads, display sleep, full acceptance, clean exit); the opt-in true 2× CEL path, Jurassic Park Interactive
reaches the in-park Raptor Maze and SECURITY BREACH outcome with a default-CEL-pacing
residual; CDI and NRG disc images, the Windows OpenAL bundle
fix, RetroAchievements with a managed rcheevos engine,
save-state hit persistence, durable offline unlocks, hardcore rules, leaderboards, rich
presence, and OSD notifications; the `FiveDo` desktop apphost; deferred startup work and
ReadyToRun release bundles; nine UI catalogs with reversible locale switching,
two-step Escape pause/quit, external metadata and media, physical optical-drive boot,
Windows DirectInput/HID gamepads, controller preview, auto-crop, display-sleep
inhibition, Lua input scripting, the input timeline, and 4DO state interop.
The underlying v1.8.0 snapshot remains: MMPX 2x and bicubic filters, wider aspect
presets, the .NET 11 / C# 15 toolchain, the CHD startup-stutter fix and dependency
refresh; the native platform runtime smoke, CD subcode, disc
conversion, focus/void/pixel-format work, the two Tier C gameplay-depth increments, and
the 1.6.0 compatibility-completion release; the TEST-07
audio-gating, scripted-input, and nightly
baseline update; the 1.5.0 GPU-HQx, video-clean VDLP, field-boundary, and
CD-parity update; the 1.4.0 cover-art, latency, unsigned-disc, exact-clock,
and differential-harness update; the TEST-07 oracle increment, the VID-12 latency update,
the PERF-02 AOT/trimming audit, the 1.3.0 field-handling update, the 1.2.2
performance/library update, the 1.2.1 arm64 packaging update, the 1.2.0
feature/accuracy update, the rewind-capture stall fix, SYS-12 SWI HLE, SYS-17
game-database expansion, SYS-08 clock validation, the third PERF-08 JIT pass, the ENG-02
interpreter hot-path increment, and the PERF-05 high-resolution VDLP copy: Direct3D 11,
startup release checks, the fuzz harness, per-edge overscan/void border, screenshot
gallery filters, FIQ/DMA/DSP telemetry, flat CHD/raw ISO support, and display-refresh
control. The earlier first full-collection sweep, performance, timing-option, BIOS,
NVRAM, rewind, cheat, TAS, disc-browser, input, OSD/quick-menu, library, CD-DA,
multi-disc, portable, PAL/NTSC, diagnostics, save-state, overscan, and fix-parity
passes are retained below. Road Rash's
100k-state race now measures
4.20 ms/frame total (p99 7.40 ms),
Debug core builds are optimized, and Auto presentation frame skip is the default.
**594 discs tested: 593 Loadable/Rendered, 1 Loadable/No video (known boot failure), 0 Not Loadable.**

**Corpus update (2026-11-08):** the current collection is **1,371 images → 414 unique
titles / 378 game test set**. Under the compatibility plan's binary beyond-title rule,
**375 of 378 games are Playable** — the full 147-title peer gap is verified, the
comparison has **0 `Behind` and 0 `Regression` rows**, and 5DO is a strict superset of
the **252-title peer union** (after the evidenced *Return Fire – Maps O' Death* data-disc
invalidation). The original **≥254** numeric target is exceeded. The two `Title-only`
rows and one `No video` row are non-retail test images (`Joypad Test v1.0`,
*Return Fire – Maps O' Death* data disc, and `LameNES Emulator`). Per-title BIOS, disc
hash, capture frame, screenshot, and failure reason live in
[`BeyondTitleEvidence.md`](BeyondTitleEvidence.md) and
`docs/data/beyond-title-results.csv`. The unique-title 5DO/4DO/Phoenix comparison is in
[`CompatibilityComparison.md`](CompatibilityComparison.md); the 594-disc legacy sweep
above remains the per-image baseline.

**1.6.0 release update (2026-11-08):** **v1.6.0** completes the compatibility plan on the
current corpus: **1,371 images / 414 unique titles / 378 game-test-set titles**, with
**375 Playable**, **0 `Behind`/`Regression`**, and a strict superset of the 252-title peer
union. The release publishes the authoritative 5DO/4DO/Phoenix matrix and the
beyond-title ledger, tightens CD poll reset, guest drive speed, SESSION INFO, TOC
Q-position, and volume-header readable-disc behavior, and moves the CD-ROM save-state
section to version 5. Settings and NVRAM persistence now fall back to a direct copy when
an atomic replace is denied, preserve newer-schema settings as
`settings.json.v<version>.bak`, and survive null/short `Players` arrays; the desktop adds
tooltip coverage and logs expected fallback paths. The suite is **2,061 tests**; build,
format, and InspectCode are clean; `verify-beyond-title-ledger.py` and
`generate-compat-comparison.py --check` pass; Road Rash boots for 600 frames in both the
compatible and accuracy profiles. The six framework-dependent bundles and
`SHA256SUMS.txt` are published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.6.0>. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42552318341> passed all six bundle
jobs and the publish job; CI run
<https://github.com/purelogiccode/5DO/actions/runs/42552303788> passed every required
job; docs run
<https://github.com/purelogiccode/5DO/actions/runs/42552303805> deployed Pages and the
wiki. All published checksums verify. See
[`WhatsNew.md`](WhatsNew.md) and [`ReleaseNotes-1.6.0.md`](ReleaseNotes-1.6.0.md).

**1.7.0 release update (2026-11-10):** **v1.7.0** publishes six bundles with macOS as a
drag-to-Applications DMG. The CD-ROM accepts Opera's 2449-byte wire size and packs Q
into the READ_DATA subcode block; the CD-ROM state section moves to v6 while v1-5 states
remain loadable. `disc convert` writes data-only ISO/BIN/CUE/flat CHD and verifies the
game ID, and CHD conversion no longer collides with a same-stem source BIN. The desktop
adds focus-loss pausing, selectable void patterns, and pointer input scripts; macOS
gains a native IOKit HID gamepad source. The suite is **2,124 tests**; build, format,
and InspectCode are clean. CI run
<https://github.com/purelogiccode/5DO/actions/runs/42877150967> passed every required
job, including the six bundle jobs and both native macOS DMG builds; native runtime
smoke run <https://github.com/purelogiccode/5DO/actions/runs/42876332881> passed macOS
x64/arm64, Linux arm64, and Windows arm64. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42878260384> published the six assets
at <https://github.com/purelogiccode/5DO/releases/tag/v1.7.0>; all published checksums
verify. See [`WhatsNew.md`](WhatsNew.md) and
[`ReleaseNotes-1.7.0.md`](ReleaseNotes-1.7.0.md).

**1.9.1 release update (2026-11-14):** **v1.9.1** publishes six framework-dependent
single-file bundles (macOS as DMG) and `SHA256SUMS.txt` at
<https://github.com/purelogiccode/5DO/releases/tag/v1.9.1>. CI run
<https://github.com/purelogiccode/5DO/actions/runs/43565870407> passed every required
job: Windows/Linux/macOS build-test, the RID-agnostic lock check, format, benchmark
gates, trimmed CLI/frontend publishes, both NativeAOT apphosts, all six bundle jobs, and
the aggregate gate. The Windows build-test job was rerun once after an ILLink native
crash during the trimmed publish; the rerun passed. Release run
<https://github.com/purelogiccode/5DO/actions/runs/43566963957> built and published all
six assets; every checksum verifies, and the assets are copied into
`src/FiveDo.Frontend.Avalonia/bin/Release`. Docs run
<https://github.com/purelogiccode/5DO/actions/runs/43565870395> deployed Pages and the
wiki. The suite is **2,259 tests**; build, format, and InspectCode are clean. See
[`WhatsNew.md`](WhatsNew.md) and [`ReleaseNotes-1.9.1.md`](ReleaseNotes-1.9.1.md).

**1.9.0 release update (2026-11-14):** **v1.9.0** publishes six framework-dependent
single-file bundles (macOS as DMG) and `SHA256SUMS.txt` at
<https://github.com/purelogiccode/5DO/releases/tag/v1.9.0>. CI run
<https://github.com/purelogiccode/5DO/actions/runs/43558322127> passed every required
job: Windows/Linux/macOS build-test, the RID-agnostic lock check, format, benchmark
gates, trimmed CLI/frontend publishes, both NativeAOT apphosts, all six bundle jobs,
and the aggregate gate. Release run
<https://github.com/purelogiccode/5DO/actions/runs/43558324543> built and published all
six assets; every checksum verifies, and the assets and internal macOS tarballs are
copied into `src/FiveDo.Frontend.Avalonia/bin/Release`. The suite is **2,252 tests**;
the three analysis-only `GameLibraryWindow` symbol errors were resolved with a type
alias in 1.9.1. Docs for `WhatsNew`, the 1.9.0 release notes,
and `wiki/Release-1.9.0` return HTTP 200. See
[`WhatsNew.md`](WhatsNew.md) and [`ReleaseNotes-1.9.0.md`](ReleaseNotes-1.9.0.md).

**1.8.0 release update (2026-11-11):** **v1.8.0** adds MMPX 2x and Catmull-Rom
bicubic filtering to OpenGL/GLES, Direct3D 11, Vulkan, and the CPU fallback, with opt-in
GPU parity coverage against `MmpxFilter` and `FrameScaler`. Settings > Video > Aspect
adds 5:4, 16:9, 16:10, and 21:9 presets. The repository moves to .NET SDK **11.0.100**,
`net11.0`, and **C# 15**, with DependencyInjection 11.0.0 and the SDK's implicit
ILLink.Tasks 11.0.0. The
release also fixes the invalid `global.json` pin, restores the reference MMPX luma
tie-breaks (including a triangle-tip regression test), and matches OpenGL's MMPX
finishing sampler to Direct3D 11/Vulkan. The suite is **2,158 tests**; build, format,
and InspectCode are clean. CI run
<https://github.com/purelogiccode/5DO/actions/runs/42958451196> passed every required
job; platform runtime run
<https://github.com/purelogiccode/5DO/actions/runs/42958451118> passed macOS
x64/arm64, Linux arm64, and Windows arm64. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42959751665> published six
framework-dependent bundles (macOS as DMG) and `SHA256SUMS.txt` at
<https://github.com/purelogiccode/5DO/releases/tag/v1.8.0>; all six checksums verify.
See [`WhatsNew.md`](WhatsNew.md) and
[`ReleaseNotes-1.8.0.md`](ReleaseNotes-1.8.0.md).

**1.7.1 release update (2026-11-10):** **v1.7.1** removes the desktop startup stutter
and audio underruns caused by the host disc prefetcher wrapping CHDSharp's own
read-ahead. CHD images now use the CHDSharp reader directly, and the remaining sector
prefetcher runs below normal priority and yields between read-ahead sectors. The
release also updates Avalonia to 12.1.6, System.CommandLine to 3.0.0, and
Microsoft.NET.Test.Sdk to 18.11.1. A 20-second desktop Road Rash run reports **0 audio
underruns** and no skipped frames. The suite is **2,126 tests**; build, format, and
InspectCode are clean. Fresh Windows/Linux/macOS x64/arm64 bundles are built from the
`v1.7.1` tag. The release run
<https://github.com/purelogiccode/5DO/actions/runs/42898378598> built and published all
six bundles; docs run
<https://github.com/purelogiccode/5DO/actions/runs/42897688864> deployed Pages and the
wiki. See [`WhatsNew.md`](WhatsNew.md) and
[`ReleaseNotes-1.7.1.md`](ReleaseNotes-1.7.1.md).

**Tier C gameplay-depth update (2026-11-09):** the first Tier C increment re-ran the
25 titles still graded `Playable` after Tier B with title-specific start and gameplay
scripts. Eight were promoted to `Good`: **Olympic Soccer** (full match to FULL-TIME),
**John Madden Football** (live football gameplay through 36,000 frames), **Crime
Patrol** and **Mad Dog II** (live-action lightgun scenes at 48,000), **Shootout at Old
Tucson** (lightgun scene with the ammo HUD), **Guardian War** (world-map gameplay
through 36,000 frames), **Hell - A Cyberpunk Thriller** (3D adventure gameplay at
44,000/48,000 frames), and **Slayer** (first-person dungeon gameplay at 80,000/90,000
frames). A fixed-script run withdrew the earlier PGA Tour 96 promotion; the Player List
stays put, so it remains `Playable`.
The CSV ledger moved to **101 rows: 84 `Good`, 17 `Playable`**. The 378-title playable
count stays **375** because Tier C changes the quality grade, not the binary
compatibility result. The 25-title baseline completed 25/25. See
[`TestCorpus.md`](TestCorpus.md) 1.1bm and [`CompatibilitySweep.md`](CompatibilitySweep.md)
4.3.

**Tier C second increment (2026-11-09):** input scripts now support pointer devices
(`@mouse`, `@lightgun`, `@arcadegun`, `@trackball`) with one-frame relative motion and
persistent buttons; `ScriptedPbusSource` applies them per configured port and the parser
has six new tests. Four more titles were promoted to `Good`: **Carrier - Fortress at
Sea** (trap menu to a live trap approach and WAVE OFF at 14,000 frames), **Family Feud**
(home questions and letter keyboard at 9,000/12,000), **Insect War** (first-person HUD
and WARNING state at 58,000/62,000), and **N. O. B.** (VITALITY HUD gameplay at 9,000).
At that point the ledger was **101 rows: 88 `Good`, 13 `Playable`**; seven of the
remaining rows were media/reference/add-on discs, and six were game leads (BladeForce,
DeathKeep, Jurassic Park, Kingdom, Off-world Interceptor, PGA Tour 96). Differential
triage closed the three
suspected core defects as content/script limits: DeathKeep and Kingdom show the same
black screen/title screen on OperaCore, and BladeForce reaches the same Main Options
menu on both engines. The quality ledger gate (`tools/verify-playability-grades.py` and
`compat-baseline.yml`) now fails on a `Good` -> `Playable` drop. The suite is **2,068
tests**; build, format, and InspectCode are clean. See [`TestCorpus.md`](TestCorpus.md)
1.1bo and [`CliReference.md`](CliReference.md#input-and-automation-options).

**P0 quality closure (2026-11-09):** the remaining menu/cutscene leads were converted
into mission, terminal, and round evidence. **BladeForce** now types a name, selects
START GAME, and reaches the Supply Depot first-person mission at 70,000/80,000/90,000
frames. **Off-world Interceptor** launches its first mission from the loadout at 12,000
frames. **PGA Tour 96** reaches Hole 1 at 64,000 frames. **Jurassic Park Interactive**
opens the Security Systems / Security Tracking terminal, and **Alone in the Dark** adds
room exploration plus the Actions menu (`Fight`/`Open/Search`/`Close`/`Push`). The three
promotions move the ledger to **101 rows: 91 `Good`, 10 `Playable`**. AUD-01's
10-minute real-time soak passes on Windows with both the null and real OpenAL devices
and on the Linux VM with the null driver: 0 dropped samples and 0 underruns on all three
runs. The Linux VM has no PCM endpoint, so a Linux real-device soak remains a hardware
limitation. Road Rash's full 5.5-mile race is still open; a fresh 150,000-frame autopilot
run ends with the rider off the bike. See [`TestCorpus.md`](TestCorpus.md) 1.1bp and
[`Audio.md`](Audio.md) §9.

**Platform and feature update (2026-11-10):** native runtime smoke now passes on
macOS x64 (`macos-15-intel`), macOS arm64 (`macos-latest`), Linux arm64
(`ubuntu-24.04-arm`), and Windows arm64 (`windows-11-arm`): each target runs the CLI
`selftest` and the frontend `--smoke-test` (`FRONTEND SMOKE OK 1.6.0`). Run
<https://github.com/purelogiccode/5DO/actions/runs/42770783778> passed all four jobs,
closing PLAT-03/PLAT-04. The CD-ROM wire size now accepts Opera's 2449-byte maximum,
so a 2352-byte digital-audio block plus 96-byte subcode is valid; READ_DATA exposes
the Q subchannel and the FZ-10 BIOS CD player lists the A&M Music Sampler's first
track (transport playback remains a documented BIOS limit). `disc convert` writes
ISO/BIN/CUE/flat CHD and verifies the game ID; Lemmings (USA) round-trips with
`discHash 0be38b22987b40b5d48f70a167656049` and a 600-frame hash of `675291253`.
The desktop adds opt-in focus-loss pausing with input release, selectable void
patterns (solid/checker/grid/scanline), and 0RGB1555/RGB565/XRGB8888 frame conversion
helpers. The suite is **2,119 tests**; build, format, and InspectCode are clean. See
[`TestCorpus.md`](TestCorpus.md) 1.1br/1.1bs, [`Compatibility.md`](Compatibility.md)
§10.3, and [`KnownIssues.md`](KnownIssues.md).

**1.5.0 release update (2026-11-08):** **v1.5.0** moves HQx 2x/3x/4x to shared
rule-table GPU passes in OpenGL/GLES, Direct3D 11, and Vulkan, with the CPU filter kept
as the reference oracle. The 3DO background is now 24-bit end to end, a zero VDL
display-list word is a stop, `RunFrame` ends on the reference field boundary, and CD
poll/FIQ/READ_DATA behavior is closer to Opera. The 50-title panel is **50/50 frame
hashes at 60/120/300 with 0 errors**: the same-frame instruction delta is 11-20 at
frame 60 and 23-2,223 at frame 120, and the remaining behavioral divergence is CD
command cadence. The command-50 boot handoff fell from +207,707 cycles / +86,511
instructions to -818 / -376. The suite is **2,033 tests**; build, format, and
InspectCode are clean. Release run
<https://github.com/purelogiccode/5DO/actions/runs/42445302517> passed all six bundle
jobs and the publish job; CI run
<https://github.com/purelogiccode/5DO/actions/runs/42445297515> passed every required
job. The six bundles and `SHA256SUMS.txt` are attached at
<https://github.com/purelogiccode/5DO/releases/tag/v1.5.0>, all checksums verify, and
local copies are kept in `src/FiveDo.Frontend.Avalonia/bin/Release`. See
[`WhatsNew.md`](WhatsNew.md), [`ReleaseNotes-1.5.0.md`](ReleaseNotes-1.5.0.md), and
[`DifferentialHarness.md`](DifferentialHarness.md).

**TEST-07 audio and scripted-input update (2026-11-08):** the differential harness now
gates the cumulative audio hash when both engines have produced the same sample count.
The 50-title panel matches **150/150 audio fields** at frames 60/120/300, so all 50
titles match video, samples, and audio at the panel checkpoints. The one measured
long-run limit is the reference clock: 5DO follows the exact `60000/1001` NTSC rate
(59.940059940 Hz), while OperaCore's 16.16 constant is `3,928,227/65,536`
(59.939987183 Hz), **1.214 ppm lower**. On Lemmings the sample counts match through
1,000 frames, then the reference is **+1 at 2,000** and **+2 at 3,000** frames
(2.68 predicted); the harness labels that as an alignment state instead of a waveform
divergence. A difference at an aligned count is still a hard failure. Scripted input is
wired into both engines: the five-title, 900-frame `samples5-input` panel matches
**5/5 frame hashes at 60/300/600**, the generic menu script changes guest output at
600/900, and the no-input control confirms the matches are not from an ignored script.
The nightly workflow now runs the showcase fresh, uploads `report.html`, and fails only
when a 5DO field changes against the checked-in baseline. The suite is **2,048 tests**;
build, format, and InspectCode are clean. See
[`DifferentialHarness.md`](DifferentialHarness.md) §4.1/§4.2/§6 and
[`TestCorpus.md`](TestCorpus.md) 1.1bd.

**CORE-04 CD register and metadata parity update (2026-11-08):** the debug reference core
now records every CD command byte, poll write/read, status read, and data read with
cycle and PC, and the harness writes matched `fivedo.cdreg.txt`/`opera.cdreg.txt` traces
with `--cd-register-trace`. On 20th Century Video Almanac the two engines' command
packets, status bytes, and 2048-byte sector data are identical through command 105, the
last command before the sequence divergence; the remaining difference is guest sleep
time, not the CD register contract. The trace exposed and fixed the XBUS 2x status bit
(`MEI_CDROM_DOUBLE_SPEED` is `0x80`, and the drive starts at double speed), the guest
speed cap in `cdrom_effective_speed`, the readable-disc limit (3DO volume header
`volume_block_count`, not the physical image size), and the TOC Q-position bit plus the
8-byte READ_SESSION_INFO payload. The confirmation panel
(`test110-panel50-metadata`, 50 titles, 300 frames) captures **50/50 with 0 errors**,
**0 frame-hash divergences**, **150/150 aligned audio fields**, **39 phase-only**
titles, and the same **11 CD-only first behavioral divergences** at f=300. The CD-ROM
save-state section is version 5. The suite is **2,048 tests**; build and format are
clean. See [`TestCorpus.md`](TestCorpus.md) 1.1bf and
[`PortingNotes.md`](PortingNotes.md) 12.19.

**1.4.0 release update (2026-11-07):** **v1.4.0** adds online cover art (473 unique
WebP covers, 645/653 game IDs mapped), VID-12 run-ahead/frame-delay/VRR/audio-sync
controls, SYS-06 unsigned-disc support, the exact 60000/1001 NTSC field clock and rational
VDL accumulator, VDLP blanking/background fixes, and the live OpenAL device-switching and
test-tone fixes. The suite is **1,905 tests**; build, format, and InspectCode are clean.
The 50-title differential panel remains the current accuracy evidence: 32/50 titles match
every frame hash at 60/120/300, every sample delta is within 9, and Space Ace,
Wolfenstein 3D, Alone in the Dark, and Lemmings match Opera at 3,000 frames while Road
Rash still diverges. See [`WhatsNew.md`](WhatsNew.md),
[`ReleaseNotes-1.4.0.md`](ReleaseNotes-1.4.0.md), [`DifferentialHarness.md`](DifferentialHarness.md),
and [`CoverArt.md`](CoverArt.md).

**Post-1.4.0 differential update (2026-11-07):** the 50-title panel is now
**video-clean**: **50/50 titles match every frame hash** at frames 60/120/300 (up
from 32/50), with **0 errors** in `test55-panel50-first-sector`. **48/50 first PC
divergences instruction-align to Opera frame 59** and **2/50 to frame 119** (the
f=60 checkpoint itself matched on those two), at a residual of only -55..-415
instructions (same-frame delta ~89,000). The
fixes are a 24-bit VDL background (the old 5-bit narrowing rendered near-black as
pure black) and treating a zero VDL display-list word as a stop that retains the
previous bitmap, matching `opera_vdlp.c`. The only remaining behavioral
divergence is CD command cadence: 36 titles first differ by 1-2 commands at
frame 120 (in-flight at the checkpoint) and 13 at frame 300 (seven FMV titles at
+3/+4, six at -1/-2). The 5-title panel matches **5/5** video at 300 frames; at
3,000 frames Alone, Lemmings, Space Ace, and Wolfenstein match and Road Rash
still diverges. The harness now records `pcPhaseFrame`/`pcPhaseInstructionDelta`
and per-command PC/retired-instruction stamps. Those stamps showed the earlier
command-50 handoff was **+207,707 cycles / +86,511 instructions** of extra guest
work; matching the oracle's first-sector READ_DATA status publication reduced it
to **-818 / -376**, and the f=300 command-104 cycle delta fell from **+324,027**
to **+111,990**. READ_DATA deadlines now accumulate from the previous deadline
instead of re-basing to `now`. The residual universal frame-60 one-field phase is
separate from that handoff and remains open. The harness also stages ISOs
atomically, retries a transient CHD read once, and reports a per-title capture
failure as an `Error` instead of aborting the run. The suite is **1,922 tests**;
build, format, and InspectCode are clean. See
[`DifferentialHarness.md`](DifferentialHarness.md) and
[`TestCorpus.md`](TestCorpus.md) 1.1az.

**CD cadence triage (2026-11-07):** the harness now classifies every final CD
transcript and reports the largest cumulative cycle-delta step with its
command-issue PC and retired-instruction jump (`cdCadence`, plus a `CD cadence`
column in the reports). The 50-title panel resolves to **39 identical command
streams, 8 prefix-only, and 3 one-command READ_ID poll shifts** (Chiki Chiki,
Dragon's Lair, Virtual Horror). **24/50 titles have a significant step and all 24
issue the step command from the same PC on both engines** (17 at 60-100k cycles,
six at 20-60k, one >=100k). The fresh five-title run confirms Alone 361/361
(`+379` cycles at the last command), Road Rash 395/395 (`+468`), Wolfenstein
341/341 (`-345`), Lemmings 109/105 with one `+22,834` step at command 51
(`+8,242` instructions), and Space Ace 168/168 with one `+80,567` step at
command 164 (`+32,330` instructions). The residual count difference is guest
wait-loop cadence, not a divergent CD command table. The new `--cd-register-trace`
shows **no CD register access during the 237,794-cycle Space Ace wait or the
2.17M-cycle Lemmings wait**, so the guest is waiting on a non-CD source
(timer/VBlank); this moves the next measurement to CORE-07. The suite is
**1,922 tests**. See [`DifferentialHarness.md`](DifferentialHarness.md) 8 and
[`TestCorpus.md`](TestCorpus.md) 1.1bb.

**Frame-boundary parity (2026-11-07):** the universal frame-60 PC/CPSR phase had
two causes. The harness finalized each Opera checkpoint at the next video
callback, so ARM/CD state belonged to the following field while the audio hash
was already correct; the harness now captures ARM/CD state in the checkpoint's
own callback. `FiveDoMachine.RunFrame` also stopped on the rounded
208,541-cycle budget while Opera stops on the 263-line scanline boundary, so the
discarded last-batch overshoot accumulated about 3.3 instructions per field;
`RunFrame` now ends on `Quartz.VdlFieldIndex`. On the 50-title confirmation
panel the same-frame retired-instruction delta is **11-20** at f=60 (average
15.3) and **23-2,223** at f=120, with **50/50 frame hashes** and **0 errors**.
The CD count matches on **50/50 titles at f=60 and f=120** (down from 36 first
divergences at f=120), and the first behavioral divergence moved to **11 titles
at f=300** (8 count, 3 sequence); the other 39 titles are phase-only. This
supersedes the "5DO frame 60 aligns with Opera frame 59" interpretation. The
3,000-frame showcase keeps **4/5 video matches** (Road Rash is the only
frame-hash divergence) with CD counts Lemmings 365/352, Gex 548/547, Road Rash
851/836, Wolfenstein 862/861, and Alone 932/932. The suite is **1,923 tests**.
See [`DifferentialHarness.md`](DifferentialHarness.md) 8 and
[`TestCorpus.md`](TestCorpus.md) 1.1bc.

Release workflow
<https://github.com/purelogiccode/5DO/actions/runs/42399055155> passed all six bundle
jobs and published six versioned archives plus `SHA256SUMS.txt`; all six checksums verify.
CI run <https://github.com/purelogiccode/5DO/actions/runs/42398646569> passed every
required job, including the six bundle builds, NativeAOT CLI/frontend, format, tests, and
benchmark gates. The release is published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.4.0>. Local bundles are kept under
`src/FiveDo.Frontend.Avalonia/bin/Release`.

**TEST-07 exact-clock and 50-title panel update (2026-11-06):** the earlier claim that the frame-60 CPSR difference was the FIQ-disable bit was wrong: `0x600000d3` and `0x800000d3` differ in the N/Z/C condition flags. The real defect was the field clock. 5DO now runs NTSC at the exact 60000/1001 field rate (208,541 cycles at 12.5 MHz) and uses a rational VDL accumulator, removing both the 1 ms/60-field drift and the per-batch 8.24 rounding that ran the scanline clock ~0.09% fast. Sample counts now track Opera within 1-8 samples at 300 frames (all 50 panel titles are within 9 at frame 300), and **32/50 titles match every frame hash** at frames 60/120/300. The 50-title panel captured 50/50 with 0 errors; CD counts match 49/50 at frame 60. The absolute instruction count carries a fixed ~89,000 startup offset, but the per-frame retirement rate matches within 0.1% on 4/5 titles, so PC/CPSR is a phase sample rather than a cycle-model gap. The new CD cycle stamps localize the remaining CD cadence difference. At 3,000 frames Alone, Lemmings, Space Ace, and Wolfenstein match Opera's hash; Road Rash still diverges. The suite is now **1,905 tests**. See [`DifferentialHarness.md`](DifferentialHarness.md) and [`TestCorpus.md`](TestCorpus.md) 1.1ax.
**TEST-07 oracle increment (2026-11-06, superseded by the exact-clock update above; the FIQ-bit and 59.94/60 Hz statements below are incorrect):** the differential harness now applies a
fixed seed to both engines (`--seed`, default 1488), compares ARM CPSR and CD
command counts/opcode hashes, and reports the **first divergence** in the console,
JSON, and Markdown. Two consecutive runs of the new five-title
`benchmarks/diff/samples5.json` panel produced identical checkpoint arrays for both
engines. The first divergence is the ARM PC at frame 60 on all five titles;
Lemmings' CD commands match 9/9 at frame 60 and 50/50 at frame 120, then 108/105 at
frame 300, and its CPSR differs at frame 60 by the FIQ-disable bit. The cumulative
audio hash is recorded but not gated because the 59.94/60 Hz field-rate offset
leaves a systematic sample-count delta. The `panel` command generated
`benchmarks/diff/panel50.json` (50 evenly spaced titles from the 594-disc
manifest). A 3,000-frame follow-up separates PC drift from visible divergence:
Lemmings matches the frame hash at every checkpoint, while Road Rash and Space
Ace show different animation frames/scenes and no ±60-frame hash match. The full
CD command sequence localizes the first command divergence to READ_DATA (`0x10`)
versus READ_ID (`0x83`) transitions, giving CORE-04 a concrete next target. The
suite is **1,881 tests**. See
[`DifferentialHarness.md`](DifferentialHarness.md) 8 and
[`TestCorpus.md`](TestCorpus.md) 1.1aw.

**Performance increment (2026-11-05):** the default rewind history no longer stalls the
emulation thread. Keyframe capture on Lemmings fell from **139.3 ms** to **7.4-9.2 ms**
(one 14.1 ms outlier), per-1,200-frame allocations fell from ~1.5 GB to 16 MB with no
gen0/gen2 collections, and the frame loop measured p50 **1.34 ms** / p99 **6.85 ms** /
max **14.76 ms** with the mailbox and rewind active. The memory save-state section
(version 4) now stores only the live RAM/VRAM range, cutting a stock-layout uncompressed
state from ~26 MB to ~10 MB, and the frontend requests the host-side prefetch cache
for uncompressed images (CHD uses CHDSharp's own hunk read-ahead).
See [`Performance.md`](Performance.md) 8.5. The suite is **1,866 tests**.

**CPU and high-res increment (2026-11-06):** the third PERF-08 pass gives compiled blocks a
shared PC/CPSR/cycle/instruction context, compiles register-specified shifts, and replaces
the compile-hit dictionary with a direct-mapped table. Lemmings improved from 1.60-1.66 to
**1.23-1.37 ms/frame** (about 20% faster than the old JIT) but is still ~1.25x slower than
the 1.00-1.02 ms/frame interpreter, so the interpreter remains the default; the synthetic
loop is 0.89-0.99 ms/frame versus 1.20-1.30 for the interpreter. The second pass had replaced
per-instruction JIT verification with exact RAM-write block invalidation and fixed compiled
R15 operands. ENG-02 added direct RAM/ROM instruction fetch, inline LSL#0/rotated-immediate
operand paths, a flag-preserving ALU dispatch, and population-count LDM/STM iteration; an
interleaved A/B measured synthetic **1.58→1.33 ms/frame** and Lemmings **1.43→1.13 ms/frame**
with unchanged hashes. PERF-05 now reads the raw VRAM word span for high-resolution scanline
DMA, cutting Lemmings high-res from **2.42-2.47** to **1.30-1.43 ms/frame** (about 1.8x) with
an identical high-res hash. See [`Performance.md`](Performance.md) 8.4 and 8.6.

**Latency and AOT increment (2026-11-06):** VID-12 adds 0-2 frame run-ahead, 0-8 frame
presentation delay, VRR-friendly compositor presentation, a −250…250 ms audio sync offset,
per-game profile fields, and software input-latency telemetry. Run-ahead uses a reusable
uncompressed `MachineStateSnapshot`; `bench --latency` measures a 9,348,438-byte snapshot,
~3.6-3.9 ms capture, and ~1.6-2.0 ms restore. AOT CLI/frontend win-x64 builds start, render,
play audio, and pass smoke tests; the AOT frontend booted Lemmings with run-ahead 1, frame
delay 1, VRR, and audio sync 40 ms. PERF-02 is complete; VID-12 and SYS-12 are marked done.
Full optical input-to-photon measurement and real adaptive-sync hardware confirmation remain
documented validation caveats. See [`Performance.md`](Performance.md) 8.7 and
[`Trimming.md`](Trimming.md).

**Release status (2026-11-02):** **v1.0.0 is released** as a documented-compatibility
release. The version is bumped, the source is tagged `v1.0.0`, and one unified bundle
per system (Windows, Linux, macOS x64) plus checksums is attached to the
[GitHub release](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0). Linux x64
validation passed on Linux Mint 22.3: the full test suite
(**1,473/1,473**), `selftest`, `bench`, a 3,000-frame Wolfenstein boot, non-silent
audio, and the frontend `--smoke-test`. The GL probe timed out under the VM's
llvmpipe/SSH session, so Linux GPU presentation is a **preview/best-effort** limitation.
Legal/attribution sign-off is recorded under the FreeDO modified GNU LGPL as free,
open-source donationware (non-commercial; voluntary donations only). M13 loadability is
complete; M13.5/M13.6 visual/high-resolution review and M14 high-resolution CEL research
are **post-1.0**.

**Field-handling update (2026-11-06):** **v1.3.0** was the previous release,
published at <https://github.com/purelogiccode/5DO/releases/tag/v1.3.0>. It adds
`InterlaceProcessor` with auto 480i detection, bob, weave, and blend modes; per-game and
`--deinterlace` selection; interlace-aware viewport geometry; screenshot/GIF capture of
the presented frame; and `run --json` mode/detection fields. The suite is **1,866 tests**.
Bob-vs-auto hashes match on five local titles (3,000 frames for Road Rash, Space Ace, and
Alone in the Dark; 300 for Wolfenstein 3D and Lemmings), forced weave produces a 320x480
frame, and high-resolution output remains 640x480. The CLI and core default to bob; the
desktop defaults to auto. No local title is confirmed 480i, so synthetic complementary
field tests are the current 480i evidence.

Release workflow <https://github.com/purelogiccode/5DO/actions/runs/42235476257> passed
all six bundle jobs and published six versioned archives plus `SHA256SUMS.txt`; the docs
workflow <https://github.com/purelogiccode/5DO/actions/runs/42235456021> deployed the
Pages site and wiki. All release checksums verify. Local bundles and an archive of every
historical release asset are kept under
`src/FiveDo.Frontend.Avalonia/bin/Release`.

**Performance and library update (2026-11-06):** **v1.2.2** was the previous release,
published at <https://github.com/purelogiccode/5DO/releases/tag/v1.2.2>. It adds the
interpreter, high-resolution VDLP, and rewind performance work; the opt-in managed JIT;
the expanded game database and library health/DAT verification; eight Opera memory
layouts; eight PBUS ports; opt-in Portfolio OS SWI HLE; and audio-only CHD cataloging.
The suite is **1,815 tests**. Release workflow
<https://github.com/purelogiccode/5DO/actions/runs/42169040644> passed all six bundle
jobs and published six versioned archives plus `SHA256SUMS.txt`; all six checksums
verify. The docs workflow deployed the Pages site and published the wiki. A post-1.2.1
audit fixed eight bugs: the JIT register-shift PC delta, stale ROM-bank and soft-reset
blocks, FIQ entry telemetry, the prefetch track-sector length, legacy PBUS slots 7-8,
the pointer-exit port bound, and the ARM clock range.

**Arm64 packaging update (2026-11-03):** **v1.2.1** was the arm64 packaging release,
published at
<https://github.com/purelogiccode/5DO/releases/tag/v1.2.1>. It added
framework-dependent Windows arm64, Linux arm64, and macOS arm64 bundles alongside the
x64 bundles, for six versioned archives plus `SHA256SUMS.txt`. CI verifies each arm64
apphost's PE/ELF/Mach-O machine type; the x64 bundles keep their runtime smoke tests.
No emulator behavior changed. Release workflow
<https://github.com/purelogiccode/5DO/actions/runs/41583317988> passed all six bundle
jobs; every checksum was verified after publication.

**Feature update (2026-11-03):** **v1.2.0** added the optional
Windows Direct3D 11 presenter, startup GitHub release checks, deterministic fuzzing,
per-edge overscan/void border, screenshot gallery filters/compare/export, display-refresh
control, flat 2048-byte CHD and raw `.iso` detection, FIQ request-to-vector latency
telemetry, DSP per-tick statistics, and the CLIO/DIPIR/FIFO/MCTL/XBUS audit fixes. The
1.2.0 suite was **1,597/1,597** passing. WebAssembly/browser, Android/iOS, and Raspberry Pi
were removed from the roadmap; desktop Windows/Linux/macOS remain supported. The three
versioned bundles and `SHA256SUMS.txt` are attached to the release; every checksum was
verified after publication.

**Maintenance update (2026-11-02):** **v1.1.0** remains available at
<https://github.com/purelogiccode/5DO/releases/tag/v1.1.0>. It adds game
library deduplication by 3DO game ID, moves the OpenAL license text to
`docs/OpenAL-Soft-COPYING.txt`, removes the frame/PC/cycle counter from the desktop
status bar, and completes the Serilog catch-block coverage and launch-statistics
wiring. The 1.1.0 suite was **1,597/1,597** passing. The bundle layout is unchanged:
framework-dependent single-file apphosts, no bundled .NET runtime, no PDB/XML files.

**Latest work (2026-11-05):** Settings > Paths now edits game library folders directly
(type or paste a path, Browse, or Remove), and the scanner indexes cover images once per
folder instead of probing every cover extension for every disc. Cover art now decodes on
bounded background tasks, so the non-virtualizing grid does not block the list. On the local
2,732-image collection the cached listing fell from 2.8 s to 0.33 s, and a cold listing shows
all entries in 17 ms before background metadata hashing. The library now reports both counts
(for example `653 games · 1,366 images`) because discs are identified by the 4DO game ID
(MD5 of the first two data sectors); **Settings > Paths > Show duplicate disc images** lists
every renamed or GoodTools/Redump/No-Intro copy. PERF-08 added an opt-in managed IL JIT (`--cpu-backend jit`) for hot data-processing, load/store, and branch blocks. A third pass shares a PC/CPSR/cycle context across blocks, compiles register-specified shifts, and uses a direct-mapped compile-hit cache. It is bit-identical on the test corpus and a 500-frame CLI A/B matches Road Rash, Powers Kingdom, and Lemmings hashes/instructions/cycles, but Lemmings boot improved only to 1.23-1.37 ms/frame and is still ~1.25x slower than the interpreter, so PERF-08 remains Partial and the interpreter remains the default. SYS-17 expanded the game database from 518 to 653
records and added `gamedb stats|scan|export|merge`, `docs/GameDatabase.md`, and a
filename parser for Redump/GoodTools/No-Intro layouts. The schema now carries revision,
disc number, BIOS/BIOS2, fix mode, input devices, NVRAM, and cheat fields; fix, BIOS, and
input values are consumed at runtime. `gamedb stats --folder "J:\Panasonic 3DO"` reports
1,366 readable images, 653 unique IDs, and 100% metadata coverage with no errors
(the three exceptions are audio-only `[CD-Audio]` CHDs). SYS-08 added the five-title
0.5x-1x-2x-4x A/B in `Performance.md` §8.3 and kept the stock 12.5 MHz default; every
non-stock clock changed guest behavior. The suite is **1,866 tests**.

**SYS-12 (2026-11-04):** Portfolio OS SWI HLE is available behind `--swi-hle`,
Settings > Emulation, and per-game profiles. It handles Opera's 14 fixed-point math SWIs
on the host, records the option in save-state Options v9, and reports per-comment
coverage with `--swi-comments`. The host routines match `opera_fixedpoint_math.c`; a
real-BIOS 4x4 benchmark cuts 847,860 guest steps to 24,578. HLE is an opt-in performance
mode and skips the guest handler's cycles/register clobbers, so some titles change
behavior. Default-off hashes are unchanged. Input devices also support eight PBUS ports:
ports 7-8 use the Opera daisy chain, `--active-devices`/Settings caps the polled ports at
0-8, and the MADAM PBUS buffer matches Opera's 256 bytes (state section v4). The
hide-lightgun-crosshair option is also available.

**SYS-07 (2026-11-04):** all eight Opera DRAM/VRAM layouts
(2/4/8/14/15 MB DRAM and 1/2 MB VRAM) are selectable behind `--memory`, Settings > Emulation >
Memory, per-game profiles, CLI JSON, and save states. VRAM is decoded directly above
DRAM, VDLP/SPORT/CEL use the live layout, and MADAM RED SYSBITS reports the capacity to
the BIOS. A homebrew probe ROM uses 4 MB DRAM + 2 MB VRAM; retail titles remain on the
stock 2 MB/1 MB default because Opera documents expanded memory as homebrew-only. The
CPU-clock matrix (0.5x-4x) and `bench --cpu-clock`/`--memory` were also added; the suite
is now **1,866 tests**. See [`TestCorpus.md`](TestCorpus.md) 1.1al, 1.1an, 1.1ao, 1.1aq, 1.1ar, 1.1as, 1.1at, and
[`Performance.md`](Performance.md) 8.1.

**Latest work (2026-11-02):** the CLI and desktop shell check the public GitHub release
feed at startup (cached, opt-out) and offer the download page; `5do update` exposes the
same check. The optional `FiveDo.Video.Direct3D` Windows backend adds a FL11_0
two-stage presenter, runtime HLSL, a DXGI flip-model child swapchain,
`--gl-backend d3d11`, Settings selection, and `--d3d-probe`/`--d3d-window-probe`.
320x240 Accurate matches the CPU oracle (0 different pixels); 4K sharp-bilinear
measures present p99 0.23 ms and GPU p99 0.07 ms. OpenGL remains the default.

The 2026-11-02 hardening pass added `tools/FiveDo.Fuzz` (state, disc, CUE, Opera-FS,
CD command-stream, and ARM60 targets plus a nightly workflow), per-edge overscan and
per-game crop profiles with a configurable void border, and screenshot gallery filters,
side-by-side compare, export, and settings sidecars. The same pass added FIQ
request-to-vector latency telemetry and `--fiq-trace` request/entry lines, closed the
CLIO output-FIFO/DIPIR/MADAM-MCTL/XBUS poll audit gaps, and exposed DSP per-tick
statistics in `run --json`. The 1.2.0 suite was **1,597 tests**.

SYS-01 and AUD-05 now expose multi-track CUE/CHD tables and CD-DA: `ITrackedDiscSource`
carries native 2048/2336/2352-byte sectors, `CueDiscImage` handles multi-file/multi-track
`INDEX`/`PREGAP`/`POSTGAP` layouts, `ChdDiscImage` exposes audio tracks, and
`CdromDevice` implements multi-track READ TOC/lead-out, READ SUB-Q, MODE SET/SENSE,
PLAY_AUDIO position tracking, 2352-byte digital-audio reads, and guest inject. CD-DA is
mixed into the 44.1 kHz DSP output with persisted volume/mute; CD state v4 stores the
mode, audio position, volume, and mute. `disc tracks` reports every track, and SYS-02
adds an M3U/M3U8 playlist, playlist editor, drag-and-drop import, File > Swap Disc/F3,
guest-eject ODE auto-advance, and `run --playlist/--swap-at/--swap-disc`. SYS-03 adds
NTSC/PAL1/PAL2 timing: PAL runs at 50 Hz with 288 visible lines, a 312-line field, and
region-aware VDLP/machine timing. Battle Chess Europe renders correctly at 320x288.
Per-game region overrides and state-driven region switching are supported. CORE-04 now
returns illegal-request statuses for unsupported/unknown CD commands, implements the
guest RESET command, exposes `CdromCommandTranscript`/`run --cd-transcript`, and
read-aheads native audio sectors in the disc prefetch cache.
FND-02 is complete with settings schema v2, legacy normalization, malformed-file backup,
per-game profiles, and `settings validate|migrate`. UX-15 adds portable mode:
`portable.flag`, `--portable`, `FIVEDO_DATA_DIR`, and named profiles redirect settings,
NVRAM, cheats, library state, and logs; `InstanceLock` prevents two desktop processes
from writing the same directory. **F2** toggles the frame/lag/skip and selected-player
input overlay. AUD-01 adds OpenAL device selection and a 20-500 ms buffer target with
live output recreation; `audio devices` lists playback devices. FND-08/UX-17 add
`diagnostics bundle` and **Help > Save Diagnostics Bundle...** with path/account
scrubbing. Save-state format v3 now declares feature flags and a section table, keeps
v1/v2 readable, records the live region, and supports warn/reject unknown sections.
Remaining disc work is known-title
CD-DA evidence and a real in-game multi-disc swap, plus subcode. A 10-disc
Release smoke after the
TOC change reported 10/10 Loadable/Rendered. **1,473 tests pass.**
The five not-loadable rows are resolved or explained: Mirai Shounen Conan now runs
(transient CHD read error), and the four Arcade CD-ROM titles load with
`panafz10-patched.bin` plus the default headless pad. The extended-boot retry renders
all 18 former failures except one: 593 of 594 discs now render, and Crash 'n Burn
(Japan) remains a known boot failure. WorldCup Super Stadium needs the FZ-10J BIOS
plus its Kanji font ROM (BIOS2), which the BIOS library now classifies correctly. Headless
`run`/`trace` now attach a default player-1 pad,
fixing Olympic Summer Games' controller check. Fifteen triaged titles now have a
reviewed 5DO status in [`GameCompatibilityList.md`](GameCompatibilityList.md); the
rest remain `No data`. The CORE-01 ARM timing option (`--arm-timing freedo|arm60`), its
save-state snapshot, `--cpu-clock 0.5x-4x`, and `--random-seed` are implemented with
tests. `run --cycle-classes` adds a per-instruction-class instruction/cycle ledger
(`cycleClasses` in `run --json`), and the exact N=2/N=4 class-cost table is documented in
`PortingNotes.md` 12.28. A 40-title A/B reports 17/40 matches with ARM60 versus 15/40
with FreeDO (`TestCorpus.md` 1.1a1); the differential harness now accepts
`--arm-timing`, `--cel-timing`, and `--cd-timing` alongside `--clio-timers` and
`--profile`. The CORE-02 CLIO timer option (`--clio-timers freedo|opera`) now implements
Opera's per-slot scan and cascade carry behind the safe FreeDO-compatible default, with
a state snapshot, 16-bit timer-register masking in Opera mode, a reference-model
conformance test, per-timer telemetry (`--clio-timer-stats`), a bounded
`--clio-timer-trace`, and the smoke/A-B differentials in `TestCorpus.md` 1.1b. The
reference debug core now records matching timer/read events (`--timer-trace`), which show
the timer-14 period equal at 12,384/12,396 cycles (`TestCorpus.md` 1.1az).
The CORE-03 CEL option (`--cel-timing freedo|opera`) now implements Opera's deferred
draw and pixel/texel busy deadline behind the unchanged synchronous default, with a
Madam section v2 busy snapshot, an Options v3 mode snapshot, and a three-title
3,000-frame smoke differential in `TestCorpus.md` 1.1c.
The CORE-04 CD option (`--cd-timing freedo|opera`) now implements Opera's cycle-based
2048-byte data-sector readiness at the stock 2x drive speed, including delayed data
polling, deferred read-data status, the XBUS CD FIQ, a CD-ROM section v2 clock
snapshot, and an Options v4 mode snapshot. The immediate FreeDO path remains the
default; the three-title smoke differential is in `TestCorpus.md` 1.1d. The Opera
poll reset now keeps the three interrupt enables (`0x87`), and the XBUS CD FIQ is
level-held each clock batch as in `opera_xbus_tick`; `TestCorpus.md` 1.1az records
the fix and the remaining guest copy/decrypt wait.
SYS-09 adds `--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x` (Settings > Emulation > CD
speed), Opera's per-speed overhead table, an Options v5 speed snapshot with a mismatch
warning, and the three-title 3,000-frame smoke differential in `TestCorpus.md` 1.1e.
CORE-08 adds `EmulationProfile` (`Compatible`, `Accuracy`, `Performance`, `Custom`),
`--profile`, and Settings > Emulation > Timing profile; the accuracy profile enables the
ARM60, Opera timer, CEL busy, and CD cycle options together, and `run --json` reports the
profile derived from the actual options. `bench --profile` measures any named profile and
reports its timing modes, `selftest` checks determinism plus state round-trip for every
named profile, and the differential harness accepts `--profile` for a full-collection A/B.
A 40-title A/B reports 17/40 matches for both `compatible` and `accuracy` with identical
per-title statuses (`TestCorpus.md` 1.1ak1); the 594-title A/B remains.
SYS-14 adds per-game NVRAM and versions. `NvramStorageMode` (`Global`, `PerGame`),
`NvramVersion` (0-9), and `NvramDirectory` select shared `nvram.ram`, versioned
`nvram.<n>.ram`, or `nvram/per-game/<gameId>.<n>.ram` images. The store reads migration
fallbacks from per-game version 0, the shared version, and the legacy image; saves stay
atomic, and a corrupt primary is backed up rather than replaced by an older image.
Settings > Emulation exposes the mode and version. The CLI adds `--nvram-mode`,
`--nvram-version`, and `--nvram-dir`, reports the resolved path in `run --json`, and
adds `nvram path|list|export|import|reset`.
SYS-04 adds the BIOS manager: a built-in MD5 catalog, `BiosLibrary` scanning/validation,
region/Kanji/Anvil/patch metadata, `--bios2` and `--bios-auto`, `bios list|scan|select`,
Settings > Paths BIOS folder/auto-select/scan, per-game BIOS overrides, and the
Settings > Emulation override combo. Auto-selection uses the game database region plus
known requirements for Slayer Europe/USA, Powers Kingdom (Europe), and Arcade CD-ROM
discs; the configured BIOS remains the fallback when no BIOS folder is set. The
five-title 3,000-frame smoke run and four known-requirement 12,000-frame runs are in
[`TestCorpus.md`](TestCorpus.md) 1.1f. The first Opera parity batch (VDLP, ARM carry, DSP FIFO, MADAM span/MARIA/BPP0/SKIPX/LRFORM/blue-LSB/REGCTL/PIXC, CLIO FIFO), OP-14 (XYPOSH/XYPOSL origin, retired `GraphicsStepY`/`BitTiming6`), OP-SYS-02 (Opera CD poll/local reset), OP-SYS-01/03's ROM overlay, DIPIR, XBUS, and CSTAT soft-reset work, and OP-SYS-06's separate CLIO PRNG are complete (the DIPIR/soft-reset path remains opt-in pending a boot transcript); see `FixParity.md` and the completed log in `ImplementationPlanArchive.md`.
UX-01/UX-02 add rewind and fast-forward. `RewindBuffer` keeps compressed keyframes plus
the per-frame PBUS packet, replays input deterministically after the nearest keyframe,
clears on timeline jumps, and enforces a 256 MB default cap; `CoreRunner.Rewind` restores
the machine and presents the frame. `CoreRunner.SpeedMultiplier` runs 2x-10x frames per
presentation tick with intermediate rendering skipped and audio muted. The shell binds
`R` (one-second rewind), `Shift+F9` (one-frame rewind), and hold-`Tab` (fast-forward);
Settings > Emulation controls the history length and multiplier. UX-04 adds per-game RAM
cheats: `FiveDo.Cheats` parses raw, typed, conditional, and button-activated codes;
`CheatStore` owns the per-game JSON files; `FiveDoMachine.BeforeFrame` applies them every
frame (including rewind replay and fast-forward); the shell adds **Emulation > Cheats...**
and Settings > Paths > Cheats; the CLI adds `--cheat`, `--cheat-code`, `--cheat-auto`,
`--cheats-dir`, and `cheat list|add|enable|disable|remove|import|export`. UX-06 adds
deterministic TAS movies: `FiveDo.Tas` owns the versioned `.5domovie` container
(per-frame PBUS packets, optional starting state, CRC-32 and Brotli), record/replay,
branch/trim, and verification; the CLI adds `--movie-record`, `--movie-play`,
`--movie-verify`, and `movie info|record|play|verify|branch`; the shell adds
**Emulation > Record Movie.../Play Movie.../Stop Movie** and a live player-1 input
display. UX-11 adds a disc browser and disc-info dialog: `FiveDo.Host.DiscBrowser`
mounts the Opera file system without starting the machine, shows a sorted
directories-first tree with sizes/LBAs, extracts a file, directory, or the whole
disc with safe path containment, and reports the volume label/comment, game ID,
4DO database metadata, and automatic fixes. File > Disc Browser... and File > Disc
Info... expose it in the shell. SYS-15 adds the missing 3DO input devices:
`InputDeviceSet` selects a joypad, flightstick, mouse, lightgun, arcade lightgun,
Orbatak trackball, or no device on each of six ports; `PbusChainEncoder` emits Opera's
daisy-chain records and keeps the legacy 4DO packet while every port is a joypad. The
desktop shell adds Settings > Emulation > Input devices, pointer-driven mouse/lightgun
input with a crosshair, and the CLI adds `--input-device` and `--pbus-format`. Crime
Patrol, Mad Dog II, Orbatak, and Shootout at Old Tucson boot and detect the selected
arcade device; scripted gameplay and calibration remain.
UX-12 adds **File > BIOS / NVRAM Manager...**, a two-tab manager that scans and
classifies BIOS images, imports validated `.bin`/`.rom` files, sets BIOS1/BIOS2, pins
or clears per-game BIOS assignments, and lists shared/per-game NVRAM images with
validation, game metadata, version, size, and modification time. The NVRAM tab backs
up one image or the whole layout, imports a validated 32 KB image, resets an image to a
fresh volume, and deletes an image with a second-click confirmation. `BiosInventory`
and `NvramInventory` isolate the file operations for tests and use atomic writes.
UX-19 adds OSD notifications and the in-game quick menu. `FiveDo.Host.Osd` owns a
bounded, time-expiring notification list and validated position/opacity/duration
options; the game view shows colour-coded toasts for save/load, slot changes, rewind,
fast-forward, screenshots, reset, and settings. **F4** or the gamepad Select+Start
chord opens a quick menu that pauses the game and offers resume/pause, save/load,
rewind, fast-forward, screenshot, reset, cheats, settings, stop, and close; Up/Down
and Enter navigate, and closing resumes the prior state. Settings > Video exposes the
OSD and quick-menu options. UX-13 adds persistent library favorites, launch counts,
last-played timestamps, and play time (`library-state.json`); a 60-second flush
bounds crash loss. `LibraryPlaytimeTracker` measures only running emulation. An
optional offline `library-metadata.json` overlay adds title/developer/publisher/
year/genre/region/players/description by game ID or path, while the 4DO database now
supplies year/publisher/region directly. The game list adds star toggles,
metadata/play-time text, All/Favorites/Played/Unplayed filters, and
Title/Recently played/Most played/Release year/Publisher sorts; Settings > Paths
chooses the metadata file. External metadata providers, rich media, and the >=90%
rich-metadata target remain. The Lemmings 300-frame differential smoke still passes
(1 match, 0 diverged). **1381 tests pass.**
The differential harness (FND-05/CORE-11/CORE-16) is implemented:
`tools/FiveDo.DiffHarness` stages the bundled OperaCore and BIOS, runs both engines
with identical options, compares checkpoint frame hashes, applies manifest
allowances, and writes JSON/Markdown/HTML reports. The Lemmings (USA) 300-frame
smoke matches at frame 300 (`0x110E7DC5` on both engines); the five-title showcase
manifest is `benchmarks/diff/showcase.json`, and its first 3,000-frame run
video-matched Wolfenstein 3D and Alone in the Dark while Lemmings, Gex, and Road
Rash diverged. The strict PC gate shows 0/5 matches: the video matches still have
PC deltas of 184,696 and 739,184 bytes, so frame hashes alone are not sufficient
evidence. The `manifest` command generated the portable 594-title
`benchmarks/diff/full.json`. See
[`DifferentialHarness.md`](DifferentialHarness.md) and
[`TestCorpus.md`](TestCorpus.md) 1.1g. The remaining work is the full-collection
run, state/CD transcript fields, and the nightly baseline.
The v1.0 documented-compatibility release is ready: M13 loadability is complete
(593/594 render), legal/attribution sign-off is recorded, and M13.5/M13.6 plus M14
are post-1.0. The project is **donationware** (non-commercial, source-available,
voluntary donations only). See
[`CompatibilitySweep.md`](CompatibilitySweep.md),
[`GameCompatibilityList.md`](GameCompatibilityList.md), and
[`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).
**Release definition:** documented compatibility, not full compatibility; see
[`ReleaseChecklist.md`](ReleaseChecklist.md).
**Scope:** pure C# 3DO emulator, desktop GL/GLES presenter, optional Vulkan and Direct3D 11 backends, CLI, and deterministic tests.
**Reference:** `References/opera-libretro-master/libopera` is now the primary
behavioral reference for new compatibility work; see
[`ReferenceStrategy.md`](ReferenceStrategy.md).

> **2026-10-30 priority change (superseded 2026-11-02):** the v1.0 release was
> deferred for the **M13 full-collection loadability and in-game sweep** and **M14
> high-resolution CEL research**. M13 loadability is now complete (594 tested, 593
> render, 1 known boot failure, 0 not loadable); M13.5/M13.6 and M14 are post-1.0.
> Gameplay-depth work (full races, inventory interaction, long playthroughs) remains
> post-1.0. The project is **donationware**: non-commercial, source-available, and
> funded by voluntary donations rather than sales. The Linux validation target is the
> local Hyper-V VM `LinuxMint` (`D:\Hyper-V\VMs\LinuxMint`). See
> [`CompatibilitySweep.md`](CompatibilitySweep.md),
> [`GameCompatibilityList.md`](GameCompatibilityList.md), and
> [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

This is the short, current answer to "where are we?" The documentation index is
[documentation index](index.md). The detailed task list is in
[`ImplementationPlan.md`](ImplementationPlan.md), the performance contract in
[`Performance.md`](Performance.md), the local game corpus in
[`TestCorpus.md`](TestCorpus.md), the release gate in
[`ReleaseChecklist.md`](ReleaseChecklist.md), and the compatibility completion
plan/next actions in [`ImplementationPlan.md`](ImplementationPlan.md).

---

## 1. What works today

- **Core:** ARM60 interpreter, configurable 2-15 MB DRAM / 1-2 MB VRAM (stock 3 MB total) with 2 MB ROM, shared/per-game NVRAM, CLIO timers/FIQ/FIFO/DMA,
  MADAM CEL/pixel/matrix engines, VDLP, SPORT, DSP, Quartz scheduler, XBUS, CD-ROM,
  ISO/BIN/CUE/CHD/CDI/NRG storage, input/PBUS, game database, deterministic save states.
- **Save states:** state format version 2 compresses large section payloads with Brotli
  while keeping the CRC over the uncompressed data. Version-1 states still load. A
  24,902,592-byte local state becomes 647,647 bytes (300-frame Gex) or 2,001,229 bytes
  (14,900-frame Gex); the section inspector reads the header without decompressing
  payloads, and `state inspect` reports stored/compressed sizes.
- **BIOS:** `panafz1.bin` boots, runs the splash/animation, initializes the OS, and
  reaches the shell. The earlier `PC 0x205C4` "halt" was the Portfolio idle loop.
  SYS-04 adds a BIOS manager: `BiosLibrary` scans `.bin`/`.rom` files, computes MD5, and
  classifies region, video standard, Kanji, Anvil, and patch state; the built-in catalog
  covers the common FZ-1/FZ-10/FZ-10E/FZ-10JA, GoldStar, Sanyo, patched arcade, Kanji
  font, and arcade support images. `--bios-auto` and Settings > Paths > Auto-select BIOS
  choose a region-appropriate primary plus optional BIOS2; `--bios2` loads an explicit
  secondary. `bios list|scan|select` expose the catalog, and per-game BIOS overrides are
  stored by 4DO game ID. The configured `BiosPath` remains the fallback, so existing
  settings keep their exact BIOS.
- **Video:** the desktop two-stage presenter expands palettes at native resolution
  into an FBO, then scales/filters to the output. Desktop GL 3.3 and GLSL ES 300
  sources are selected from the context version; the frontend can force the backend
  with `--gl-backend auto|wgl|angle|software`. Windows ANGLE/EGL OpenGL ES 3.0 is
  validated on the development RTX 4070: all filters compile and a 320x240 Accurate
  capture matches the CPU oracle pixel-for-pixel. The VDLP bitmap path now honors the
  modulo-derived line width (320/384/512/640/1024). The palette shader was fixed in the
  eighth pass to use independent red, green, and blue CLUT indices; the previous shader
  used only the low five bits and made real games grayscale. An opt-in high-resolution
  mode (`--high-res`, `EmulatorTuning.HighResolution`) renders CELs and bitmap lines
  into four 1 MB VRAM planes and presents 640x480 by line-doubling the 640x240 source;
  Lemmings' title screen now matches the 640x480 reference. This is not true vertical
  640x480 rendering; the internal high-resolution research is M14. The mode is off by default
  because 320x240 titles must not be upscaled, and the desktop shell exposes it as a
  Hi-Res checkbox that restarts the machine. An opt-in xBR-lv2 4x filter, HQx
  2x/3x/4x filters, MMPX 2x, bicubic sampling, and CRT bloom are available in Settings; xBR and HQx are GPU
  passes over the GPU-expanded native source with sharp-bilinear finishing, MMPX runs a
  2x GPU rule pass, and CRT
  bloom adds a 3x3 native-resolution glow in the CRT filter. The CPU HQx reference
  hashes remain the oracle, and the GPU HQx rule table matches them in the opt-in
  parity tests.
- **Performance:** the machine runs on a dedicated core thread paced by the 44.1 kHz
  audio clock (60 Hz fallback) with a frame pacer; the UI renders on demand; uploads use `TexImage2D` allocation + `TexSubImage2D`;
  uniform locations and the present delegate are cached; physical-pixel sizing is
  implemented and unit-tested at 100/125/150/200% scaling. The benchmark reports
  p50/p95/p99 and per-stage allocations, and the
  synthetic steady state is 0 bytes/frame. An opt-in bounded prefetch cache is
  available for CHD/ISO streaming. The `--gl-probe` validates both WGL and
  ANGLE/GLES: 4K sharp-bilinear presents in 0.14 ms mean / 0.47 ms p99 with 0
  renderer allocations/frame on the RTX 4070; a 320x240 Accurate capture matches
  the CPU oracle on both backends.
- **Optional Vulkan:** `FiveDo.Video.Vulkan` is an offscreen Vulkan 1.0 two-stage
  presenter with build-time SPIR-V shaders, sampled R16_UINT/CLUT/parameter
  textures, accurate/nearest/sharp-bilinear/CRT/xBR/FSR/HQx filters, GPU timestamp
  queries, and a host readback buffer. xBR-lv2 runs as a 4x GPU pass; FSR 1.0 runs
  EASU plus RCAS; HQx 2x/3x/4x run as a GPU rule-table pass over the palette texture.
  `--vk-probe` writes the shared
  GPU probe JSON and PNG captures; `--vk-window-probe` creates a native Win32 or
  Linux/X11 swapchain, presents through FIFO vsync, and writes the same report with
  `offscreen: false`; `--gl-backend vulkan` hosts the optional desktop Win32 or
  Linux/X11 child surface. On the bare-metal Linux HD 4000 host (2026-11-16) the X11
  window probe presents at 1920x1080 FIFO (60.1 fps, 16.4 ms present, 6.3 ms GPU,
  0 bytes/frame) and the 320x240 Accurate capture matches the CPU oracle with 0
  different pixels; the Linux shell presents through a depth-24 X11 child window at
  1060x466 and rescales on resize.
  The 320x240 Accurate probe matches the CPU oracle
  (`0xD6BD89C5`, 0 different pixels); 4K sharp-bilinear measures 0.24 ms mean /
  1.06 ms p99 present and 0.075/0.091 ms GPU, 4K xBR 0.32/0.80 ms present and
  0.13/0.14 ms GPU, 4K FSR 0.60/1.30 ms present and 0.40/0.42 ms GPU, and 4K HQx
  2x/3x/4x 0.26/0.41, 0.29/0.34, and 0.34/0.52 ms present with 0.10/0.12/0.15 ms
  GPU, all with 0 managed bytes/frame on the RTX 4070. The 4K window probe reports
  0.077/0.088 ms GPU and 5.96/6.53 ms present (including the 165 Hz FIFO wait), 0
  managed bytes/frame. Wayland and Metal surfaces remain future work; Wayland-only
  sessions use the OpenGL presenter. See [`Vulkan.md`](Vulkan.md).
- **Renderer robustness:** GL/GLES capability parsing reports the API and version,
  fullscreen/F11/Escape work, GL readback screenshots are implemented, and GL init
  failure or context loss switches the shell to the CPU `WriteableBitmap` presenter.
  An optional FPS/status overlay is available in the settings dialog.
- **Desktop shell:** the game library scans configured folders through the 4DO
  database, previews sibling cover art, and opens the selected disc. Library
  favorites, play time, launch history, and an optional offline metadata overlay
  persist in `library-state.json`/`library-metadata.json`; the game list adds star
  toggles, metadata/play-time text, collection filters, and sort orders. The settings
  dialog edits video, audio, emulation, and paths; keyboard bindings for all eight players can be captured with
  automatic conflict removal and reset-to-defaults; gamepads are mapped through a
  Gamepad tab (Windows XInput, Linux evdev, macOS IOKit HID). Save states use ten numbered slots with
  next/previous, a list/save/load/delete window, JSON metadata, and PNG thumbnails;
  screenshots are PNG files named by game, time, and frame. **File > Disc Browser...**
  lists the current disc's Opera tree and extracts a file, directory, or the whole
  disc; **File > Disc Info...** shows the volume, game ID, database metadata, and
  automatic fixes. **File > BIOS / NVRAM Manager...** lists the BIOS library, sets
  BIOS1/BIOS2 and per-game assignments, and backs up, imports, resets, or deletes
  shared/per-game NVRAM images. The shell has a first-run
  guide, error dialogs, and an in-app diagnostic log viewer with level/category/search
  filters. The app accepts a positional disc path or `--disc <path>` and ships a Linux
  `.desktop` entry; Windows/macOS associations remain installer work. Per-game 4DO fix
  flags are applied automatically when a disc opens; explicit masks can be set in the
  settings dialog. The shell dispatches the configurable `InputProfile.Hotkeys`
  bindings (F5 save, F8 load, F6/F7 previous/next slot, F9 frame advance, F10
  reset, F11 fullscreen, F12 screenshot, Escape pause by default) before forwarding
  keys to the emulated pad. **Emulation > Record Input** captures player-1 input to
  a replayable script. Loading a state
  whose recorded disc hash differs from the current disc reports a warning in the
  status line and log instead of loading silently.
- **Logging and bug reports:** Serilog is configured once by each host through
  `FiveDo.Diagnostics.Logging`. The pipeline writes rolling daily files, stderr
  (desktop), the in-memory **Log Viewer**, and an asynchronous bug-report sink for
  `Warning`-or-higher events. Reports include the required environment, error, and
  exception blocks; submission is deduplicated for five minutes, capped at ten per
  minute, and disabled in CI/test runs. `FIVEDO_BUG_REPORTS=0` disables it and
  `FIVEDO_LOG_DIR` redirects the files.
- **Cheats:** per-game JSON cheat files are applied before every frame by
  `FiveDoMachine.BeforeFrame`, so state loads, rewind replay, and fast-forward keep the
  configured entries active. The engine supports raw byte writes, typed
  `write8`/`write16`/`write32` writes, memory conditions, player-1 button conditions, and
  `and`/`or`/`not` combinations. Emulation > Cheats... and Settings > Paths > Cheats
  manage files; the CLI exposes `cheat list|add|enable|disable|remove|import|export` and
  `run --cheat`/`--cheat-code`/`--cheat-auto`. See [`Cheats.md`](Cheats.md).
- **Per-game fixes:** `GameCompatibility` maps the 4DO game IDs to `FixMode` bits.
  The CLI exposes `--fix-mode 0xMASK` and `--no-game-fixes`, and `run --json` /
  `info` report the applied mask. Alone in the Dark's `FIX_BIT_TIMING_6` is applied
  automatically; the current 3,000-frame boot keys are in
  [`TestCorpus.md`](TestCorpus.md) 1.1 after the audio-clock rebaseline.
- **Tooling:** CLI `run`, `trace`, `bench`, `selftest`, `info`, `disc dump/tracks`
  (`disc tracks` covers ISO/BIN/CUE/CHD/CDI/NRG), `fs list/extract` (the
  first-party Opera parser covers ISO/BIN/CUE/CHD/CDI/NRG),
  `disasm`, plus KPRINT, CD, CLIO, FIQ, RAM-write watchpoints,
  RAM/DSP dumps, DSP-instruction rings, CEL command/geometry traces, PBUS packet
  traces, CPU instruction-ring diagnostics, scripted input, NVRAM load/save, periodic
  frame capture (`--capture-dir`/`--capture-every`), RAM patches at run start
  (`--poke ADDR=HEX`), an experimental Road Rash road-following driver
  (`--autopilot road`), an opt-in bounded disc prefetch cache (`--prefetch`), and RAM
  dumps at trace stops. `run --json` reports the source size, VDL modulo, road
  coverage, autopilot state (including `autopilotMenuAdvance`), and CD status/poll
  for mode/crop, driver, and wait-state investigations.
- **Audio:** the DSP executes in every run, including headless runs with no host
  audio sink. The AudioFolio semaphore handshake works, instrument programs run, and
  the corpus produces non-silent 44.1 kHz stereo WAV output. The DSP sample clock is
  now exact: 44,100 samples per emulated second (600 frames = 441,015-441,020
  samples, 10.000 s), replacing FreeDO's 8.24 accumulator that ran ~0.7% fast
  (444,097 samples / 10.070 s). Road Rash's 6,000-frame capture has the same peak,
  RMS, and spectral peaks as before; only the timebase changed. Live output uses
  a bundled OpenAL Soft 1.23.1 runtime (no system OpenAL install required), makes
  the context current on the audio thread, pads underruns, and reports
  dropped/underrun/queued counters. The fill loop now rents only buffers OpenAL
  has released (the old queued-count index rewrote buffers still attached to the
  playing source and caused constant crackle), and `FIVEDO_AUDIO_TAP=<prefix>`
  records the exact queued PCM plus a per-buffer timing log. `CoreRunner` paces frames against the audio
  clock (with a 60 Hz fallback), the CLI can play live with `--audio openal
  [--realtime]`, and `audio analyze <wav> --json` reports peak/RMS/clipping/DC
  statistics. Exact-clock 600-frame captures: Wolfenstein 18,100 / 2,168,
  Lemmings 8,192 / 1,661, Gex 12,964 / 2,089, Road Rash 32,768 / 11,012 (2,036
  clipped), Theme Park silent; see `TestCorpus.md` 1.5a2. The trimmed win-x64 and
  linux-x64 CLI and the opt-in NativeAOT CLI open the same backend. The opt-in
  NativeAOT frontend completed 10-minute null and default-device soaks with
  **0 dropped samples / 0 underruns** (2026-11-10).
- **Input:** joypad, flightstick, mouse, lightgun, arcade lightgun, and Orbatak
  trackball devices are supported on six PBUS ports. The legacy 4DO packet remains the
  default for all-joypad configurations; selecting a peripheral emits Opera's
  daisy-chain records. Settings > Emulation > Input devices and the CLI
  `--input-device` select devices; `--pbus-format opera` forces the chain layout. The
  desktop pointer drives the first mouse/lightgun/trackball port and shows a crosshair
  for lightguns. See [`Architecture.md`](Architecture.md) §9.
- **Input scripting:** the CLI accepts `--input-script <path>` with
  `frame button[,button...]` lines for repeatable controller taps. Input has been
  validated in Road Rash (race state/wreck), Wolfenstein (menus, gameplay, level
  start), Alone (scene/menu/intro), Lemmings (menu and passcode entry), and Space Ace
  (X menu, A play).
- **Tests:** 1381 passing (`dotnet test -c Release`), including high-resolution CEL and
  VDLP regression coverage, RAM-patch and Road Rash autopilot contracts, CLI process
  tests, zero-allocation uploader/mailbox assertions, OpenGL capability parsing,
  bounded prefetch-cache tests, game-fix mapping, save-slot metadata/roundtrips and
  thumbnails, keyboard/gamepad binding capture, gamepad mapping, log filtering,
  source-generated CLI JSON contracts, frontend startup-argument parsing, renderer
  backend selection, GPU-probe capture/parity contracts, shader source invariants,
  Vulkan xBR/FSR/HQx split-frame orientation, save-state disc-hash warnings, hotkey
  routing, input-recorder coalescing/roundtrip, ISO/BIN `disc tracks`, Opera FS parsing
  (multi-block directories, `burst`/`gap` reads, invalid headers, and CLI ISO
  extraction), and BIOS catalog/scan/selection/override tests.
- **Packaging:** CI builds/tests/formats and publishes one bundle per system
  (`5do-win-x64.zip`, `5do-linux-x64.tar.gz`, `5do-osx-x64.dmg`). Each bundle
  contains the CLI and desktop app as framework-dependent single-file apphosts, the
  README/license/notices/whatsnew, and a generated `PACKAGE.txt`. The .NET 11 runtime
  is required on the host and is **not** bundled; PDB and XML documentation files are
  removed. Native OpenAL Soft/Skia/ANGLE libraries stay beside the apphosts for LGPL
  replacement. The CLI also has an opt-in NativeAOT win-x64 publish
  (`-p:FiveDoNativeAot=true`) that is CI-smoke-tested with `--version`, `selftest`,
  `bench`, `info`, `fs list`, a golden 3,000-frame boot, and live OpenAL audio. The
  frontend has an opt-in NativeAOT single-file publish that passes `--smoke-test` and
  the WGL/ANGLE parity probes. Unsigned installer/package scaffolding now covers the
  Windows Inno Setup installer with `.iso`/`.cue`/`.chd` associations, Linux
  `.deb`/`.rpm` packages with a desktop entry and `application/x-3do-disc` MIME type,
  and macOS `.app`/`.dmg`/`.pkg` with a document UTI. See
  [`Installers.md`](Installers.md) and
  [`packaging/README.md`](https://github.com/purelogiccode/5DO/blob/master/packaging/README.md).

---

## 1a. Latest compatibility sweep (2026-10-30)

The first full loadability pass over both collections is complete. It booted every
disc for 3,000 frames and recorded JSON/PNG evidence under `artifacts/compat-full/`.
The committed summary is [`GameCompatibilityResults.md`](GameCompatibilityResults.md).

| Result | Count | Meaning |
|---|---:|---|
| `Loadable/Rendered` | **593** | Completed 3,000 frames (18 rows retried at 20,000-65,000 frames); final frame is non-black. |
| `Loadable/No video` | **1** | Completed 65,000 frames; still stuck on a boot screen. |
| `Not Loadable` | **0** | No CLI error or timeout remains. |
| **Total** | **594** | 589 main collection + 5 MAME software-list images. |

The 2026-11-01 extended-boot retry reran every one of the 18 non-rendered rows. The
five former `Not Loadable` rows (`Crime Patrol`, `Mad Dog II`, `Mirai Shounen Conan`,
`Orbatak`, and `Shootout at Old Tucson RE3`) now complete and render at 20,000 frames
with `panafz10`, and 11 of the 13 former black/blank rows render at 20,000 frames or at
65,000 with the Anvil BIOS. The two remaining rows are known Anvil boot-screen hangs:
`Crash 'n Burn (Japan)`. WorldCup Super Stadium now renders when the FZ-10J BIOS is paired with its Kanji font ROM (BIOS2).

The 2026-11-01 retry reran all 13 former `Loadable/No video` titles. Ishida Yoshio
Kudan no Igo Seiha, both Konpeki no Kantai discs, all three Myst discs, Olympic Summer
Games, and all three Slayer discs render at 20,000 frames with `panafz10`; Powers
Kingdom (Europe) renders its live-action intro at 65,000 frames with
`panafz10e-anvil`. Only `Crash 'n Burn (Japan)` remains on the Anvil boot screen at 65,000 frames;
WorldCup Super Stadium renders with the FZ-10J BIOS and its Kanji font ROM.

`Loadable/Rendered` is **not** a final compatibility grade; it still needs the visual
and input review described in [`CompatibilitySweep.md`](CompatibilitySweep.md).

## 2. Real-game status (local corpus, `panafz1.bin`)

The table below is the curated five-title showcase set. The full local collection
(594 images across `J:\Panasonic 3DO` and `G:\MAME\MAME Software List CHDs\3do`) is
being swept in M13; the working matrix is
[`GameCompatibilityList.md`](GameCompatibilityList.md).

The 2026-10-24 comparison used the same BIOS and an ISO extracted from the same CHD
on both sides. Opera is the bundled Windows `OperaCore` build in
`References/CPP_SimpleGui`; it is a behavioral reference, not product code.

| Title | Our C# port | Opera reference |
|---|---|---|
| Lemmings (USA) | **Boots, renders, plays DSP audio**; reaches the title screen and the passcode screen; with `--high-res` the 640x480 title renders all four buttons and the full logo, matching the reference. The Fun 4 code `HNLHCIOECW` is now typed entirely with the D-pad (`docs/scripts/lem-fun4-dpad.txt`); gameplay starts at 23,000 frames (`PC 0x00083FE8`, hash `0x63C02DED`) and roundtrips (`PC 0x000230A4`, `0x4CA339CD` twice). 600-frame WAV evidence in `TestCorpus.md` 1.5a | Not re-tested this pass; expected to pass |
| Space Ace (USA) | **Boots, renders, plays DSP audio**; ReadySoft player runs the animated demo past 12,000 frames. The interactive path is now validated: X returns to the title/menu, A starts the scene; the 15,000-frame state roundtrips (`0x7E780D93` -> `PC 0x000284EC`, `0x70B16422` twice) and the gameplay WAV is non-silent | **Plays the demo intro for 60 s, no runaway** |
| Road Rash (USA) | **Boots, renders, plays DSP audio**; the 3D race renders correctly and the attract race runs past 60,000 frames with no allocator error; B accelerates, A brakes, the D-pad steers, C attacks, and P pauses. The CD media-access pulse fixes the old ~42k/~92k READ_ID-only wait: those screens advance to `BUSTED`, the race-select menu, and a new race. The autopilot now passes `GAME LOADING`/`Get ready` and cycles race -> wreck -> menu -> new race to 100k, but still does not finish a full 5.5-mile race | **Renders the intro FMV, then a race** |
| Wolfenstein 3D (USA) | **Boots, renders, plays DSP audio**; A opens the preview menu, selecting a level starts a fresh game (FLOOR 1-1), the player can walk/turn/fire, and save/load roundtrips in-game (`0xF568EBCA` -> `PC 0x000782B8` twice); 600-frame WAV evidence captured. The fresh-game load advances with A pulses (character select -> FLOOR 1-1 at 64,000, hash `0x2F5B9864`) | **Renders the intro for 60 s** |
| Alone in the Dark (USA) | **Boots, renders, plays DSP audio**; reaches the in-game staircase/room with an upright character; the Up/Left/A/Down sequence reaches the Begin/Resume menu, A starts the intro scene, A pulses pass the intro, and a scripted room movement sequence roundtrips (`0x44C7576B` -> two 120-frame continuations at `PC 0x0008BF00`, `0xE490C8C7`) | **Renders the intro/loading for 60 s** |

**Additional corpus (2026-10-26).** Seven more titles were booted, rendered, and
save-state-roundtripped with `panafz1.bin`: Gex, Need for Speed, Super Street
Fighter II Turbo, Total Eclipse, Killing Time, Theme Park, and Return Fire. Gex,
Total Eclipse, and Super Street Fighter II Turbo show gameplay/attract scenes;
Need for Speed, Killing Time, Theme Park, and Return Fire render menus. Six produce
non-silent 44.1 kHz stereo audio; Theme Park's captured menu is silent. Hashes and
commands are in [`TestCorpus.md`](TestCorpus.md) 1.7 and the matrix in
[`Compatibility.md`](Compatibility.md) §10.

The CD disc-end fix on 2026-10-23 is what unblocked Road Rash, Space Ace, and
Wolfenstein: `InitCd` no longer double-adds the 150-frame lead-in, so the CD DIPIR
accepts discs whose volume block count is less than the physical track length.

The 2026-10-25 eighth pass fixed a real-content renderer bug: the GPU palette shader
used the low five bits of each pixel as a single CLUT index for all three channels,
so games appeared grayscale. It now uses the red, green, and blue indices independently
and matches the CPU frame assembler. Lemmings gameplay was captured through
sharp-bilinear and xBR with correct colors. The 2026-10-29 pass removed the former
CPU-expanded xBR source; xBR now samples the GPU palette texture directly.

The 2026-10-24 session found and fixed the common blockers:

1. **DSP execution.** `AudioSink?.PushSample(Dsp.Loop())` skipped the DSP entirely
   when the host had no audio sink, which is the default headless path. The machine
   now always executes the DSP; the sink only receives samples.
2. **DSP immediate operands.** The loader shifted un-justified immediates left by
   two instead of zero. The AudioFolio tail program's `Sema4Status & 8` test became
   `Sema4Status & 0x20`, so the DSP always skipped the ARM command. The fix uses the
   reference's `IMMEDIATE << (JUSTIFY & 3)`. SAudio `-6996` is gone and the DSP now
   runs instrument code.
3. **DSP multiplier rounding.** MUXB=3 now masks the product
   (`(MULT1 * (Y >> 15)) & ~1`) instead of masking the shifted accumulator first.
4. **CCB field order.** The parser now reads position, size, perspective, and
   pixel-processor control before `PRE0`/`PRE1`, matching FreeDO. The old order
   mis-decoded every 3D CEL; Road Rash's race produced a bogus 1987x583 CEL and
   appeared to hang. The rasterizer's missing `cnt_cross > 2` x-point sort was also
   ported.
5. **NVRAM initialization.** The CLI now loads a fresh 32 KB NVRAM volume by default
   (and `--nvram <path>` persists it). Without it, Alone in the Dark saw zero free
   save slots, displayed its save-space warning, and shut down. It now reaches the
   in-game room/inventory scene.
6. **Arbitrary-CEL transposition.** `TexelDrawArbitrary` passed `(i, j)` to `(x, y)`
   read/write helpers, so every asymmetric quad was written transposed. Road Rash's
   race CELs overwrote the OS VRAM free list at `0x2FC000` (`AllocMem error, bad node
   address`) and the race froze; Alone's character was drawn horizontally. Passing
   `(j, i)` fixed both. The VDLP bitmap copy now uses the modulo-derived line width
   (no 320-pixel crop), and state loads restore the VDL pixel buffers.

Space Ace's former frame-681 crash was a downstream consequence of the failed
`AllocInstrument`: the player copied a decoded buffer to a null pointer, wiping the
ARM vectors. It now runs past 12,000 frames.

**Gameplay is now reachable and renderer-correct.** Road Rash's 3D race renders like
the Opera reference and runs past 60,000 frames; Wolfenstein 3D starts a fresh game,
walks/turns/fires, and its save states roundtrip; Alone in the Dark's in-game character
is upright and its Begin/Resume menu is reachable; Lemmings completes a Fun 4 passcode
(`HNLHCIOECW`) end-to-end into gameplay with save/load roundtrip, and its 640x480
high-resolution title matches the reference; Space Ace starts its interactive scene
(X -> menu, A -> play) and plays. Per-title save-state, PNG, and WAV evidence is
recorded in `TestCorpus.md` 1.5a.

The ninth pass corrected the Road Rash diagnosis and fixed the wait: the ~42k and
~92k stops were a static READ_ID-only CD wait, not wall pins or driving failures.
The CD device now raises a media-access event (`0x40`) after two consecutive
READ_ID polls with an idle gap; this advances those screens to `BUSTED`, race
select, and a new race. `run --json` exposes `cdStatus`/`cdPoll`, and the autopilot
reports `autopilotMenuAdvance` while it pulses A through the post-race flow. The
3,000-frame keys were later re-baselined by the exact audio clock; see
[`TestCorpus.md`](TestCorpus.md) 1.1. From the 60k state the autopilot now starts
a new race at 80k and cycles race -> wreck -> menu -> new race to 100k; the
remaining Road Rash work is finishing a full race without wrecking, not the CD
wait. That is post-1.0 backlog; it is not a v1 release gate.

---

## 3. Performance snapshot

Release build, development machine, 600 frames, CPU side only:

| Case | Total | p50 | p95 | p99 | Allocations |
|---|---:|---:|---:|---:|---:|
| Synthetic | **1.250 ms/frame** | 1.218 ms | 1.434 ms | 1.638 ms | **0 bytes/frame** |
| Lemmings CHD | **1.281 ms/frame** | 1.088 ms | 2.005 ms | 3.398 ms | ~5.9 KB/frame (CHD decode) |
| Lemmings CHD, high-res | **1.605 ms/frame** | 1.398 ms | 2.497 ms | 4.598 ms | ~5.8 KB/frame (CHD decode) |
| Road Rash 3D race (100k state) | **4.20 ms/frame** | 2.15 ms | 7.13 ms | 7.40 ms | **0 bytes/frame** |

This leaves ~15.4 ms of headroom against the 16.67 ms frame budget for the synthetic
case. The 2026-10-31 hot-path pass reduced the Road Rash race from 5.46 to 4.20 ms
mean and from 13.14 to 7.40 ms p99 (frame hash unchanged). The core projects are also
compiled with JIT optimizations in Debug, so an IDE Debug run is no longer limited to
~40 fps. CI now runs the synthetic benchmark with `--gate`: it fails on any steady-state
allocation, core p99 > 8 ms, total mean > 8 ms, or total p99 > 16.667 ms, and uploads
the JSON result as the baseline artifact. The benchmark's `--baseline` option also
implements the same-machine >10% relative comparison (core p99, total mean, total
p99; exit code 4); wiring it to CI awaits a stable/self-hosted runner. **Tier B
desktop numbers and GLES timing are not measured yet.** CHD decode allocations are
host-side and are attributed to the core stage; the synthetic
core/pack/mailbox/read path is allocation-free in steady state.

The desktop renderer uses a `GL_TIME_ELAPSED` query and reports GPU/present time in
the FPS overlay. The fourteenth pass added a true-4K offscreen `--gl-probe` and
measured the presentation path on Windows 11 (Ryzen 9 7900, RTX 4070, OpenGL 4.0):

| 4K case (3840x2160) | Present mean | Present p99 | GPU mean | GPU p99 | Renderer alloc |
|---|---:|---:|---:|---:|---:|
| Sharp-bilinear | **0.080 ms** | **0.208 ms** | **0.086 ms** | **0.089 ms** | **0 B/frame** |
| Accurate | 0.082 ms | 0.178 ms | 0.091 ms | 0.287 ms | 0 B/frame |
| CRT | 0.147 ms | 1.157 ms | 0.191 ms | 0.193 ms | 0 B/frame |
| xBR 4x | 0.283 ms | 1.466 ms | 0.185 ms | 0.324 ms | 0 B/frame |
| FSR (EASU + RCAS) | 0.132 ms | 1.191 ms | 0.441 ms | 0.483 ms | 0 B/frame |

The xBR row now uses the GPU-expanded palette texture directly; the previous path
uploaded a CPU `FrameAssembler` copy. The optional Vulkan offscreen backend was also
measured on the same machine: 4K sharp-bilinear present 0.24 ms mean / 1.06 ms p99,
GPU 0.075/0.091 ms, 0 managed bytes/frame; 4K xBR present 0.32/0.80 ms, GPU
0.13/0.14 ms; 4K FSR present 0.60/1.30 ms, GPU 0.40/0.42 ms; 4K HQx 2x/3x/4x
present 0.32/0.83, 0.32/0.48, and 0.40/0.69 ms with GPU 0.10/0.11, 0.12/0.13, and
0.15/0.16 ms; 1080p sharp-bilinear present 0.17/0.79 ms, 1080p xBR
0.21/0.38 ms, and 1080p FSR 0.27/0.78 ms, all with 0 managed bytes/frame. GPU time
excludes the optional per-frame readback. Its 320x240 Accurate output matches the CPU
oracle (`0xD6BD89C5`, 0 different pixels). See [`Vulkan.md`](Vulkan.md).

The platform swap interval is wired through `SwapInterval.TryCreate` (WGL/GLX/EGL);
the Windows WGL path resolves `WGL_EXT_swap_control` and reports `vsyncActive: true`
when requested. The Avalonia compositor paces the window independently, so the probe
reports ~64 fps throughput even when vsync is requested; the renderer cost itself is
far below the 16.67 ms budget. `DisplayRefreshRate` (Settings > Video, default 60)
re-presents the latest frame above 60 Hz when the user wants a faster swap rate; it
does not add motion because the emulated 3DO still outputs 60 fields per second.

### 4K60 feasibility

- **Desktop (Tier A): measured.** The core is ~1.25-1.6 ms/frame; the 4K presentation
  pass is one fullscreen textured quad after native-resolution palette expansion.
  The RTX 4070 measured GPU p99 0.089 ms and present p99 0.208 ms for sharp-bilinear,
  leaving ample headroom for 60 Hz. The remaining risk is the Avalonia/WGL compositor
  and frame pacing, not the emulator core.
- **Tier B desktops: unmeasured.** The presenter should handle 1080p60; integrated
  GPUs and older CPUs may be the limiting factor in complex scenes.
- **4K internal CEL rendering is not a v1 goal.** The target is 4K output, not 4K
  internal rendering.

CPU p99 and per-stage allocations are measured, and Tier A 4K GPU/present p99 is now
recorded. Remaining performance work is post-1.0: GLES 3.1 and DPI validation,
Tier B timing, and the CI relative gate. WebAssembly, Android/iOS, and Raspberry Pi
are out of scope. See [`Performance.md`](Performance.md).

---

## 4. How far from a release?

The original 157 developer-day plan assumed full compatibility hardening and a full
platform matrix. **v1.0 adopts a documented-compatibility release scope instead.**
Ship the platforms and titles that are validated, publish the per-title matrix and
known issues, and move the rest to the post-1.0 backlog. Most emulators release this
way; full compatibility is not a v1 requirement.

**As of 2026-11-02 the v1.0.0 documented-compatibility release is published on
[GitHub](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0).** M13 loadability is
complete (594 tested, 593 render, 1 known boot failure, 0 not loadable), legal/attribution
sign-off is recorded, and M14 is post-1.0 research. Linux x64 CLI/frontend smoke and the
full test suite passed (see [`Compatibility.md`](Compatibility.md) §10.3); the GL probe
timed out under llvmpipe, so Linux GPU presentation is preview/best-effort. The release
artifacts and checksums are attached to the GitHub release.

| Release workstream | State | Remaining |
|---|---|---:|
| M0-M8 core, CPU, boot, MADAM, DSP, CD, input, save states | Functionally complete | 0 |
| M9 Avalonia frontend | Feature-complete for the tested workflow | 0 (optional polish is post-1.0) |
| M9R OpenGL presenter | Windows WGL + ANGLE validated; CPU fallback, filters, parity, 4K probe | 0 on Windows; Linux x64 full suite passed, GL probe timed out under llvmpipe |
| M9P performance/pacing | Tier A measured, zero-allocation steady state, CI gate | 0 (Tier B/Pi and hot-path are post-1.0) |
| M10 packaging | Unified Windows/Linux/macOS x64 bundles, framework-dependent single-file apps, optional NativeAOT, unsigned installer/package scaffolding | 0 (signed/notarized installers require certificates; macOS package build needs a macOS host) |
| M11 compatibility | 12 titles boot/render; five have scripted gameplay evidence; matrix and known issues documented | 0 |
| **M13 compatibility sweep** | **Loadability complete: 594 tested, 593 render, 1 known boot failure** | M13.5/M13.6 post-1.0; Linux x64 full suite passed |
| **M14 high-res CEL research** | **Research plan written; post-1.0** | **Not a v1.0 gate** |
| M11 release/legal | OpenAL notice/source offer/relinking record and license text ship in every bundle; FreeDO modified GNU LGPL, donationware | Legal sign-off recorded 2026-11-02 |

**Current estimate:** roughly **100% complete** against the documented-compatibility
release scope. v1.0.0 is published on GitHub with the release artifacts.

**Release decisions (2026-11-02):** Windows x64 is validated; Linux x64 CLI/frontend
smoke and the full test suite passed (GPU presentation is preview/best-effort); the current release version is `1.7.0`;
installers, signing, and notarization are post-1.0; the compatibility set is the 12
documented titles, with five showcase titles carrying scripted gameplay evidence. The
distribution model is recorded as **donationware** (non-commercial, source-available).

The project license is recorded in [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE) as the FreeDO modified
GNU LGPL, so release builds are **donationware**: non-commercial, source-available, and
funded by voluntary donations rather than sales. The 2026-11-25 `REL-13` decision retained
those terms. The 4DO game database is a separate GPL-2.0 data component; its complete
license text is in [`GPL-2.0.txt`](GPL-2.0.txt). The project-owner legal/attribution
sign-off is recorded in [`ReleaseLegalReview.md`](ReleaseLegalReview.md). The OpenAL Soft
LGPL-2.0-or-later binary-distribution obligation is handled: the notice, complete
license text, three-year source offer, and relinking instructions ship in
[`OpenALSoft.md`](OpenALSoft.md) and
[`OpenAL-Soft-COPYING.txt`](OpenAL-Soft-COPYING.txt). See
[`Licensing.md`](Licensing.md), [`ReleaseLegalReview.md`](ReleaseLegalReview.md), and
[`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md).

Gameplay-depth and full-compatibility work is **post-release**, not a current gate:
finishing Road Rash's 5.5-mile race, scripting Alone's inventory, adding gameplay
scripts for the other seven titles, and further CD/CLIO/MADAM/ARM timing triage.
M13.5/M13.6 visual/high-resolution review and M14 research are post-1.0. The current
v1.0 gate is release mechanics (version bump, tag, and artifacts).

---

## 5. Current path (compatibility and video quality)

### P0 - v1.0 release work

1. **M13 loadability sweep: complete.** 594 discs tested: 593 `Loadable/Rendered`,
   1 known `Loadable/No video` failure (`Crash 'n Burn Japan`), 0 `Not Loadable`. See
   [`GameCompatibilityResults.md`](GameCompatibilityResults.md).
2. **Failure triage: complete.** The five former not-loadable rows and 12 black/blank
   titles are resolved or explained; Crash 'n Burn (Japan) remains the only known boot
   failure. WorldCup Super Stadium renders with the FZ-10J BIOS plus its Kanji font ROM.
3. **Linux x64 validation: complete (2026-11-02).** The full test suite
   (**1,473/1,473**), CLI `selftest`, `bench`, `info`, `bios info`, a 3,000-frame
   Wolfenstein boot, non-silent audio, and the frontend `--smoke-test` passed on Linux
   Mint 22.3. The GL probe timed out under llvmpipe, so Linux GPU presentation is
   preview/best-effort. See [`Compatibility.md`](Compatibility.md) §10.3.
4. **Release v1.0.0: complete (2026-11-02).** The version is bumped, the source is
   tagged `v1.0.0`, the unified Windows/Linux/macOS x64 bundles and checksums are
   attached to the [GitHub release](https://github.com/purelogiccode/5DO/releases/tag/v1.0.0),
   and the release-owner/QA sign-off is recorded.
5. **M13.5/M13.6 visual and high-resolution review (post-1.0).** Inspect every rendered
   final frame, record `Unplayable`/`Playable`/`Good`, and run the `-HighRes` comparison.

### P1 - research and later work

6. **M14 high-resolution CEL research (post-1.0):** measure the current mode, prototype
   true 640x480 and 2x supersampling, and write the GPU CEL feasibility note.
7. **Compatibility fixes:** only after the failure classes are triaged and compared
   with the 4DO/Opera reference.
8. **Gameplay depth (deferred):** finish a full Road Rash race, script Alone's
   inventory, and add input scripts for the other seven titles.
9. **Platforms/performance/packaging (post-1.0):** Pi/GLES 3.1, arm64 hardware, DPI,
   Tier B timing, installers/signing, file associations, and the optional M12 JIT.
10. **Release status:** v1.0.0 is published on GitHub with the artifacts attached.
    Legal/attribution sign-off is recorded, CI is green, and Linux x64 full suite
    passed. Post-1.0 work remains.

---

## 6. Next session

**v1.0.0 is published on GitHub** (source tag `v1.0.0`, unified
Windows/Linux/macOS x64 bundles and checksums attached). Linux x64 validation passed on
Linux Mint 22.3: the full test suite (**1,473/1,473**), CLI `selftest`/`bench`/`info`,
a 3,000-frame Wolfenstein boot, non-silent audio, and the frontend `--smoke-test`. The
GL probe timed out under llvmpipe and is a documented preview/best-effort limitation. The BIOS-aware
retry is complete: **593 of 594 discs render**, and only Crash 'n Burn (Japan) remains
stuck on the Anvil boot screen. The next session is **post-1.0**:

1. The v1.0.0 GitHub release is published; collect feedback and compatibility reports.
2. Run the M13.5/M13.6 in-game/visual and high-resolution review.
3. Start M14 from [`HighResolutionCelResearch.md`](HighResolutionCelResearch.md).

Do not re-enable the null audio-sink short-circuit, do not change the XBUS DMA byte
order (the raw `IRam` order is correct), do not port `opera_cdrom.c` wholesale, and do
not redo the trimmed CLI/frontend or source-generated JSON work; see
[`Trimming.md`](Trimming.md). Do not treat full-race completion, scripts for the other
seven titles, Pi/arm64 validation, or installers as current gates; they are later
backlog.

---

## 7. Reproduce

```powershell
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release

# The corpus roots are machine-specific; set them once per shell.
$env:FIVEDO_GAMES = "J:\Panasonic 3DO"
$env:FIVEDO_MAME  = "G:\MAME\MAME Software List CHDs\3do"
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Road Rash (USA).chd"

# Full-collection loadability sweep (3000 frames, resume-safe)
pwsh -File tools/compat-sweep.ps1 -Frames 3000 -Resume -OutDir artifacts/compat

# Regenerate the 4DO reference table after changing the collection
python tools/generate-compat-list.py

# Build/test/format
dotnet build -c Release
dotnet test tests/FiveDo.Tests -c Release
dotnet format --verify-no-changes

# Synthetic CPU-side baseline
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600

# Real disc baseline
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- `
  --frames 600 --bios bios/panafz1.bin `
  --disc "$env:FIVEDO_GAMES\Lemmings (USA).chd" --label lemmings

# Boot a disc and dump the last frame
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Lemmings (USA).chd" --bios bios/panafz1.bin `
  --frames 3000 --nvram artifacts/lemmings.ram --frame-out artifacts/lemmings.png

# Scripted input, frame sequence, and PBUS packet trace
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_GAMES\Wolfenstein 3D (USA).chd" --bios bios/panafz1.bin `
  --frames 12000 --input-script artifacts/input.txt `
  --capture-dir artifacts/w3d-frames --capture-every 1000 `
  --frame-out artifacts/w3d.png --state-out artifacts/w3d.state

# RAM dump at a KPRINT stop
dotnet run -c Release --project src/FiveDo.Cli -- trace `
  "$env:FIVEDO_DISC" --bios bios/panafz1.bin `
  --frames 16000 --trace-frames 2200 --stop-kprint "AllocMem error" --ring 40000 `
  --out artifacts/rr-trace.txt --dump-ram artifacts/rr-stop-ram.bin `
  --dump-ram-offset 0 --dump-ram-length 0x300000

# Opera reference: extract the data track to ISO, then run the bundled SimpleGui
dotnet run -c Release --project src/FiveDo.Cli -- disc dump `
  "$env:FIVEDO_DISC" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The desktop frontend is `dotnet run -c Release --project src/FiveDo.Frontend.Avalonia`.
See [`ReferenceStrategy.md`](ReferenceStrategy.md) for the full differential procedure.
