# Third-Party Notices

This file records the projects and licenses that influence or ship with 5DO. It is updated as dependencies are added.

## Emulation reference

### FreeDO

- Source: `References/freedo-master`, `References/4DO-master/FreeDOCore`
- Website: `www.freedo.org`
- Authors: Alexander Troosh, Maxim Grishin, Allen Wright, John Sammons, Felix Lazarev
- License: modified GNU LGPL with additional restrictions. Non-commercial use requires full credits; commercial use requires the owners' approval. See the original source headers and [`docs/Licensing.md`](docs/Licensing.md).

### 4DO

- Source: `References/4DO-master`
- License: see the 4DO project. The C# host code, game database, input mapping, and save-state helpers are used as behavioral references.
- This project is a C# port/derivative of the FreeDO/4DO emulation core and preserves attribution.

### Opera (opera-libretro)

- Source: `References/opera-libretro-master`
- Used as the primary behavioral reference for new compatibility work. The C# port
  remains FreeDO/4DO-based; Opera is used to trace structure, comments, and hardware
  behavior for fixes added after the initial port.

## Shader algorithms

### xBR-lv2 (Hyllian)

- Source algorithm: Hyllian (sergiogdb@gmail.com), xBR-lv2, as published in the
  libretro `glsl-shaders` collection.
- License: MIT.
- Use: the opt-in `VideoFilter.Xbr` 4x edge-directed pass in
  `FiveDo.Video.OpenGL/Shaders.cs`. The shader was adapted to the emulator's
  native-resolution palette output and uniform layout; the copyright and permission
  notice is preserved in the shader comments.

### FidelityFX Super Resolution 1.0 (AMD)

- Source algorithm: Advanced Micro Devices, Inc., FidelityFX FSR 1.0 (`ffx_fsr1.h`,
  v1.20210629).
- License: MIT.
- Use: the opt-in `VideoFilter.Fsr` presenter path in `FiveDo.Video.OpenGL/Shaders.cs`.
  The EASU and RCAS passes are adapted to the emulator's two-stage presenter (GLSL
  3.30 / ES 3.00) and use integer `texelFetch` taps. The AMD copyright and MIT
  permission notice is preserved in the shader comments. FSR 2/3 and DLSS are not
  used because they require motion vectors and engine integration that a 3DO
  framebuffer does not provide.
- FSR 1.0 is a spatial post-process only; it does not change emulated output.

### HQx (Maxim Stepin, Cameron Zemek)

- Source: `References/4DO-master/FreeDOCore/Filters/hq2x.cpp`, `hq3x.cpp`,
  `hq4x.cpp`, and `hqx_common.h`.
- Authors: Maxim Stepin (maxst@hiend3d.com), Cameron Zemek (grom@zeminvaders.net).
- License: GNU Lesser General Public License 2.1 or later.
- Use: the opt-in `VideoFilter.Hqx`/`Hqx3x`/`Hqx4x` filters in
  `FiveDo.Video.OpenGL/HqxFilter.cs` and the generated pattern switches in
  `HqxPatterns.cs`, `HqxPatterns3.cs`, and `HqxPatterns4.cs`. The interpolation
  helpers and table structure are ported; the original copyright and license notice
  is preserved in the source header. The filters are opt-in and can be removed from
  a distribution that cannot comply with the LGPL.

## Planned runtime and build dependencies

| Dependency | Purpose | License (verify before release) |
|---|---|---|
| .NET 10 / runtime | Platform | MIT |
| Avalonia UI | Desktop frontend | MIT |
| Avalonia.Desktop / Themes.Fluent / Fonts.Inter | Desktop frontend | MIT |
| CommunityToolkit.Mvvm | ViewModels | MIT |
| Microsoft.Extensions.DependencyInjection | Frontend composition | MIT |
| System.CommandLine | CLI parsing | MIT |
| xUnit | Tests | Apache-2.0 |
| Microsoft.NET.Test.Sdk | Tests | MIT |
| coverlet.collector | Coverage | MIT |
| BenchmarkDotNet | Benchmarks | MIT |
| Silk.NET.OpenAL 2.23.0 | Desktop audio bindings | MIT |
| Silk.NET.OpenAL.Soft.Native 1.23.1 | Native OpenAL Soft runtime bundled beside the CLI/frontend (win-x64/win-arm64/win-x86, linux-x64/linux-arm64, osx-x64/osx-arm64); `soft_oal.dll` is copied as `OpenAL32.dll` on Windows | MIT wrapper for OpenAL Soft; OpenAL Soft itself is LGPL-2.0-or-later. Source: <https://github.com/kcat/openal-soft>. The release must ship the LGPL notice/source offer; include this item in the legal review. |
| OS XInput / Linux evdev | Gamepad input (optional, no bundled native library) | OS API; no third-party runtime dependency |
| CHDSharp 1.5.2 | CHD disc images (v1) | MIT + LGPL-2.1 component; pure C#, no native dependency. Verify the package LICENSE before release. |
| VideoGameFileSystemParser 1.4.0 | 3DO Opera FS listing/extraction in `5do fs` | MIT; depends on CHDSharp. Written by the 5DO author. |
| 4DO game database | Game IDs and fix flags | Derived with attribution; verify |

## Project license

The project's own distribution license is not yet finalized because the FreeDO/4DO
terms are restrictive and the distribution model is still under review. Until the
owner/legal sign-off is recorded in [`docs/ReleaseLegalReview.md`](docs/ReleaseLegalReview.md),
treat release-candidate builds as **non-commercial, source-available** builds and keep
the FreeDO/4DO/Opera attribution intact.

## Assets and content

- No BIOS ROMs, game discs, screenshots of copyrighted games, or other 3DO content are included.
- The UI uses original artwork or permissively licensed assets only.
- Test fixtures are synthetic unless explicitly marked as user-provided.

## Disclaimer

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. This project is not affiliated with or endorsed by them.

This notice is not legal advice. Verify every dependency's license before distributing binaries.
