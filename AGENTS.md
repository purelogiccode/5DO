# Agent Instructions - 5DO

Standing rules for AI agents (and anyone) working in this repository.

## Local Linux VM validation

The local Linux validation target is the Hyper-V VM `LinuxMint` at
`D:\Hyper-V\VMs\LinuxMint` (Linux Mint 22.3, x86_64). Start it from an elevated
PowerShell with `Start-VM -Name LinuxMint`.

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
jb inspectcode 5DO.sln --severity=WARNING --no-updates
```

Run the release gate commands from `docs/ReleaseChecklist.md` before tagging a release.

## Release artifacts

Every release must **produce and publish runnable bundles/binaries**. A source tag or
GitHub release without the built artifacts is not a complete release.

- One bundle per system and architecture: `5do-win-x64.zip`, `5do-win-arm64.zip`,
  `5do-linux-x64.tar.gz`, `5do-linux-arm64.tar.gz`, `5do-osx-x64.tar.gz`, and
  `5do-osx-arm64.tar.gz`.
- Each bundle contains the framework-dependent single-file CLI and desktop apphosts,
  `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `whatsnew.md`, and the
  release/legal docs. **No .NET runtime is bundled**; users must install the .NET 10
  runtime on their machine.
- Build with `packaging/package-bundle.ps1`. `build-bundle.yml` validates the bundles
  in CI and `release.yml` publishes them with `SHA256SUMS.txt`.
- Keep a local copy of each bundle in `src/FiveDo.Frontend.Avalonia/bin/Release`.
- Remove PDB and XML documentation files from the bundles; they are not runtime
  dependencies.
- Run `jb inspectcode` at warning severity and fix every warning/error before release.

## Documentation rules

- `docs/ImplementationPlan.md` is the active compatibility task list. Historical status,
  milestones, evidence, and the completed log live in `docs/ImplementationPlanArchive.md`.
- v1.0 is a **documented-compatibility** release. Full compatibility, M13.5/M13.6
  visual/high-resolution review, and M14 high-resolution CEL research are post-1.0.
- Legal/attribution sign-off is recorded under the FreeDO modified GNU LGPL as free,
  open-source donationware (non-commercial, voluntary donations only).
