# Third-Party Notices

This file records the projects and licenses that influence or ship with 3DOEmulator. It is updated as dependencies are added.

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
- Used as a secondary reference for structure, comments, and hardware behavior. Primary behavior remains FreeDO/4DO.

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
| Silk.NET.OpenAL | Desktop audio (optional) | MIT |
| OpenAL Soft | Native audio runtime (optional) | LGPL |
| Silk.NET.Input / SDL2 | Gamepad input (optional) | MIT / zlib |
| CHDSharp 1.5.2 | CHD disc images (v1) | MIT + LGPL-2.1 component; pure C#, no native dependency. Verify the package LICENSE before release. |
| VideoGameFileSystemParser 1.4.0 | 3DO Opera FS listing/extraction in `3do fs` | MIT; depends on CHDSharp. Written by the 3DOEmulator author. |
| 4DO game database | Game IDs and fix flags | Derived with attribution; verify |

## Assets and content

- No BIOS ROMs, game discs, screenshots of copyrighted games, or other 3DO content are included.
- The UI uses original artwork or permissively licensed assets only.
- Test fixtures are synthetic unless explicitly marked as user-provided.

## Disclaimer

"3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. This project is not affiliated with or endorsed by them.

This notice is not legal advice. Verify every dependency's license before distributing binaries.
