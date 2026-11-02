# OpenAL Soft runtime notice and source offer

**Component:** OpenAL Soft 1.23.1 (unmodified upstream release)
**Used by:** `FiveDo.Audio`, the CLI, and the Avalonia frontend
**License:** GNU Library General Public License, version 2, or later
(LGPL-2.0-or-later). The full license text is in
[`licenses/OpenAL-Soft-COPYING.txt`](licenses/OpenAL-Soft-COPYING.txt).

This notice is an engineering compliance record, not legal advice. The project
owner/legal reviewer remains responsible for the final distribution decision; see
[`ReleaseLegalReview.md`](ReleaseLegalReview.md).

## 1. What ships

The release packages contain the OpenAL Soft shared library built from the
unmodified 1.23.1 upstream release. The file names are:

| Platform | Runtime file | Loader name |
|---|---|---|
| Windows x64 / arm64 | `soft_oal.dll` (from the NuGet runtime) plus `OpenAL32.dll` (the same binary copied under the name Silk.NET.OpenAL loads) | `OpenAL32.dll` |
| Linux x64 / arm64 | `libopenal.so` | `libopenal.so` |
| macOS x64 / arm64 | `libopenal.dylib` | `libopenal.dylib` |

The Windows copy is performed by `Directory.Build.targets`
(`FiveDoPublishOpenAlSoft`). Linux and macOS use the package's native file names.
The library is loaded dynamically at runtime; 5DO does not statically link it into
the executable.

## 2. Copyright and license notice

OpenAL Soft is Copyright (C) 1999-2007 by authors, and is distributed under the
GNU Library General Public License, version 2, or (at your option) any later
version. The upstream source headers carry the following notice:

> This library is free software; you can redistribute it and/or modify it under
> the terms of the GNU Library General Public License as published by the Free
> Software Foundation; either version 2 of the License, or (at your option) any
> later version.
>
> This library is distributed in the hope that it will be useful, but WITHOUT ANY
> WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
> PARTICULAR PURPOSE. See the GNU Library General Public License for more details.

The complete LGPL-2.0 text is reproduced in
[`licenses/OpenAL-Soft-COPYING.txt`](licenses/OpenAL-Soft-COPYING.txt). It is
copied into every release bundle by `packaging/package-bundle.ps1`.

## 3. Corresponding source offer

The corresponding source is the unmodified OpenAL Soft 1.23.1 release:

- Project: <https://github.com/kcat/openal-soft>
- Tag: `1.23.1`
- Source archive: <https://github.com/kcat/openal-soft/archive/refs/tags/1.23.1.tar.gz>
- License text: <https://github.com/kcat/openal-soft/blob/1.23.1/COPYING>

The binary is distributed by the `Silk.NET.OpenAL.Soft.Native` 1.23.1 package
without modification. The exact upstream source is available from the URLs above.

In addition, 5DO offers, valid for at least three years from the date of the
release that includes this notice, to provide the complete corresponding source
for the bundled OpenAL Soft runtime to any third party, for no charge beyond the
cost of physically performing the source distribution. Requests can be made
through the project issue tracker:

<https://github.com/purelogiccode/5DO/issues>

If the upstream archive is unavailable, the written offer above remains the
source-delivery mechanism.

## 4. Relinking and replacement rights

The OpenAL Soft library is a separate shared library, not statically linked into
the 5DO executable. A user may replace the bundled library with a compatible
OpenAL Soft build (including a modified build) without rebuilding 5DO:

1. Locate the runtime file listed in section 1 beside the CLI and desktop apphosts.
2. Replace it with a compatible build of OpenAL Soft (same ABI and major/minor
   library name).
3. Start 5DO normally. The loader resolves the replacement at process startup.

On Windows, replace both `OpenAL32.dll` and `soft_oal.dll` with the same compatible
build so the loader and the packaged runtime stay consistent. Keep a backup of the
original files if you want to restore the bundled runtime.

No registration, activation, or signature check prevents replacement. The
emulator does not ship a statically linked OpenAL implementation.

## 5. Package contents

Every release bundle built by `packaging/package-bundle.ps1` includes:

- `docs/OpenALSoft.md` (this notice and source offer),
- `docs/licenses/OpenAL-Soft-COPYING.txt` (the LGPL-2.0 text),
- `THIRD-PARTY-NOTICES.md` and `docs/Licensing.md` (the dependency summary).

The `PACKAGE.txt` manifest lists the component, RID, version, commit, and build
time. The release notes list the same files in the archive summary.
