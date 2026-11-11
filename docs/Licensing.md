# Licensing and Distribution Notes

> This is an engineering note, not legal advice. Review the actual licenses and, if you intend to distribute the emulator, consult a lawyer.

**Distribution model:** 5DO is **donationware** — non-commercial, source-available, and
funded by voluntary donations rather than sales. Donations are not a license purchase
and do not grant commercial rights. The upstream FreeDO/4DO terms still control what
may be distributed.

## 1. The FreeDO/4DO source is not under a standard OSI license

The FreeDOCore source headers (`References/4DO-master/FreeDOCore/FreeDO/*.cpp`, `*.h`) contain the following notice:

- The FreeDO is licensed under a **modified GNU LGPL**.
- The owners and original authors reserve the right to develop closed-source derivative work.
- Any **non-commercial** use of the sources or knowledge obtained from them must be accompanied by **full credits**.
- Any **commercial** use is **strictly forbidden** without the owners' approval.
- These notes take precedence over the GNU LGPL where they conflict.

This is a restrictive license. It is not the same as "LGPL v2.1 or later" and it is not an OSI-approved license. The 4DO project may also carry GPL obligations for its own code. The project license decision is recorded in the root [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE): 5DO is distributed under the FreeDO modified GNU LGPL, with the FreeDO notice reproduced verbatim and the GNU LGPL 2.1 text as the base license.

### 1a. Opera/libopera

`References/opera-libretro-master` is a maintained fork of 4DO/FreeDO and inherits
the same modified GNU LGPL with the non-commercial restriction. It is now the primary
behavioral reference for new compatibility work. That does not change the project's
license position, but it adds Opera to the attribution and review list. If Opera code
or knowledge is used, preserve its headers and credit the Opera/libretro
contributors in addition to FreeDO and 4DO.

## 2. What this means for the project

The project license is the FreeDO modified GNU LGPL recorded in the root
[`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE). Until the rights holders grant different terms:

- Treat the project as **donationware**: non-commercial, source-available, and funded by voluntary donations rather than sales.
- Keep **full attribution** to FreeDO and 4DO in the source, README, About box, and release notes.
- Preserve the original copyright/license headers in ported files (adapted to C# comments).
- Publish the corresponding source for any distributed binaries if the LGPL/GPL terms require it.
- Do **not** sell builds, licenses, support, or access; do not bundle builds with commercial products or use them in a commercial service without written permission.
- Do **not** describe a donation as purchasing a license or commercial rights.
- Do **not** include BIOS ROMs, game discs, or other copyrighted 3DO material in the repository or release artifacts.

## 3. Recommended actions before the first public release

1. **Contact the FreeDO/4DO authors** (or the current maintainers) and ask for clarification of the port and distribution terms. Keep the correspondence.
2. Keep the project license aligned with the upstream terms. It cannot be more permissive than the upstream terms allow.
3. Keep the root [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE) and [`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md) current when dependencies or upstream terms change.
4. Add an in-app **About / Licenses** page listing FreeDO, 4DO, Avalonia, and other dependencies.
5. Have a lawyer review the final distribution model if the project is published commercially or bundled with hardware.

## 4. Attribution requirements

At minimum, preserve:

- The FreeDO project authors listed in the source headers:
  Alexander Troosh, Maxim Grishin, Allen Wright, John Sammons, Felix Lazarev.
- The FreeDO website/project attribution (`www.freedo.org`).
- The 4DO project attribution and its contributors.
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
- .NET 11 runtime (MIT; required on the host and **not bundled** in the release artifacts)
- CommunityToolkit.Mvvm (MIT)
- Microsoft.Extensions.* (MIT)
- xUnit (Apache-2.0)
- Serilog, Serilog.Sinks.Console, Serilog.Sinks.File (Apache-2.0)
- BenchmarkDotNet (MIT)
- Silk.NET (MIT), including Silk.NET.OpenGL, Silk.NET.OpenAL, and Silk.NET.Vulkan
- XenoAtom.ShaderCompiler.Build (BSD-2-Clause; build-time GLSL-to-SPIR-V compiler,
  not distributed at runtime)
- xBR-lv2 shader algorithm by Hyllian (MIT; attribution preserved in the shader and
  `THIRD-PARTY-NOTICES.md`)
- MMPX 2x filter by Morgan McGuire and Mara Gagiu (MIT; attribution preserved in the
  shader, `MmpxFilter.cs`, and `THIRD-PARTY-NOTICES.md`)
- HQx 2x/3x/4x filters by Maxim Stepin and Cameron Zemek (LGPL-2.1+; attribution
  preserved in `HqxFilter.cs` and `THIRD-PARTY-NOTICES.md`)
- OS XInput / Linux evdev / macOS IOKit HID gamepad APIs (no bundled third-party runtime)
- OpenAL Soft 1.23.1 (LGPL-2.0-or-later; bundled in the Windows/Linux/macOS x64
  bundles through `Silk.NET.OpenAL.Soft.Native`, including Windows `OpenAL32.dll`). The
  LGPL notice, complete license text, written source offer, and relinking
  instructions ship in [`OpenALSoft.md`](OpenALSoft.md) and
  [`OpenAL-Soft-COPYING.txt`](OpenAL-Soft-COPYING.txt); the
  release review is tracked in [`ReleaseLegalReview.md`](ReleaseLegalReview.md).
- CHDSharp (MIT plus an LGPL-2.1 component; pure C#; verify the package LICENSE before release)
- 4DO game database (derive with attribution; verify terms)

## 6. Trademarks

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. The project is named 5DO. Do not imply endorsement.
