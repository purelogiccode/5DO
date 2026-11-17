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
