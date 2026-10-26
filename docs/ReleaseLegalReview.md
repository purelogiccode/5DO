# Release Legal and Attribution Review

**Date:** 2026-10-26
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
- `THIRD-PARTY-NOTICES.md`,
- `docs/Licensing.md`,
- `PACKAGE.txt`,
- the Linux `.desktop` entry for Linux packages.

They do **not** contain `References/`, `artifacts/`, `bios/`, test corpora, disc
images, BIOS ROMs, extracted assets, screenshots, or WAV captures.

## 2. Verification performed

- [x] `git ls-files` contains no `.bin`, `.chd`, `.iso`, `.cue`, `.wav`, `.rom`,
      `.3do`, `.png`, or `.jpg` files.
- [x] `bios/` and `artifacts/` are ignored by `.gitignore` and are not copied by
      `packaging/package.ps1`.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO, 4DO, Opera/libopera, xBR-lv2, HQx,
      CHDSharp, VideoGameFileSystemParser, Avalonia, CommunityToolkit.Mvvm,
      Microsoft.Extensions.*, System.CommandLine, Silk.NET/OpenAL, xUnit,
      BenchmarkDotNet, and the optional OpenAL Soft runtime.
- [x] The desktop About dialog reports the version, renderer state, and the
      FreeDO/4DO licensing warning, and states that no BIOS ROMs or game images are
      included.
- [x] The CLI prints the product/version and does not bundle BIOS/disc content.
- [x] FreeDO/4DO/Opera source headers and attribution remain in the repository.

## 3. Open decisions (release blockers)

- [ ] **Distribution model and project license.** The repository still has no root
      `LICENSE` file. The upstream FreeDO/4DO terms are a modified GNU LGPL with a
      non-commercial restriction and a requirement for full credits. The project's own
      license cannot be more permissive than the upstream terms allow.
- [ ] **Rights-holder contact.** Ask the FreeDO/4DO maintainers to clarify port and
      binary-distribution terms, and keep the correspondence.
- [ ] **Commercial-use position.** Do not sell builds or bundle them with commercial
      products without written permission.
- [ ] **Source availability.** Confirm how corresponding source will be offered for
      distributed binaries, as required by the LGPL/GPL terms.
- [ ] **Third-party license review.** Confirm the CHDSharp LGPL-2.1 component and the
      HQx LGPL-2.1 filter obligations, and decide whether the HQx filters remain in a
      binary distribution that cannot comply.
- [ ] **Trademark review.** "3DO", "Panasonic", and "GoldStar" are trademarks of their
      respective owners; the 5DO name and store listing must not imply endorsement.

## 4. Recommendation

Until the owner/legal review is signed off:

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
