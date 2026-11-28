# Licensing and Distribution Notes

> This is an engineering note, not legal advice. Review the actual licenses and, if you intend to distribute the emulator, consult a lawyer.

**Distribution model:** 5DO is **donationware** — non-commercial, source-available, and
funded by voluntary donations rather than sales. Donations are not a license purchase
and do not grant commercial rights. The upstream FreeDO/4DO terms still control what
may be distributed.

**2026-11-25:** the `REL-13` license-compliance and relicense review is complete. The
project **retains the FreeDO modified GNU LGPL** and the non-commercial donationware
model. Written rights-holder permission has not been obtained and a clean-room replacement
has not been performed for this release. The 4DO game database is a separate **GPL-2.0**
data component; its complete text ships in [`GPL-2.0.txt`](GPL-2.0.txt). Official Debian,
Ubuntu, Fedora, openSUSE, and Flathub channels remain **not eligible** under the retained
terms. The current release is governed by the root [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE).

## 1. The FreeDO/4DO source is not under a standard OSI license

The FreeDOCore source headers (`References/4DO-master/FreeDOCore/FreeDO/*.cpp`, `*.h`) contain the following notice:

- The FreeDO is licensed under a **modified GNU LGPL**.
- The owners and original authors reserve the right to develop closed-source derivative work.
- Any **non-commercial** use of the sources or knowledge obtained from them must be accompanied by **full credits**.
- Any **commercial** use is **strictly forbidden** without the owners' approval.
- These notes take precedence over the GNU LGPL where they conflict.

This is a restrictive license. It is not the same as "LGPL v2.1 or later" and it is not an OSI-approved license. The 4DO project itself is listed as GPLv2 by its distribution page and SourceForge metadata, but its FreeDO-derived core retains the FreeDO modified LGPL. The project license decision is recorded in the root [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE): 5DO is distributed under the FreeDO modified GNU LGPL, with the FreeDO notice reproduced verbatim and the GNU LGPL 2.1 text as the base license. The 4DO game database is a separate GPL-2.0 data component; see §1b.

### 1a. Opera/libopera

`References/opera-libretro-master` is a maintained fork of 4DO/FreeDO and inherits
the same modified GNU LGPL with the non-commercial restriction. It is now the primary
behavioral reference for new compatibility work. That does not change the project's
license position, but it adds Opera to the attribution and review list. If Opera code
or knowledge is used, preserve its headers and credit the Opera/libretro
contributors in addition to FreeDO and 4DO.

### 1b. 4DO game database (GPL-2.0)

The 4DO project distributes its source under **GNU GPL version 2** according to its
download page and SourceForge metadata. The embedded
`src/FiveDo.Storage/Data/GameDatabase.xml` is a separate GPL-2.0 data component derived
from the 4DO database. The 5DO-generated records and extension fields in that file are
contributed under the same GPL-2.0 terms. The complete license text ships in
[`GPL-2.0.txt`](GPL-2.0.txt) and is copied into every bundle. The database is read at
runtime and is not linked into the emulator code; the emulator code remains under the
FreeDO modified GNU LGPL.

## 2. What this means for the project

The project license is the FreeDO modified GNU LGPL recorded in the root
[`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE). The 2026-11-25
`REL-13` review retained that license: replacing the upstream restrictions would need
written rights-holder permission or a clean-room reimplementation, neither of which has
been secured for this release. The 4DO game database is separately licensed under GPL-2.0
as described in §1b. The retained terms mean:

- Treat the project as **donationware**: non-commercial, source-available, and funded by voluntary donations rather than sales.
- Keep **full attribution** to FreeDO and 4DO in the source, README, About box, and release notes.
- Preserve the original copyright/license headers in ported files (adapted to C# comments).
- Publish the corresponding source for any distributed binaries if the LGPL/GPL terms require it.
- Do **not** sell builds, licenses, support, or access; do not bundle builds with commercial products or use them in a commercial service without written permission.
- Do **not** describe a donation as purchasing a license or commercial rights.
- Do **not** include BIOS ROMs, game discs, or other copyrighted 3DO material in the repository or release artifacts.

## 3. Ongoing compliance actions

1. **Keep the `REL-13` decision current.** The retained FreeDO modified GNU LGPL and the
   separate GPL-2.0 database are recorded in the root `LICENSE`,
   [`ReleaseLegalReview.md`](ReleaseLegalReview.md), and this file. Revisit only if a
   rights holder grants broader permission, a clean-room replacement lands, or the
   project changes its distribution model.
2. Keep the project license aligned with the upstream terms. It cannot be more permissive than the upstream terms allow.
3. Keep the root [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE), [`GPL-2.0.txt`](GPL-2.0.txt), and [`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md) current when dependencies or upstream terms change.
4. Add an in-app **About / Licenses** page listing FreeDO, 4DO, Avalonia, and other dependencies.
5. Have a lawyer review the final distribution model if the project is published commercially or bundled with hardware.

## 4. Attribution requirements

At minimum, preserve:

- The FreeDO project authors listed in the source headers:
  Alexander Troosh, Maxim Grishin, Allen Wright, John Sammons, Felix Lazarev.
- The FreeDO website/project attribution (`www.freedo.org`).
- The 4DO project attribution and its contributors.
- The 4DO game database GPL-2.0 notice and the complete text in `GPL-2.0.txt`.
- The Opera/libopera project attribution and its contributors, including the
  `opera-libretro` and `opera-boot` work where applicable.
- The original file headers in ported source files.
- A statement that the C# port is a derivative work of FreeDO/4DO and uses Opera as a
  behavioral reference.

Suggested About text:

> This emulator is a C# port of the FreeDO/4DO 3DO emulation core. FreeDO is Copyright its authors and is used under the terms in the original source headers. 4DO is Copyright its contributors. Opera/libopera is Copyright its contributors and is used as a behavioral reference under the same upstream terms. This project is not affiliated with or endorsed by Panasonic or the 3DO Company.

## 5. Third-party dependencies

The planned dependencies have their own licenses. Record them in `THIRD-PARTY-NOTICES.md` as they are added:

- Avalonia UI (MIT)
- .NET 11 runtime (MIT; embedded in the self-contained single-file apphosts)
- Microsoft Visual C++ Redistributable (`vcruntime140.dll` and `msvcp140.dll`, plus
  the x64-only `vcruntime140_1.dll` helper; app-local in the Windows bundles under the
  Microsoft Visual Studio redistributable terms)
- CommunityToolkit.Mvvm (MIT)
- Microsoft.Extensions.* (MIT)
- xUnit (Apache-2.0)
- Serilog, Serilog.Sinks.Console, Serilog.Sinks.File (Apache-2.0)
- MoonSharp 2.0.0 (BSD-3-Clause; sandboxed Lua input scripting)
- BenchmarkDotNet (MIT)
- Silk.NET (MIT), including Silk.NET.OpenGL, Silk.NET.OpenAL, and Silk.NET.Vulkan
- XenoAtom.ShaderCompiler.Build (BSD-2-Clause; build-time GLSL-to-SPIR-V compiler,
  not distributed at runtime)
- rcheevos (MIT; the managed RetroAchievements evaluation engine and 3DO hash are C#
  ports; attribution and the original notice are preserved in `THIRD-PARTY-NOTICES.md`
  and `docs/RetroAchievements.md`)
- xBR-lv2 shader algorithm by Hyllian (MIT; attribution preserved in the shader and
  `THIRD-PARTY-NOTICES.md`)
- MMPX 2x filter by Morgan McGuire and Mara Gagiu (MIT; attribution preserved in the
  shader, `MmpxFilter.cs`, and `THIRD-PARTY-NOTICES.md`)
- HQx 2x/3x/4x filters by Maxim Stepin and Cameron Zemek (LGPL-2.1+; attribution
  preserved in `HqxFilter.cs` and `THIRD-PARTY-NOTICES.md`)
- OS XInput / Linux evdev / macOS IOKit HID gamepad APIs (no bundled third-party runtime)
- OpenAL Soft 1.23.1 (LGPL-2.0-or-later; bundled in the Windows/Linux x64+arm64 and
  macOS arm64 bundles through `Silk.NET.OpenAL.Soft.Native`, including Windows
  `OpenAL32.dll`). The
  LGPL notice, complete license text, written source offer, and relinking
  instructions ship in [`OpenALSoft.md`](OpenALSoft.md) and
  [`OpenAL-Soft-COPYING.txt`](OpenAL-Soft-COPYING.txt); the
  release review is tracked in [`ReleaseLegalReview.md`](ReleaseLegalReview.md).
- CHDSharp 1.7.2 (MIT plus an LGPL-2.1 component; pure C#; package license reviewed for
  the 1.19.0 dependency set and unchanged for 1.21.0)
- MDSSharp 1.3.0 (MIT; pure managed Alcohol MDS/MDF/MDX reader)
- CCDSharp 1.0.3 (MIT; pure managed CloneCD CCD/IMG/SUB reader)
- FFmpeg (optional, **not bundled or redistributed**): MP4 recording invokes a
  user-supplied FFmpeg executable as a separate process. The desktop and
  `5do ffmpeg install` can download a pinned upstream build at the user's request,
  verify its SHA-256, and save it locally; 5DO ships no FFmpeg binary or codec. The
  pinned Windows/Linux/macOS builds include libx264 and are therefore GPL-2.0-or-later;
  users are responsible for the licence and codec-patent terms of the build they install.
- 4DO game database (GPL-2.0; separate data component derived from the 4DO project;
  complete text in [`GPL-2.0.txt`](GPL-2.0.txt); the 5DO-generated records and
  extension fields are under the same terms; read at runtime, not linked into the
  emulator code)

## 6. Trademarks

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. The project is named 5DO. Do not imply endorsement.
