# Agent Instructions - 5DO

Standing rules for AI agents (and anyone) working in this repository.

## Local Linux VM validation

The local Linux validation target is the Hyper-V VM `LinuxMint` at
`D:\Hyper-V\VMs\LinuxMint` (Linux Mint 22.3, x86_64). Start it from an elevated
PowerShell with `Start-VM -Name LinuxMint`.

A hardware GPU target is also available: the **bare-metal** Linux Mint 22.3 host at
`192.168.1.108` (`ssh linuxgpu`, details in `AGENTS.local.md`). It has a real Intel
HD 4000 with hardware OpenGL 4.2 (crocus) and Vulkan 1.2 (`hasvk`), a live 1920x1080
X11 session, and HDA Intel PCH audio, so use it for GPU presentation, Vulkan-surface,
real-device audio, and acceptance evidence. It has no optical drive. Do not treat CI
`Xvfb`/`llvmpipe` results as GPU evidence.

Machine-specific SSH credentials are kept in the gitignored `AGENTS.local.md`.
Read that file before connecting. The guest does not have the .NET SDK installed;
publish `linux-x64` on the host and copy the output into the guest, or install the
SDK in the guest first.

The v1.0 Linux x64 gate has passed: the full test suite (1,473/1,473), CLI smoke,
a 3,000-frame disc boot, audio, and the frontend `--smoke-test` ran on Linux Mint 22.3.
The GL probe timed out under the VM's llvmpipe/SSH session, so Linux GPU presentation
is documented as preview/best-effort. Record new Linux evidence in
`docs/Compatibility.md` §10.3 and `docs/KnownIssues.md`.

## ARM64 Linux validation

The ARM64 Linux target is the Azure VM `linuxarm-test` (`Standard_D2ps_v6`,
Neoverse-N2, 2 vCPU / 8 GB, Ubuntu 24.04.5, aarch64). Host inventory and rebuild
scripts live in `C:\Sincronizar\ArmHosts`; machine-specific access details are in the
gitignored `AGENTS.local.md`. Start and stop it with the helper, which allocates a
temporary public IP for the session and deletes it on stop:

```powershell
pwsh -NoProfile -File C:\Sincronizar\ArmHosts\ArmHost.ps1 linux start
pwsh -NoProfile -File C:\Sincronizar\ArmHosts\ArmHost.ps1 linux stop
```

**Deallocate when finished.** Compute billing stops on deallocation and the public IP is
deleted with the session, so a stopped host costs only its OS disk (about $3.01/month,
Standard HDD). The old static IPs were removed on 2026-11-19. It auto-deallocates daily at
03:00 UTC.

The VM has **no GPU and no sound card**. Mesa exposes only `llvmpipe` (OpenGL) and
`lavapipe` (Vulkan), and audio needs a PulseAudio null sink. Use it to validate arm64
codegen, self-contained publishing, packaging, determinism parity with Windows x64, and
the CPU/Vulkan software paths. Hardware GPU presentation and real-device audio evidence
stay on the bare-metal x86 host above; never record llvmpipe results as GPU evidence.

Run the `linux-arm64` gate against the published `5do-<version>-linux-arm64.tar.gz`, not
a local cross-publish:

- Verify `sha256sum` against the release `SHA256SUMS.txt`, then confirm both apphosts
  are stripped aarch64 ELF files and the bundle has no PDB/XML documentation.
- Install the .NET 11 SDK into `~/.dotnet` (the image ships without it) and run
  `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release`.
- CLI: `--version`, `selftest --json`, `bench` (compatible and accuracy), `bios info`,
  `info`, a 3,000-frame disc boot with `--frame-out`, `--audio wav` plus
  `audio analyze`, `trace`, `doctor`, and a `--autopilot road` run. Compare the frame
  hashes and PC with a Windows x64 run of the same disc and BIOS.
- Desktop: `--smoke-test --smoke-output <path>` under `xvfb-run` (the marker is only
  written when `--smoke-output` is supplied), plus a real XFCE session with
  `--gl-backend software` or `--gl-backend vulkan`. The auto backend selects OpenGL and
  leaves a 1x1 black video surface under llvmpipe.
- Packages: AppImage (FUSE and `--appimage-extract-and-run`), the `.deb` install, the
  `.rpm` metadata, and the Flatpak install.

Record results in `docs/Compatibility.md` §10.5 and `docs/KnownIssues.md`.

Two practical warnings: the SSH key under `C:\Sincronizar\ArmHosts` has loose ACLs, so
copy it to a private folder and grant only the current user before `ssh`; and
`dotnet publish -r linux-arm64` rewrites every `packages.lock.json` with RID-specific
entries and CRLF endings, so `git restore -- ':(glob)**/packages.lock.json'` afterwards.

## Windows ARM64 validation

The Windows ARM64 target is the Azure VM `winarm-test` (`Standard_D2ps_v6`, Cobalt 100,
2 vCPU / 8 GB, Windows 11 Pro 25H2 build 26200). Inventory is in
`C:\Sincronizar\ArmHosts`; access details are in the gitignored `AGENTS.local.md`. Use the
same helper, which allocates a temporary public IP and refreshes the RDP NSG rule to the
current client IP on every start:

```powershell
pwsh -NoProfile -File C:\Sincronizar\ArmHosts\ArmHost.ps1 windows start
pwsh -NoProfile -File C:\Sincronizar\ArmHosts\ArmHost.ps1 windows stop
```

**Deallocate when finished.** A stopped host costs only its OS disk (about $5.89/month,
Standard HDD); the public IP exists only during the session. It auto-deallocates daily at
03:00 UTC.

The VM is a clean client image: it has **no GPU** (Microsoft Basic Render Driver), **no
Vulkan driver**, and **no audio endpoint**. Direct3D 11 and ANGLE (OpenGL ES 3.0) render,
so the desktop uses the ANGLE/GLES presenter; `--vk-probe` fails with
`ErrorIncompatibleDriver`; OpenAL needs `vc_redist.arm64.exe` installed and has no device
to open (use `ALSOFT_DRIVERS=null` for audio-path tests). The Windows distribution is a
ZIP bundle; there is no Windows installer.

The gate, run against the published `5do-<version>-win-arm64.zip`:

- Verify `sha256sum` against the release `SHA256SUMS.txt`, then confirm the PE machine is
  `ARM64` for `FiveDo.exe`, `FiveDo.Cli.exe`, `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`,
  `OpenAL32.dll`, `soft_oal.dll`, and `av_libglesv2.dll`.
- Install the .NET 11 SDK with `dotnet-install.ps1 -Channel 11.0 -InstallDir
  C:\Users\azureuser\dotnet`, unpack the `v1.14.1` tag source, and run
  `dotnet test tests\FiveDo.Tests\FiveDo.Tests.csproj -c Release`. There is no `git` on
  the image, so upload a `git archive` ZIP of the tag instead of cloning.
- CLI: `--version`, `selftest --json`, `bench`, `bios info`, `info`, a 3,000-frame disc
  boot with `--frame-out`, `--audio wav` plus `audio analyze`, `trace`, `doctor`,
  `--autopilot road`, state save/resume, movie record/play, `disc convert`, and the
  portable-mode NVRAM path. The 600-frame hash `0x110E7DC5`, the 3,000-frame hash
  `0xAD4F370C` with PC `0x0007AEF8`, and the Road Rash 20,000-frame hash must match the
  Linux arm64 and Windows x64 runs.
- Desktop: SSH lands in session 0 with no desktop. Enable autologon, reboot, and run the
  smoke and game through `schtasks /IT` tasks; wrap PowerShell in a `.vbs` launcher
  (`WScript.Shell.Run ..., 0`) and force conhost (`HKCU\Console\%%Startup`), otherwise
  Windows Terminal leaves a console tab over the screen. Capture with
  `System.Drawing` `CopyFromScreen` from the same interactive session.
- Probes: `--gl-probe` and `--d3d-probe` must pass, and `--vk-probe` is expected to fail
  on this VM.

Record results in `docs/Compatibility.md` §10.6 and `docs/KnownIssues.md`.

## Build and test

```powershell
dotnet build 5DO.sln -c Debug
dotnet test 5DO.sln -c Debug --no-build --no-restore
dotnet format 5DO.sln --verify-no-changes --no-restore
jb inspectcode 5DO.sln --severity=WARNING --no-updates --no-build --output=inspectcode.sarif
```

Run the release gate commands from `docs/ReleaseChecklist.md` before tagging a release.

## Release artifacts

Every release must **produce and publish runnable bundles/binaries**. A source tag or
GitHub release without the built artifacts is not a complete release.

- Five bundles: `5do-win-x64.zip`, `5do-win-arm64.zip`, `5do-linux-x64.tar.gz`,
  `5do-linux-arm64.tar.gz`, and `5do-osx-arm64.dmg`. Intel macOS is no longer
  supported (the validation host is arm64); macOS is distributed as a
  drag-to-Applications DMG and the internal `5do-osx-arm64.tar.gz` is only the CI
  extraction/smoke intermediate.
- Each bundle contains the **self-contained** single-file CLI and desktop apphosts,
  `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the
  release/legal docs. The .NET 11 runtime is embedded; users do not install a separate
  runtime. Publish with `--self-contained true -p:PublishSingleFile=true
  -p:PublishTrimmed=false`.
- Build with `packaging/package-bundle.ps1`. `build-bundle.yml` validates the bundles
  in CI and `release.yml` publishes them with `SHA256SUMS.txt`.
- Keep a local copy of each bundle in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- Remove PDB and XML documentation files from the bundles; they are not runtime
  dependencies.
- Run `jb inspectcode` at warning severity and fix every warning/error before release.

## Documentation rules

- `docs/ImplementationPlan.md` is the active implementation and missing-feature backlog.
  Historical status, milestones, evidence, and the completed log live in
  `docs/ImplementationPlanArchive.md`.
- v1.0 is a **documented-compatibility** release. Full compatibility, M13.5/M13.6
  visual/high-resolution review, and M14 high-resolution CEL research are post-1.0.
- Legal/attribution sign-off is recorded under the FreeDO modified GNU LGPL as free,
  open-source donationware (non-commercial, voluntary donations only).
