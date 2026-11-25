# Release Legal and Attribution Review

**Date:** 2026-11-25
**Release:** 1.17.1 (five self-contained bundles)
**Status:** the 2026-11-02 project-owner legal/attribution sign-off remains the basis for
the current release. A **license-compliance and relicense review** is open as `REL-13`;
no new license or distribution channel is approved until that review is signed off and
the root `LICENSE` is updated.
**Distribution model:** **donationware** — non-commercial, source-available, voluntary
donations only (no sales). See [Licensing.md](Licensing.md).

This is an engineering audit, not legal advice. It records what the current release
contains, what was verified, and which compliance decisions remain open.

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
`docs/OpenAL-Soft-COPYING.txt`, `PACKAGE.txt`, the release notes, and the packaged
desktop/MIME/AppStream files.

They do **not** contain `References/`, `artifacts/`, `bios/`, test corpora, disc images,
BIOS ROMs, extracted assets, screenshots, WAV captures, PDB files, XML documentation
files, or any FreeDO/4DO/Opera reference binary. The .NET 11 runtime **is** embedded in
the self-contained apphosts.

## 2. Verification performed

- [x] `git ls-files` contains no `.bin`, `.chd`, `.iso`, `.cue`, `.wav`, `.rom`,
      `.3do`, `.png`, or `.jpg` files.
- [x] `bios/` and `artifacts/` are ignored by `.gitignore` and are not copied by
      `packaging/package-bundle.ps1`.
- [x] The root `LICENSE` currently records the FreeDO modified GNU LGPL: the FreeDO notice
      is reproduced verbatim, the GNU LGPL 2.1 text is included, and the additional terms
      are carried forward. `packaging/package-bundle.ps1` copies `LICENSE` into each
      bundle.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO, 4DO, Opera/libopera, xBR-lv2, HQx,
      CHDSharp 1.7.2, SharpCompress, Avalonia, CommunityToolkit.Mvvm,
      Microsoft.Extensions.*, System.CommandLine, Silk.NET/OpenAL/Vulkan,
      XenoAtom.ShaderCompiler.Build, xUnit, BenchmarkDotNet, the embedded .NET 11
      runtime, the bundled OpenAL Soft 1.23.1 runtime, and the Windows app-local
      Microsoft Visual C++ runtime.
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
      `jb inspectcode --severity=WARNING` are clean for the 1.17.1 source state.

## 3. Open compliance items

### 3.1 License and relicense review (`REL-13`)

The current root license is the **FreeDO modified GNU LGPL**. The FreeDO terms include a
non-commercial restriction and a full-credit requirement and take precedence over the
LGPL where they conflict. 4DO and Opera/libopera are forks of FreeDO and inherit the same
upstream position; Opera is the primary behavioral reference for new accuracy work.

The project owner has authorized changing the 5DO license to a compliant license where
needed to comply with the reference projects. That review must establish one of the
following:

1. written permission from the FreeDO/4DO/Opera rights holders for the intended license
   and distribution channels;
2. replacement or clean-room reimplementation of the restricted portions, with the
   replacement recorded and tested; or
3. retention of the current non-commercial, source-available terms.

Relicensing 5DO's original code alone does **not** remove the upstream restrictions on
code derived from FreeDO/4DO/Opera. The review must also resolve the 4DO game-database
provenance and the dependency set. Until it is signed off, the current release remains
under the existing `LICENSE`, and Linux distribution packaging/Flathub eligibility must
not be claimed.

### 3.2 4DO game database provenance (`REL-13`)

`src/FiveDo.Storage/Data/GameDatabase.xml` is ported with attribution from
`References/4DO-master`. The 4DO tree has no LICENSE file or source headers. Permission
or replacement remains unresolved. This is a release-blocking provenance item, not a
completed attribution checkbox; it must be resolved as part of `REL-13`.

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

### 3.5 Linux distributions and Flathub

The FreeDO non-commercial terms do not satisfy the Debian DFSG or Fedora legal tests,
and Flathub review needs an explicit licence decision. Direct downloads and the optional
mirror tooling remain the supported Linux path. Revisit only after `REL-13` resolves the
license position and a network-free, vendored source build exists.

## 4. Recommendation

Keep the following practices until `REL-13` is signed off:

- keep the root `LICENSE` and the FreeDO modified GNU LGPL terms in every distribution;
- distribute source and release artifacts as **donationware** (non-commercial,
  source-available); donations do not grant commercial rights;
- keep full FreeDO/4DO/Opera attribution in the README, About box, and release notes;
- publish the five self-contained bundles plus the Linux package set; do not claim
  distro/Flathub availability until the license review explicitly clears it;
- keep the .NET 11 runtime embedded (MIT) and include its notice;
- keep the OpenAL Soft notice, source offer, relinking instructions, and license text in
  every bundle;
- keep the Windows app-local MSVC runtime and its notice;
- do not describe the build as an officially licensed 3DO product;
- record the final license decision, 4DO-database resolution, and channel eligibility in
  this document before the next release.

The engineering side of the release gate is tracked in
[ReleaseChecklist.md](ReleaseChecklist.md). This document covers only the legal and
attribution portion.

## 5. Sign-off

| Sign-off | Name | Date | Scope |
|---|---|---|---|
| Release owner | Project owner (purelogiccode) | 2026-11-02 | Initial v1.0 donationware release |
| Legal / distribution | Approved: FreeDO modified GNU LGPL, donationware | 2026-11-02 | Initial v1.0 release |
| Attribution check | FreeDO/4DO/Opera, OpenAL Soft, Microsoft Visual C++, CHDSharp, SharpCompress, HQx, and NuGet notices shipped; .NET 11 embedded | 2026-11-22 | 1.17.x bundle layout |
| Bundle layout | Self-contained single-file CLI + desktop app; no separate runtime folder; Windows MSVC runtime app-local | 2026-11-20 | 1.17.x bundles |
| License/relicense review | **Open** | 2026-11-25 | `REL-13`; no new license committed |
| 4DO database provenance | **Open** | 2026-11-25 | Permission or replacement pending |
| Code signing / notarization | **Open** | 2026-11-25 | `UX-16`; certificate/account pending |
| RetroAchievements hardcore | **Awaiting external review** | 2026-11-25 | `REL-14`; request submitted |

The current release remains governed by the 2026-11-02 terms until the open rows are
closed and the root `LICENSE` is updated.
