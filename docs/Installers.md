# Installers and file associations

5DO ships the portable six-bundle matrix as its primary release artifact; the macOS
bundle itself is a drag-to-Applications DMG. **UX-16** adds unsigned installer/package
scaffolding on top of those bundles. Signing and notarization are conditional: they run
only when the required Windows certificate or Apple Developer ID/keychain profile is
available.

| Platform | Artifact | Script | Status |
|---|---|---|---|
| Windows | `5do-<version>-win-x64-setup.exe`, `win-arm64` | `packaging/windows/build-installer.ps1` | x64 built and install/launch/association-tested; arm64 built (unsigned) |
| Linux | `5do_<version>_amd64.deb`, `5do-<version>-1.x86_64.rpm` | `packaging/linux/build-deb.sh`, `build-rpm.sh` | Built and metadata/MIME-validated (unsigned) |
| macOS | `5do-<version>-osx-<arch>.dmg`, `.pkg`, `.app` | `packaging/macos/build-macos-package.sh` | Built natively in CI; DMG is the published macOS bundle |

The authoritative build instructions and script arguments are in
[`packaging/README.md`](https://github.com/purelogiccode/5DO/blob/master/packaging/README.md).

## File associations

The installed desktop app accepts a positional disc path or `--disc <path>`, which is
the same path the shell passes from a file association.

- **Windows:** Inno Setup registers `5DO.Disc` for `.iso`, `.cue`, `.chd`, `.bin`, `.cdi`, and `.nrg`
  and `5DO.Playlist` for `.m3u`. The association is per-user when the installer is not
  elevated and machine-wide when it is.
- **Linux:** the package installs `/usr/share/mime/packages/5do.xml` with the
  `application/x-3do-disc` MIME type for `.iso`, `.cue`, `.chd`, `.bin`, `.cdi`, and `.nrg`, plus the
  `5do.desktop` entry and `5do` SVG icon. `postinst` refreshes the desktop and MIME
  databases.
- **macOS:** the `.app` declares `com.5do.disc-image` as an exported UTI plus a
  `CFBundleDocumentTypes` viewer entry. Finder opens the selected image with the app.

## Signing and notarization

- **Windows Authenticode:** pass `-SignCommand` to `build-installer.ps1`. The wrapper
  hands it to Inno Setup as the `5doSign` sign tool; the installer and uninstaller are
  signed. Without a certificate the installer is unsigned but fully functional.
- **macOS Developer ID:** pass `--sign-identity` (or set `MACOS_SIGN_IDENTITY`). The
  script signs `5DO.app` with the hardened runtime and the JIT entitlements required by
  the framework-dependent .NET apphost. Pass `--notary-profile` (or
  `MACOS_NOTARY_PROFILE`) to submit and staple the DMG and PKG with `notarytool`.
- **Linux:** packages are unsigned; add a repository signing key at the distribution
  layer if needed.

No certificate, Apple account, or private key is committed to this repository.

## Validation evidence (2026-11-10)

Windows x64, Inno Setup 6.7.1, unsigned 1.6.0 bundle:

- Built `5do-1.6.0-win-x64-setup.exe` from `5do-1.6.0-win-x64.zip`; SHA-256
  `e3ecba618a10aacb16225108e6c88d66d6de4fde51d34686b2462f856b830e64`.
- Silent per-user install to a temporary directory succeeded; the installed
  `FiveDo.Cli.exe --version` reported `5DO 1.6.0` and the frontend
  `--smoke-test` wrote `FRONTEND SMOKE OK 1.6.0` (`windowState=Normal`). Both
  processes exited 0.
- `HKCU\Software\Classes\.iso`, `.cue`, `.chd`, `.bin`, `.cdi`, and `.nrg` resolved to `5DO.Disc`;
  `.m3u` resolved to `5DO.Playlist`; the open command launched the installed frontend
  with `"%1"`.
- Silent uninstall exited 0 and removed the ProgIDs and default extension values.
- Built the arm64 installer from the published `win-arm64` bundle
  (`5do-1.6.0-win-arm64-setup.exe`).

Linux x64 (WSL Ubuntu 22.04):

- `5do_1.6.0_amd64.deb` built with `dpkg-deb`; control metadata reports
  `Architecture: amd64`, `Depends: dotnet-runtime-11.0`, and the expected
  `/opt/5do`, `/usr/bin/5do`, desktop, icon, and MIME paths.
- `5do-1.6.0-1.x86_64.rpm` built with `rpmbuild`; `rpm -qip` reports the expected
  name/version/license and `rpm -qlp` lists the same payload.
- `desktop-file-validate` exits 0, and `update-mime-database` produces
  `application/x-3do-disc` globs for `*.iso`, `*.cue`, `*.chd`, `*.bin`, `*.cdi`, and `*.nrg`.

macOS x64/arm64 (native CI, 2026-11-10):

- `package-bundle.ps1` ran `build-macos-package.sh` from the staged bundle and produced
  `5do-1.7.1-osx-x64.dmg` and `5do-1.7.1-osx-arm64.dmg` (plus `.pkg`) in CI run
  <https://github.com/purelogiccode/5DO/actions/runs/42877150967>.
- `build-bundle.yml` selected the DMG as the release asset for each macOS RID; the
  release workflow publishes the DMG instead of the internal tar.gz.
- The app bundle declares `com.5do.disc-image` and a `CFBundleDocumentTypes` viewer entry.
  Signing/notarization are skipped when no Developer ID/keychain profile is supplied.
- The six assets are published at
  <https://github.com/purelogiccode/5DO/releases/tag/v1.7.1> with `SHA256SUMS.txt`.
  Local copies are in `src/FiveDo.Frontend.Avalonia/bin/Release`; all six
  `5do-1.7.1-*` SHA-256 entries verify.

## 1.10.1 bundle validation (2026-11-15)

- CI run <https://github.com/purelogiccode/5DO/actions/runs/43760004443> passed every
  required job, including build/test on Windows/Linux/macOS, the RID-agnostic lock
  check, format, benchmark gates, trimmed CLI/frontend, both NativeAOT apphosts, the six
  bundle jobs, and the aggregate gate. Each bundle job verified `PACKAGE.txt`, the exact
  `docs/ReleaseNotes-1.10.1.md`, the absence of PDB/XML/runtime folders, the apphost
  architecture, CLI `selftest`, and the frontend smoke marker.
- CI also exercised the renamed `FiveDo.exe` / `FiveDo` apphost and its new
  `FRONTEND SMOKE OK 1.10.1` marker.
- Release run <https://github.com/purelogiccode/5DO/actions/runs/43760492909> built and
  published the six bundles and `SHA256SUMS.txt` at
  <https://github.com/purelogiccode/5DO/releases/tag/v1.10.1>.
- The local Windows x64 bundle reports `5DO 1.10.1`, passes `selftest`, writes
  `FRONTEND SMOKE OK 1.10.1`, and contains no `runtime/`, PDB, or XML files.
- All six published `5do-1.10.1-*` SHA-256 entries verify; the assets are copied into
  `src/FiveDo.Frontend.Avalonia/bin/Release`:
  - `5do-1.10.1-win-arm64.zip` `a20e321a178979d2106ac9b49ddd05e125ea08cdb0d5a6572ebdbf36464263c3`
  - `5do-1.10.1-win-x64.zip` `a0db37b37f62c71f2e6250326152e4dfe20958af741eb34712371eee89ffeb90`
  - `5do-1.10.1-linux-arm64.tar.gz` `899c91ee3741965111452cd703f133d0ff67013ce1b4b24d16244efc80bd5ecb`
  - `5do-1.10.1-linux-x64.tar.gz` `716633cd75683625a0b9e6dfcca302e863ba3e99a0ebc07c261838ac662b1def`
  - `5do-1.10.1-osx-arm64.dmg` `6bc0a64450fb7c9f3ed260b78b0cc3f671396371299186e26c0b51d54f6028ef`
  - `5do-1.10.1-osx-x64.dmg` `80eba4e75b5d7c03cfa411d548eaa38f688e454bd445255c6ebcda3dccab1023`
- Full suite **2,344/2,344**, locked restore, build, format, and
  `jb inspectcode --severity=WARNING` report no warnings or errors.
- Docs run <https://github.com/purelogiccode/5DO/actions/runs/43760004392> deployed the
  Pages site and wiki. `WhatsNew.html`, `ReleaseNotes-1.10.1.html`, and
  `wiki/Release-1.10.1` return HTTP 200.

## 1.10.0 bundle validation (2026-11-15)

- CI run <https://github.com/purelogiccode/5DO/actions/runs/43735222579> passed every
  required job, including the six bundle jobs. The bundle jobs now assert that each
  archive contains `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and
  the exact `docs/ReleaseNotes-<version>.md` for the build version.
- Initial release run
  <https://github.com/purelogiccode/5DO/actions/runs/43733549470> published
  `v1.10.0`. A packaging refresh run
  <https://github.com/purelogiccode/5DO/actions/runs/43735694120> then rebuilt the six
  bundles from master so the archives include `docs/ReleaseNotes-1.10.0.md` (the first
  build selected the lexicographically latest 1.9.1 notes).
- The release is marked latest at
  <https://github.com/purelogiccode/5DO/releases/tag/v1.10.0> with six bundles and
  `SHA256SUMS.txt`.
- The local Windows x64 bundle reports `5DO 1.10.0`, passes `selftest`, and writes
  `FRONTEND SMOKE OK 1.10.0`; the bundle contains no `runtime/`, PDB, or XML files.
- All six published `5do-1.10.0-*` SHA-256 entries verify; the assets are copied into
  `src/FiveDo.Frontend.Avalonia/bin/Release`:
  - `5do-1.10.0-win-arm64.zip` `3ac2ebd96aebbd9ed396f7203a3ce0538c25dbd4976c83f4822c549251dc764f`
  - `5do-1.10.0-win-x64.zip` `5de65601addfb0cbd43989972bd4ef9b645fb40e144a0e268d9ff28b4c096adc`
  - `5do-1.10.0-linux-arm64.tar.gz` `c5d39d82e6681603db0adeec9be8d04344d2326972291b128609e8e971d6d795`
  - `5do-1.10.0-linux-x64.tar.gz` `8360bc40150344b824b25ba1d1f8796a578c5052a3c166e58556a602e1d62eba`
  - `5do-1.10.0-osx-arm64.dmg` `63382fa44275b9679d5e04bb2f4fc76d21f45b0931f2eeb5071bc750107a4f95`
  - `5do-1.10.0-osx-x64.dmg` `1c7063c32d4078e949a2faf386966fd45956349de36ba6f2ffcfb9dd0ba0e37d`
- Full suite **2,338/2,338**, locked restore, build, format, and
  `jb inspectcode --severity=WARNING` report no warnings or errors.
- Docs run <https://github.com/purelogiccode/5DO/actions/runs/43736567863> deployed the
  Pages site and wiki. `WhatsNew.html`, `ReleaseNotes-1.10.0.html`, and
  `wiki/Release-1.10.0` return HTTP 200.

## 1.9.1 bundle validation (2026-11-14)

- Release run <https://github.com/purelogiccode/5DO/actions/runs/43566963957> built and
  published six framework-dependent bundles and `SHA256SUMS.txt` at
  <https://github.com/purelogiccode/5DO/releases/tag/v1.9.1>.
- The Windows x64 and Linux x64 bundle jobs ran CLI `--version`/`selftest` and the
  desktop `--smoke-test`; all jobs verified the apphost architecture, the
  no-runtime/no-PDB/no-XML rule, and the bundle contents.
- The local Windows x64 bundle reports `5DO 1.9.1`, passes `selftest`, and writes
  `FRONTEND SMOKE OK 1.9.1` with `locale=fr` and `firstMenuHeader=Fichier`; the Japanese
  smoke marker writes `firstMenuHeader=ファイル`.
- All six published `5do-1.9.1-*` SHA-256 entries verify; the assets are copied into
  `src/FiveDo.Frontend.Avalonia/bin/Release`.
- Full suite **2,259/2,259**, locked restore, build, format, and
  `jb inspectcode --severity=WARNING` report no warnings or errors.

## 1.9.0 bundle validation (2026-11-14)

- Release run <https://github.com/purelogiccode/5DO/actions/runs/43558324543> built and
  published six framework-dependent bundles and `SHA256SUMS.txt` at
  <https://github.com/purelogiccode/5DO/releases/tag/v1.9.0>.
- The Windows x64 and Linux x64 bundle jobs ran CLI `--version`/`selftest` and the
  desktop `--smoke-test`; all jobs verified the apphost architecture, the
  no-runtime/no-PDB/no-XML rule, and the bundle contents.
- The local Windows x64 bundle reports `5DO 1.9.0`, passes `selftest`, and writes
  `FRONTEND SMOKE OK 1.9.0` with `locale=de` and `firstMenuHeader=Datei`.
- All six published `5do-1.9.0-*` SHA-256 entries verify; the assets and internal macOS
  tarballs are copied into `src/FiveDo.Frontend.Avalonia/bin/Release`.
- Full suite **2,252/2,252**, locked restore, build, format, and a clean-cache
  `jb inspectcode --severity=WARNING` pass.

## 1.8.0 bundle validation (2026-11-11)

- Release run <https://github.com/purelogiccode/5DO/actions/runs/42959751665> built and
  published six framework-dependent bundles and `SHA256SUMS.txt` at
  <https://github.com/purelogiccode/5DO/releases/tag/v1.8.0>.
- The Windows x64 and Linux x64 bundle jobs ran CLI `--version`/`selftest` and the
  desktop `--smoke-test`; all Windows, Linux, and macOS jobs verified the apphost
  architecture, the no-runtime/no-PDB/no-XML rule, and the bundle contents.
- The local Windows x64 bundle reports `5DO 1.8.0`, passes `selftest`, and writes
  `FRONTEND SMOKE OK 1.8.0` (`windowState=Normal`).
- All six published `5do-1.8.0-*` SHA-256 entries verify; the assets are copied into
  `src/FiveDo.Frontend.Avalonia/bin/Release`.
