# Release Legal and Attribution Review

**Date:** 2026-11-02 (donationware model and project-owner sign-off recorded)
**Status:** engineering audit complete; **project-owner legal/attribution sign-off
recorded**; v1.0 is a documented-compatibility release. M14 is post-1.0 research.
**Distribution model:** **donationware** — non-commercial, source-available, voluntary
donations only (no sales)
**Release:** 1.0.0 (documented-compatibility scope)

This is an engineering audit, not legal advice. It records what the release contains,
what was verified, and the decisions recorded for the public v1.0 release.

---

## 1. What the release contains

The release is **one bundle per system**: `5do-win-x64.zip`,
`5do-linux-x64.tar.gz`, and `5do-osx-x64.tar.gz`. Each bundle contains only:

- the framework-dependent single-file CLI and desktop apphosts and their native
  dependencies,
- `README.md`,
- `whatsnew.md`,
- `LICENSE`,
- `THIRD-PARTY-NOTICES.md`,
- `docs/Licensing.md`,
- `docs/OpenALSoft.md` (OpenAL Soft notice, source offer, and relinking rights),
- `docs/licenses/OpenAL-Soft-COPYING.txt` (the LGPL-2.0 license text),
- `PACKAGE.txt`,
- the release notes,
- the Linux `.desktop` entry for the Linux bundle.

They do **not** contain `References/`, `artifacts/`, `bios/`, test corpora, disc
images, BIOS ROMs, extracted assets, screenshots, WAV captures, the .NET runtime, PDB
files, XML documentation files, or any FreeDO/4DO/Opera reference binary.

## 2. Verification performed

- [x] `git ls-files` contains no `.bin`, `.chd`, `.iso`, `.cue`, `.wav`, `.rom`,
      `.3do`, `.png`, or `.jpg` files.
- [x] `bios/` and `artifacts/` are ignored by `.gitignore` and are not copied by
      `packaging/package-bundle.ps1`.
- [x] The root `LICENSE` records the FreeDO modified GNU LGPL: the FreeDO notice is
      reproduced verbatim, the GNU LGPL 2.1 text is included, and the additional terms
      are carried forward. `packaging/package-bundle.ps1` copies `LICENSE` into each
      bundle.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO, 4DO, Opera/libopera, xBR-lv2, HQx,
      CHDSharp, Avalonia, CommunityToolkit.Mvvm,
      Microsoft.Extensions.*, System.CommandLine, Silk.NET/OpenAL/Vulkan,
      XenoAtom.ShaderCompiler.Build, xUnit, BenchmarkDotNet, the .NET 10 runtime, and
      the bundled OpenAL Soft 1.23.1 runtime.
- [x] The OpenAL Soft native runtime is included in the Windows, Linux, and macOS x64
      bundles through `Silk.NET.OpenAL.Soft.Native`; Windows copies `soft_oal.dll` as
      `OpenAL32.dll`. This adds an LGPL-2.0-or-later binary-distribution obligation to
      the release. `docs/OpenALSoft.md` records the notice, the exact upstream 1.23.1
      source, the three-year written source offer, and the relinking/replacement
      instructions; `docs/licenses/OpenAL-Soft-COPYING.txt` contains the complete
      LGPL-2.0 text. Both files are copied into every bundle by
      `packaging/package-bundle.ps1`. The native library stays outside the single-file
      apphosts, so users can replace it.
- [x] The .NET 10 runtime is a **host prerequisite** and is not redistributed in the
      bundles. Users install it from Microsoft; the .NET runtime is MIT licensed and is
      listed in `THIRD-PARTY-NOTICES.md`.
- [x] The desktop About dialog reports the version, renderer state, and the
      FreeDO modified GNU LGPL notice, and states that no BIOS ROMs or game images are
      included.
- [x] The CLI prints the product/version and does not bundle BIOS/disc content.
- [x] FreeDO/4DO/Opera source headers and attribution remain in the repository.
- [x] The bundles contain no `References/` file, no FreeDO/4DO/Opera binary, no .NET
      runtime, no PDB or XML documentation files, and no BIOS ROM, disc image, extracted
      asset, screenshot, or WAV capture.

## 3. Decisions and residual risks

- [x] **Distribution model and project license.** The root `LICENSE` now records the
      project license as the FreeDO modified GNU LGPL: the FreeDO notice is reproduced
      verbatim, the GNU LGPL 2.1 text is the base, and the additional terms are carried
      forward. The upstream FreeDO/4DO terms include a non-commercial restriction and a
      requirement for full credits, so the project remains **donationware**:
      non-commercial, source-available, and funded by voluntary donations rather than
      sales. The project's own license is not more permissive than the upstream terms
      allow.
- [x] **Donationware position recorded.** The project accepts voluntary donations for
      development costs but does not sell builds, licenses, support, or access. Donation
      tiers must not be described as purchasing a license or commercial rights.
- [x] **Confirm donationware with the rights holders.** Project-owner decision recorded
      2026-11-02: distribute under the FreeDO modified GNU LGPL as free, open-source
      donationware (non-commercial; voluntary donations only). External rights-holder
      correspondence is not a v1.0 gate; revisit before any commercial use.
- [x] **Rights-holder contact.** Not required for the non-commercial v1.0 distribution;
      revisit if the distribution model changes.
- [x] **Commercial-use position.** Do not sell builds or bundle them with commercial
      products without written permission.
- [x] **Source availability.** The source is public on GitHub and the LGPL source
      offer/relinking record ships with the OpenAL Soft runtime.
- [x] **Remaining third-party license review.** CHDSharp and HQx LGPL obligations,
      the 4DO game database attribution, and the bundled OpenAL Soft notice are
      recorded in `THIRD-PARTY-NOTICES.md` and the release archives. Accepted as part
      of the project-owner sign-off.
- [x] **Trademark review.** "3DO", "Panasonic", and "GoldStar" are trademarks of their
      respective owners; the 5DO name and release notes do not imply endorsement.
- [x] **OpenAL Soft LGPL-2.0-or-later runtime obligation.** The engineering side is
      complete: notice and copyright, the complete LGPL-2.0 text, the upstream 1.23.1
      source location, a three-year written source offer, and relinking/replacement
      instructions are in `docs/OpenALSoft.md` and
      `docs/licenses/OpenAL-Soft-COPYING.txt`, and both files are packaged. Accepted
      as part of the project-owner sign-off on 2026-11-02.

## 4. Recommendation

Owner/legal review is signed off (2026-11-02). Keep the following practices:

- keep the root `LICENSE` and the FreeDO modified GNU LGPL terms in every distribution,
- distribute the **source** and release artifacts as **donationware**
  (non-commercial, source-available) builds; donations are voluntary and do not grant
  commercial rights,
- keep full FreeDO/4DO/Opera attribution in the README, About box, and release notes,
- ship the unified bundles only, with no installers or store listings,
- document that the .NET 10 runtime is a host prerequisite and do **not** bundle it,
- keep the OpenAL Soft notice and license text in every bundle,
- do not describe the build as an officially licensed 3DO product.

The engineering side of the release gate is tracked in
[`ReleaseChecklist.md`](ReleaseChecklist.md). This document covers only the legal and
attribution portion.

## 5. Sign-off

| Sign-off | Name | Date |
|---|---|---|
| Release owner | Project owner (purelogiccode) | 2026-11-02 |
| Legal / distribution | Approved: FreeDO modified GNU LGPL, donationware | 2026-11-02 |
| Attribution check | FreeDO/4DO/Opera, OpenAL Soft, CHDSharp, HQx, and NuGet notices shipped; .NET is a host prerequisite | 2026-11-02 |
| Bundle layout | Framework-dependent single-file CLI + desktop app; no bundled .NET runtime | 2026-11-02 |
