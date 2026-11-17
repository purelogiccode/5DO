# Building from Source

## Prerequisites

- .NET SDK **11.0.100** (pinned by `global.json`; projects target `net11.0` with C# 15)
- Git
- Optional: PowerShell 7 for packaging/soak scripts
- Optional: Python 3.12+ for the documentation site
- Optional: a legally obtained BIOS/disc corpus for real-content tests

## Build and test

```powershell
git clone https://github.com/purelogiccode/5DO.git
cd 5DO
dotnet build 5DO.sln -c Release
dotnet test 5DO.sln -c Release
dotnet format 5DO.sln --verify-no-changes
```

## Publish

The release layout is five bundles: Windows and Linux x64/arm64 plus macOS arm64
(Intel macOS is unsupported), containing both apps. The bundles are self-contained:
the .NET 11 runtime is embedded and users do not install it separately. Publish the two
apphosts as self-contained single files:

```powershell
$rid = 'win-x64'

dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r $rid `
  --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -m:1 -o "artifacts/bundle/$rid/cli"

dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r $rid --self-contained true -p:PublishSingleFile=true `
  -p:PublishTrimmed=false -m:1 -o "artifacts/bundle/$rid/frontend"

pwsh -NoProfile -File packaging/package-bundle.ps1 `
  -Rid $rid `
  -CliPublishDirectory "artifacts/bundle/$rid/cli" `
  -FrontendPublishDirectory "artifacts/bundle/$rid/frontend" `
  -OutputDirectory "artifacts/bundles"
```

The script removes PDB and XML documentation files, copies the README/license/notices/
whatsnew/release notes, and writes `PACKAGE.txt` and `SHA256SUMS.txt`. The .NET
runtime is embedded in the single-file apphosts; no separate runtime folder is staged.

Optional NativeAOT CLI:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:FiveDoNativeAot=true -p:PublishSingleFile=true
```

CI builds `5do-<version>-win-x64.zip`, `5do-<version>-win-arm64.zip`,
`5do-<version>-linux-x64.tar.gz`, `5do-<version>-linux-arm64.tar.gz`, and `5do-<version>-osx-arm64.dmg` and publishes them
to the GitHub release. The macOS job runs natively and `package-bundle.ps1` builds the
DMG (the
tar.gz is only the extraction/smoke intermediate).

## Documentation

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --require-hashes -r requirements-docs.txt
mkdocs build --strict
```

## Full guides

- [Build and test](https://purelogiccode.github.io/5DO/BuildAndTest.html)
- [Test strategy](https://purelogiccode.github.io/5DO/TestStrategy.html)
- [Architecture](https://purelogiccode.github.io/5DO/Architecture.html)
- [Developer guide](https://purelogiccode.github.io/5DO/DeveloperGuide.html)
