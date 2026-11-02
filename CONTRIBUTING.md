# Contributing to 5DO

Thanks for helping improve 5DO. This project is a C#/.NET 10 emulator with a
deterministic core, an Avalonia desktop app, a headless CLI, and a documentation site.
Contributions of code, tests, documentation, compatibility evidence, and reproducible
bug reports are all welcome.

By participating, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- Check the [FAQ](docs/FAQ.md), [Known issues](docs/KnownIssues.md), and
  [Troubleshooting](docs/Troubleshooting.md) first.
- Search existing [issues](https://github.com/purelogiccode/5DO/issues) and
  [discussions](https://github.com/purelogiccode/5DO/discussions).
- For a large change, open a discussion or an issue before writing the implementation.
- **Never include BIOS ROMs, game images, or extracted game assets** in a commit,
  issue, discussion, test fixture, or diagnostics bundle.

## Development setup

Requirements:

- .NET SDK **10.0.100** or newer (`global.json` rolls forward within .NET 10).
- Git.
- Optional: PowerShell 7 for packaging/soak scripts and Python 3.12+ for the docs site.
- Optional: a legally obtained BIOS and disc images for real-content validation.

```powershell
git clone https://github.com/purelogiccode/5DO.git
cd 5DO
dotnet build 5DO.sln
dotnet test 5DO.sln
```

The test suite does not need a BIOS or game images. Real-content tests and sweeps are
manual and use files you supply locally.

## Repository layout

| Path | Contents |
|---|---|
| `src/FiveDo.Core` | Contracts, shared types, constants, state interfaces |
| `src/FiveDo.Cpu` | ARM60 interpreter and disassembler |
| `src/FiveDo.Memory` | RAM, ROM, NVRAM, mirrors, word-swapped access |
| `src/FiveDo.Madam` | MADAM/CEL, matrix engine, pixel pipeline |
| `src/FiveDo.Clio` | CLIO timers, interrupts, FIFOs, DMA, XBUS window |
| `src/FiveDo.Dsp` | 3DO DSP and audio sample generation |
| `src/FiveDo.Sport` | SPORT VRAM fill/copy |
| `src/FiveDo.Video` | VDLP, frame assembly, filters, CPU presenter |
| `src/FiveDo.Video.OpenGL` | OpenGL/GLES desktop presenter |
| `src/FiveDo.Video.Vulkan` | Optional Vulkan presenter |
| `src/FiveDo.Xbus` | Controller/peripheral bus |
| `src/FiveDo.Cdrom` | CD-ROM command state machine |
| `src/FiveDo.Storage` | ISO/BIN/CUE/CHD, BIOS/NVRAM, game database, file system |
| `src/FiveDo.Audio` | Audio pipeline, OpenAL/WAV/null sinks, analysis |
| `src/FiveDo.Input` | Controller state, profiles, scripts, recording |
| `src/FiveDo.Machine` | Machine composition, Quartz scheduler, frame loop |
| `src/FiveDo.Host` | Settings, library, save slots, profiles, diagnostics |
| `src/FiveDo.Cli` | Headless command-line interface |
| `src/FiveDo.Frontend.Avalonia` | Desktop frontend |
| `tests/FiveDo.Tests` | xUnit tests |
| `benchmarks/FiveDo.Benchmarks` | Deterministic performance gate |
| `tools/FiveDo.DiffHarness` | 5DO vs OperaCore differential harness |
| `docs/` | Documentation site source |
| `wiki/` | Curated GitHub wiki source |

See [Architecture](docs/Architecture.md) and [Developer Guide](docs/DeveloperGuide.md)
for the dependency rules and extension points.

## Coding conventions

The build enforces most of these:

- `net10.0`, nullable enabled, implicit usings disabled.
- Warnings are errors; NuGet audit findings are errors.
- Deterministic builds; no wall-clock or hidden global state in the core.
- One console subsystem per project; no chip-to-chip project references. Wire devices
  through `FiveDo.Core` interfaces and `FiveDo.Machine`.
- Keep hot paths allocation-free in steady state. The benchmark gate enforces zero
  managed bytes/frame.
- Use source-generated JSON contexts for settings, state metadata, and CLI `--json`
  output. Keep trimmed and NativeAOT paths healthy.
- Follow the existing naming, file layout, and documentation-comment style.
- Add or update tests for behavior changes. Include a real-content hash or state
  roundtrip when the change affects emulation.

Run formatting before opening a pull request:

```powershell
dotnet format 5DO.sln --verify-no-changes
```

## Tests and evidence

The test layers are described in [Test Strategy](docs/TestStrategy.md). At minimum:

```powershell
dotnet build 5DO.sln -c Release
dotnet test 5DO.sln -c Release
```

For emulation changes, record the evidence in the relevant document rather than only
in the pull request:

- frame/audio/state hashes in [Test corpus](docs/TestCorpus.md);
- per-title status in [Game compatibility list](docs/GameCompatibilityList.md);
- sweep results in [Game compatibility results](docs/GameCompatibilityResults.md);
- known limitations in [Known issues](docs/KnownIssues.md).

Use the CLI's deterministic checkpoint and capture options where possible:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios bios/panafz1.bin `
  --frames 3000 --checkpoints 3000 --state-out state.tdos --json
```

## Documentation changes

The docs site is built with MkDocs Material:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --require-hashes -r requirements-docs.txt
mkdocs build --strict
```

Update the user-facing page, the implementation-facing page, and the
[changelog](CHANGELOG.md) when behavior changes. See
[Documentation guide](docs/Documentation.md) and the
[documentation source guide](docs/README.md).

## Pull requests

1. Fork the repository and create a focused branch.
2. Keep the change scoped; avoid unrelated formatting or refactoring.
3. Add tests and documentation with the implementation.
4. Run build, tests, format, and `mkdocs build --strict` if docs changed.
5. Open a pull request using the template. Describe the problem, the approach, and the
   evidence.
6. Respond to review comments and keep CI green.

CI runs on Windows, Linux, and macOS, including build/test, format verification,
benchmark gates, trimmed publish smoke tests, NativeAOT smoke tests, docs builds, and
security analysis. A pull request is not ready to merge while required checks fail.

## Security

Do not open a public issue for a vulnerability. Follow [SECURITY.md](SECURITY.md) and
use GitHub's private vulnerability reporting.

## Licensing

5DO is distributed under the FreeDO modified GNU LGPL. By contributing, you agree that
your contribution can be distributed under the project license. Read
[LICENSE](LICENSE), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), and
[docs/Licensing.md](docs/Licensing.md) before adding third-party code, data, or assets.

Do not add dependencies, fonts, shaders, game metadata, or artwork unless the license is
compatible and the attribution is recorded.
