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
  `FiveDo.Video.OpenGL/Shaders.cs`, `FiveDo.Video.Vulkan/Xbr.frag`, and
  `FiveDo.Video.Direct3D/Shaders.cs`. The shader was adapted to the emulator's
  native-resolution palette output and uniform layout; the copyright and permission
  notice is preserved in the shader comments.

### MMPX (Morgan McGuire, Mara Gagiu)

- Source algorithm: Morgan McGuire and Mara Gagiu, "MMPX Style-Preserving
  Pixel Art Magnification" (Journal of Computer Graphics Techniques, 2021).
- License: MIT.
- Use: the opt-in `VideoFilter.Mmpx` 2x pass in
  `FiveDo.Video.OpenGL/Shaders.cs`, `FiveDo.Video.Vulkan/Mmpx.frag`,
  `FiveDo.Video.Direct3D/Shaders.cs`, and the CPU reference
  `FiveDo.Video/MmpxFilter.cs`. The copyright and MIT permission notice is
  preserved in the shader and filter comments.

### FidelityFX Super Resolution 1.0 (AMD)
- Source algorithm: Advanced Micro Devices, Inc., FidelityFX FSR 1.0 (`ffx_fsr1.h`,
  v1.20210629).
- License: MIT.
- Use: the opt-in `VideoFilter.Fsr` presenter path in
  `FiveDo.Video.OpenGL/Shaders.cs`, `FiveDo.Video.Vulkan/FsrEasu.frag`/`FsrRcas.frag`,
  and `FiveDo.Video.Direct3D/Shaders.cs`. The EASU and RCAS passes are adapted to the
  emulator's two-stage presenter and use integer texel taps. The AMD copyright and MIT
  permission notice is preserved in the shader comments. FSR 2/3 and DLSS are not
  used because they require motion vectors and engine integration that a 3DO
  framebuffer does not provide.
- FSR 1.0 is a spatial post-process only; it does not change emulated output.

### OpenAL Soft

- Version: 1.23.1, bundled through `Silk.NET.OpenAL.Soft.Native` 1.23.1.
- License: GNU Library General Public License 2.0 or later
  (LGPL-2.0-or-later); the complete text is in
  [`docs/OpenAL-Soft-COPYING.txt`](docs/OpenAL-Soft-COPYING.txt).
- Copyright: OpenAL Soft is Copyright (C) 1999-2007 by authors and is
  distributed under the LGPL-2.0-or-later.
- Use: the native OpenAL runtime loaded by `FiveDo.Audio` for live playback.
  The binary is unmodified. Windows ships `soft_oal.dll` and `OpenAL32.dll`
  (the same binary under the loader name); Linux ships `libopenal.so`; macOS
  ships `libopenal.dylib`.
- Source offer and relinking instructions: [`docs/OpenALSoft.md`](docs/OpenALSoft.md).
  The corresponding source is the unmodified upstream 1.23.1 release
  (<https://github.com/kcat/openal-soft>); 5DO also offers the complete source
  on request through the project issue tracker. The library is dynamically
  loaded, so users may replace it with a compatible build without rebuilding 5DO.

### MoonSharp

- Package: `MoonSharp` 2.0.0.
- License: 3-clause BSD.
- Use: the sandboxed Lua interpreter in `FiveDo.Scripting` for deterministic TAS input
  authoring. Scripts run under MoonSharp's hard sandbox preset and emit controller
  entries; no Lua code or MoonSharp source is modified. MoonSharp is not used on the
  emulation hot path.

### Serilog

- Packages: `Serilog` 4.4.0, `Serilog.Sinks.Console` 6.1.1, and
  `Serilog.Sinks.File` 7.0.0.
- License: Apache-2.0.
- Use: structured logging in `FiveDo.Diagnostics.Logging`. The rolling file,
  stderr console, in-memory viewer, and asynchronous bug-report sinks all consume
  the same Serilog event stream. No Serilog source is modified.

### HQx (Maxim Stepin, Cameron Zemek)

- Source: `References/4DO-master/FreeDOCore/Filters/hq2x.cpp`, `hq3x.cpp`,
  `hq4x.cpp`, and `hqx_common.h`.
- Authors: Maxim Stepin (maxst@hiend3d.com), Cameron Zemek (grom@zeminvaders.net).
- License: GNU Lesser General Public License 2.1 or later.
- Use: the opt-in `VideoFilter.Hqx`/`Hqx3x`/`Hqx4x` filters in
  `FiveDo.Video/HqxFilter.cs` and the generated `FiveDo.Video/HqxRules.cs`
  table. The interpolation helpers and table structure are ported; the
  original copyright and license notice is preserved in the source header.
  The filters are opt-in and can be removed from a distribution that cannot
  comply with the LGPL.

## Planned runtime and build dependencies

| Dependency | Purpose | License (verify before release) |
|---|---|---|
| .NET 11 / runtime | Platform; **required on the host, not bundled** in release artifacts | MIT |
| Avalonia UI | Desktop frontend | MIT |
| Avalonia.Desktop / Themes.Fluent / Fonts.Inter | Desktop frontend | MIT |
| CommunityToolkit.Mvvm | ViewModels | MIT |
| Microsoft.Extensions.DependencyInjection | Frontend composition | MIT |
| System.CommandLine | CLI parsing | MIT |
| Serilog / Serilog.Sinks.Console / Serilog.Sinks.File | Structured logging, console, and rolling-file sinks | Apache-2.0 |
| MoonSharp 2.0.0 | Sandboxed Lua input scripting for TAS movies | BSD-3-Clause |
| xUnit | Tests | Apache-2.0 |
| Microsoft.NET.Test.Sdk | Tests | MIT |
| coverlet.collector | Coverage | MIT |
| BenchmarkDotNet | Benchmarks | MIT |
| Silk.NET.Vulkan 2.23.0 + Silk.NET.Vulkan.Extensions.KHR 2.23.0 | Optional offscreen Vulkan backend, native Win32 swapchain probe, and `--vk-probe`/`--vk-window-probe` | MIT |
| Vortice.Direct3D11 3.8.3 + Vortice.DXGI 3.8.3 + Vortice.D3DCompiler 3.8.3 | Optional Direct3D 11 presenter, DXGI swapchain, and runtime HLSL compilation; SharpGen.Runtime is a transitive dependency | MIT |
| XenoAtom.ShaderCompiler.Build 1.2.2 | Build-time GLSL-to-SPIR-V compilation for the Vulkan backend; not distributed at runtime | BSD-2-Clause |
| Silk.NET.OpenAL 2.23.0 | Desktop audio bindings | MIT |
| Silk.NET.OpenAL.Soft.Native 1.23.1 | Native OpenAL Soft runtime bundled beside the CLI/frontend (win-x64/win-arm64/win-x86, linux-x64/linux-arm64, osx-x64/osx-arm64); `soft_oal.dll` is copied as `OpenAL32.dll` on Windows | MIT wrapper for OpenAL Soft; OpenAL Soft itself is LGPL-2.0-or-later. The notice, source offer, relinking rights, and license text ship in [`docs/OpenALSoft.md`](docs/OpenALSoft.md) and [`docs/OpenAL-Soft-COPYING.txt`](docs/OpenAL-Soft-COPYING.txt). |
| OS XInput / Linux evdev | Gamepad input (optional, no bundled native library) | OS API; no third-party runtime dependency |
| CHDSharp 1.5.2 | CHD disc images (v1) | MIT + LGPL-2.1 component; pure C#, no native dependency. Verify the package LICENSE before release. |
| 4DO game database | Game IDs and fix flags | Ported with attribution from `References/4DO-master`. The 4DO tree has no LICENSE file or source headers; permission or replacement is required before release. |

## Project license

5DO is distributed under the FreeDO modified GNU LGPL. The root [`LICENSE`](LICENSE)
reproduces the FreeDO notice verbatim, includes the GNU LGPL 2.1 text, and records the
additional project terms. The FreeDO additional terms take precedence where they
conflict with the LGPL.

Release builds are **donationware**: non-commercial, source-available, and funded by
voluntary donations rather than sales. Commercial use requires prior written approval
from the FreeDO rights holders; the owner/legal sign-off is tracked in
[`docs/ReleaseLegalReview.md`](docs/ReleaseLegalReview.md). Donations do not grant a
license or commercial rights. Keep the FreeDO/4DO/Opera attribution intact.

## Assets and content

- No BIOS ROMs, game discs, screenshots of copyrighted games, or other 3DO content are included.
- The UI uses original artwork or permissively licensed assets only.
- Test fixtures are synthetic unless explicitly marked as user-provided.

## Disclaimer

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. This project is not affiliated with or endorsed by them.

This notice is not legal advice. Verify every dependency's license before distributing binaries.
