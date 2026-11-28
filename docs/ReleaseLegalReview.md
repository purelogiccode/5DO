# Release Legal and Attribution Review

**Date:** 2026-11-28
**Release:** 1.20.0 (five self-contained bundles)
**Status:** the 2026-11-25 `REL-13` license-compliance and relicense review is complete.
The project **retains the FreeDO modified GNU LGPL** (non-commercial donationware) and
resolves the 4DO game database as a separate **GPL-2.0** data component. Official Debian,
Ubuntu, Fedora, openSUSE, and Flathub channels remain **not eligible** under the retained
terms.
**Distribution model:** **donationware** - non-commercial, source-available, voluntary
donations only (no sales). See [Licensing.md](Licensing.md).

**1.20.0 scope:** the lobby service uses the ASP.NET Core 11 shared framework and adds
no new third-party runtime packages. The FreeDO modified LGPL and the 4DO GPL-2.0 data
component decisions are unchanged.

This is an engineering audit, not legal advice. It records what the current release
contains, what was verified, and which decisions remain.

---

## 1. What the release contains

The release is **one bundle per system/architecture**:

- `5do-<version>-win-x64.zip`
- `5do-<version>-win-arm64.zip`
- `5do-<version>-linux-x64.tar.gz`
- `5do-<version>-linux-arm64.tar.gz`
- `5do-<version>-osx-arm64.dmg`

Linux releases also publish `.deb`, `.rpm`, AppImage, and Flatpak assets. Each bundle
contains only the self-contained single-file CLI and desktop apphosts, their native
dependencies, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`,
`docs/Licensing.md`, `docs/ReleaseLegalReview.md`, `docs/OpenALSoft.md`,
`docs/OpenAL-Soft-COPYING.txt`, `docs/GPL-2.0.txt`, `PACKAGE.txt`, the release notes,
and the packaged desktop/MIME/AppStream files.

They do **not** contain `References/`, `artifacts/`, `bios/`, test corpora, disc images,
BIOS ROMs, extracted assets, screenshots, WAV captures, PDB files, XML documentation
files, or any FreeDO/4DO/Opera reference binary. The .NET 11 runtime **is** embedded in
the self-contained apphosts.

## 2. Verification performed

- [x] `git ls-files` contains no `.bin`, `.chd`, `.iso`, `.cue`, `.wav`, `.rom`, or
      `.3do` game/BIOS files. The tracked `.png`/`.jpg` files are original UI artwork and
      documentation screenshots, not extracted game assets.
- [x] `bios/` and `artifacts/` are ignored by `.gitignore` and are not copied by
      `packaging/package-bundle.ps1`.
- [x] The root `LICENSE` records the FreeDO modified GNU LGPL: the FreeDO notice
      is reproduced verbatim, the GNU LGPL 2.1 text is included, and the additional terms
      are carried forward. `packaging/package-bundle.ps1` copies `LICENSE` into each
      bundle.
- [x] The 4DO game database is a separate GPL-2.0 data component. The complete GPL-2.0
      text is in `docs/GPL-2.0.txt`, `packaging/package-bundle.ps1` copies it into each
      bundle, and `src/FiveDo.Storage/Data/GameDatabase.xml` carries the
      copyright/license header.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO, 4DO, Opera/libopera, xBR-lv2, HQx,
      CHDSharp 1.7.2, MDSSharp 1.3.0, CCDSharp 1.0.3, SharpCompress, Avalonia,
      CommunityToolkit.Mvvm, Microsoft.Extensions.*, System.CommandLine,
      Silk.NET/OpenAL/Vulkan, XenoAtom.ShaderCompiler.Build, xUnit, BenchmarkDotNet,
      the embedded .NET 11 runtime, the bundled OpenAL Soft 1.23.1 runtime, and the
      Windows app-local Microsoft Visual C++ runtime.
- [x] The OpenAL Soft native runtime is included in every Windows/Linux/macOS bundle
      through `Silk.NET.OpenAL.Soft.Native`; Windows copies `soft_oal.dll` as
      `OpenAL32.dll`. The notice, exact upstream 1.23.1 source, three-year written source
      offer, relinking/replacement instructions, and complete LGPL-2.0 text ship in
      `docs/OpenALSoft.md` and `docs/OpenAL-Soft-COPYING.txt`.
- [x] The Windows bundles redistribute `vcruntime140.dll` and `msvcp140.dll` (plus
      `vcruntime140_1.dll` on x64) app-local beside OpenAL Soft. These Microsoft Visual
      C++ redistributable files are deployed under the Microsoft Visual Studio
      redistributable terms; the notices record the files and their purpose.
- [x] The .NET 11 runtime is embedded in the self-contained single-file apphosts. It is
      MIT licensed and listed in `THIRD-PARTY-NOTICES.md`.
- [x] The desktop About dialog reports the version, renderer state, and the current
      FreeDO modified GNU LGPL notice, and states that no BIOS ROMs or game images are
      included.
- [x] The CLI prints the product/version and does not bundle BIOS/disc content.
- [x] FreeDO/4DO/Opera source headers and attribution remain in the repository.
- [x] The bundles contain no `References/` file, no FreeDO/4DO/Opera binary, no separate
      `runtime/` folder, no PDB or XML documentation files, and no BIOS ROM, disc image,
      extracted asset, screenshot, or WAV capture.
- [x] `mkdocs build --strict`, `dotnet build`, `dotnet format`, and
      `jb inspectcode --severity=WARNING` are clean for the `REL-13` source state; the
      full suite passed **2,610/2,610** at that review (2,677/2,677 for 1.19.0).

## 3. Compliance decisions and open items

### 3.1 License and relicense review (`REL-13`) — resolved 2026-11-25

The current root license is the **FreeDO modified GNU LGPL**. The FreeDO terms include a
non-commercial restriction and a full-credit requirement and take precedence over the
LGPL where they conflict. 4DO and Opera/libopera are forks of FreeDO and inherit the same
upstream position; Opera is the primary behavioral reference for new accuracy work.

The project owner reviewed the three options and chose **retention of the current
non-commercial, source-available terms**:

1. **Written rights-holder permission** has not been obtained for this release. The
   FreeDO and 4DO projects are inactive, and no permission covering a permissive
   relicence or commercial distribution is on file.
2. **Replacement or clean-room reimplementation** of the restricted core is an XL effort
   and has not been performed. Relicensing 5DO's original code alone would not remove the
   upstream restrictions on the FreeDO/4DO/Opera-derived portions.
3. **Retention** keeps the project compliant with the upstream terms for its
   non-commercial donationware distribution. The root `LICENSE`, every bundle, the About
   box, and the public docs carry the FreeDO notice and full attribution.

**Decision:** 5DO remains under the FreeDO modified GNU LGPL, distributed as
non-commercial donationware. No new license is committed. Commercial use, paid
distribution, and commercial bundling remain prohibited without written approval from the
FreeDO rights holders. The project does not claim OSI approval, Debian DFSG compliance, or
Flathub eligibility.

The 4DO game database is resolved separately in §3.2.

### 3.2 4DO game database provenance (`REL-13`) — resolved 2026-11-25

`src/FiveDo.Storage/Data/GameDatabase.xml` is derived with attribution from
`References/4DO-master/FourDO/Emulation/GameSource/GameDatabase.xml`. The 4DO project
distributes its source under **GNU GPL version 2** according to its own download page
(<https://fourdo.com/downloads>) and its SourceForge project metadata. The local 4DO
reference tree does not carry a separate LICENSE file, so the project-level GPLv2 notice
is the governing statement.

**Resolution:** the database is treated as a separate GPL-2.0 data component. The complete
GPL-2.0 text is in [`GPL-2.0.txt`](GPL-2.0.txt), the XML file carries a
copyright/license header, and every bundle includes the text. The 5DO-generated records
and extension fields are contributed under the same GPL-2.0 terms. The database is read at
runtime and is not linked into the emulator code; the emulator code remains under the
FreeDO modified GNU LGPL. If a rights holder objects to the database distribution, remove
or replace the file and ship a user-supplied database instead.

### 3.3 RetroAchievements hardcore approval (`REL-14`)

The client uses the core achievement set; hardcore credit is awaiting RetroAchievements
client approval and is currently treated as casual by the server. The compliance request
has been submitted. The code path is ready; after approval,
`RetroAchievementsCompliance.IsHardcoreApproved()` must return `true`, the pending
wording must be removed, and a live hardcore award must be recorded. This is an external
review item, not a 5DO code defect.

### 3.4 Code signing and notarization (`UX-16`)

Windows Authenticode and macOS Developer ID/notarization are not available yet. A
Windows code-signing certificate purchase is planned; an Apple Developer account is still
required for macOS. The current release artifacts are unsigned. Do not describe them as
signed or notarized, and keep the unsigned-install instructions in the release notes.

### 3.5 Linux distributions and Flathub — resolved 2026-11-25

The retained FreeDO non-commercial terms do not satisfy the Debian DFSG or Fedora legal
tests, and Flathub review would require an explicit licence decision that the retained
terms do not support. Direct downloads and the optional self-hosted mirror tooling remain
the supported Linux path. Do not claim official Debian/Ubuntu/Fedora/openSUSE or Flathub
availability. Revisit only if the FreeDO rights holders grant written commercial-use
permission or the restricted portions are replaced, and a network-free, vendored source
build exists.

## 4. Decision and operating practices

The 2026-11-25 `REL-13` decision retains the current terms and the following practices:

- keep the root `LICENSE` and the FreeDO modified GNU LGPL terms in every distribution;
- keep the 4DO game database under GPL-2.0, with `docs/GPL-2.0.txt` in every bundle and
  the copyright/license header in the XML;
- distribute source and release artifacts as **donationware** (non-commercial,
  source-available); donations do not grant commercial rights;
- keep full FreeDO/4DO/Opera attribution in the README, About box, and release notes;
- publish the five self-contained bundles plus the Linux package set; do not claim
  distro/Flathub availability under the retained terms;
- keep the .NET 11 runtime embedded (MIT) and include its notice;
- keep the OpenAL Soft notice, source offer, relinking instructions, and license text in
  every bundle;
- keep the Windows app-local MSVC runtime and its notice;
- do not describe the build as an officially licensed 3DO product;
- revisit the decision only if a rights holder grants broader permission, a clean-room
  replacement lands, or the distribution model changes.

The engineering side of the release gate is tracked in
[ReleaseChecklist.md](ReleaseChecklist.md). This document covers only the legal and
attribution portion.

## 5. Sign-off

| Sign-off | Name | Date | Scope |
|---|---|---|---|
| Release owner | Project owner (purelogiccode) | 2026-11-02 | Initial v1.0 donationware release |
| Legal / distribution | Approved: FreeDO modified GNU LGPL, donationware | 2026-11-02 | Initial v1.0 release |
| Attribution check | FreeDO/4DO/Opera, OpenAL Soft, Microsoft Visual C++, CHDSharp, MDSSharp, CCDSharp, SharpCompress, HQx, the 4DO database GPL-2.0 text, and NuGet notices shipped; .NET 11 embedded | 2026-11-28 | 1.20.0 bundle layout |
| Bundle layout | Self-contained single-file CLI + desktop app; no separate runtime folder; Windows MSVC runtime app-local | 2026-11-20 | 1.17.x bundles |
| License/relicense review | Retain FreeDO modified GNU LGPL; non-commercial donationware | 2026-11-25 | `REL-13`; no new license committed |
| 4DO database provenance | GPL-2.0 data component; text in `docs/GPL-2.0.txt`; XML header added | 2026-11-25 | `REL-13`; database distribution resolved |
| Linux distribution / Flathub | **Not eligible** under the retained non-commercial terms; direct downloads and self-hosted mirrors only | 2026-11-25 | `REL-13` channel decision |
| Code signing / notarization | **Open** | 2026-11-25 | `UX-16`; certificate/account pending |
| RetroAchievements hardcore | **Awaiting external review** | 2026-11-25 | `REL-14`; request submitted |

The 2026-11-25 decision retains the FreeDO modified GNU LGPL and records the 4DO
database as a separate GPL-2.0 component. This is an engineering audit, not legal advice;
obtain counsel before any commercial use, relicensing, or new distribution channel.
