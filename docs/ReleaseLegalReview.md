# Release Legal and Attribution Review

**Date:** 2026-10-29 (OpenAL Soft notice/source offer added)
**Status:** engineering audit complete; **owner/legal sign-off pending**
**Release candidate:** 1.0.0-rc.1 (documented-compatibility scope)

This is an engineering audit, not legal advice. It records what the release
candidate contains, what was verified, and the decisions that remain before a public
v1.0 release.

---

## 1. What the release candidate contains

The portable archives contain only:

- the published CLI or frontend binaries and their runtime dependencies,
- `README.md`,
- `LICENSE`,
- `THIRD-PARTY-NOTICES.md`,
- `docs/Licensing.md`,
- `docs/OpenALSoft.md` (OpenAL Soft notice, source offer, and relinking rights),
- `docs/licenses/OpenAL-Soft-COPYING.txt` (the LGPL-2.0 license text),
- `PACKAGE.txt`,
- the Linux `.desktop` entry for Linux packages.

They do **not** contain `References/`, `artifacts/`, `bios/`, test corpora, disc
images, BIOS ROMs, extracted assets, screenshots, or WAV captures.

## 2. Verification performed

- [x] `git ls-files` contains no `.bin`, `.chd`, `.iso`, `.cue`, `.wav`, `.rom`,
      `.3do`, `.png`, or `.jpg` files.
- [x] `bios/` and `artifacts/` are ignored by `.gitignore` and are not copied by
      `packaging/package.ps1`.
- [x] The root `LICENSE` records the FreeDO modified GNU LGPL: the FreeDO notice is
      reproduced verbatim, the GNU LGPL 2.1 text is included, and the additional terms
      are carried forward. `packaging/package.ps1` copies `LICENSE` into each archive.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO, 4DO, Opera/libopera, xBR-lv2, HQx,
      CHDSharp, VideoGameFileSystemParser, Avalonia, CommunityToolkit.Mvvm,
      Microsoft.Extensions.*, System.CommandLine, Silk.NET/OpenAL, xUnit,
      BenchmarkDotNet, and the now-bundled OpenAL Soft 1.23.1 runtime.
- [x] The OpenAL Soft native runtime is included for all six RIDs through
      `Silk.NET.OpenAL.Soft.Native`; Windows copies `soft_oal.dll` as `OpenAL32.dll`.
      This adds an LGPL-2.0-or-later binary-distribution obligation to the release.
      `docs/OpenALSoft.md` records the notice, the exact upstream 1.23.1 source,
      the three-year written source offer, and the relinking/replacement
      instructions; `docs/licenses/OpenAL-Soft-COPYING.txt` contains the complete
      LGPL-2.0 text. Both files are copied into every portable archive by
      `packaging/package.ps1`.
- [x] The desktop About dialog reports the version, renderer state, and the
      FreeDO modified GNU LGPL notice, and states that no BIOS ROMs or game images are
      included.
- [x] The CLI prints the product/version and does not bundle BIOS/disc content.
- [x] FreeDO/4DO/Opera source headers and attribution remain in the repository.

## 3. Open decisions (release blockers)

- [x] **Distribution model and project license.** The root `LICENSE` now records the
      project license as the FreeDO modified GNU LGPL: the FreeDO notice is reproduced
      verbatim, the GNU LGPL 2.1 text is the base, and the additional terms are carried
      forward. The upstream FreeDO/4DO terms include a non-commercial restriction and a
      requirement for full credits, so the project remains non-commercial and
      source-available. The project's own license is not more permissive than the
      upstream terms allow.
- [ ] **Rights-holder contact.** Ask the FreeDO/4DO maintainers to clarify port and
      binary-distribution terms, and keep the correspondence.
- [ ] **Commercial-use position.** Do not sell builds or bundle them with commercial
      products without written permission.
- [ ] **Source availability.** Confirm how corresponding source will be offered for
      distributed binaries, as required by the LGPL/GPL terms.
- [x] **OpenAL Soft LGPL-2.0-or-later runtime obligation.** The engineering side is
      complete: notice and copyright, the complete LGPL-2.0 text, the upstream 1.23.1
      source location, a three-year written source offer, and relinking/replacement
      instructions are in `docs/OpenALSoft.md` and
      `docs/licenses/OpenAL-Soft-COPYING.txt`, and both files are packaged. The
      owner/legal reviewer must still accept the written offer and contact details
      before distribution.
- [ ] **Remaining third-party license review.** Confirm the CHDSharp LGPL-2.1
      component, the HQx LGPL-2.1 filter obligations, and the 4DO game database terms
      (the 4DO tree has no LICENSE file or source headers). Decide whether any of
      these remain in a binary distribution that cannot comply.
- [ ] **Trademark review.** "3DO", "Panasonic", and "GoldStar" are trademarks of their
      respective owners; the 5DO name and store listing must not imply endorsement.

## 4. Recommendation

Until the owner/legal review is signed off:

- keep the root `LICENSE` and the FreeDO modified GNU LGPL terms in every distribution,
- distribute the **source** and release-candidate artifacts as **non-commercial,
  source-available** builds,
- keep full FreeDO/4DO/Opera attribution in the README, About box, and release notes,
- ship the portable archives only, with no installers or store listings,
- do not describe the build as an officially licensed 3DO product.

The engineering side of the release gate is tracked in
[`ReleaseChecklist.md`](ReleaseChecklist.md). This document covers only the legal and
attribution portion.

## 5. Sign-off

| Item | Name | Date |
|---|---|---|
| Release owner | | |
| Legal / distribution | | |
| Attribution check | | |
