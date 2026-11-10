# Building from Source

## Prerequisites

- .NET SDK **10.0.301** (pinned by `global.json`)
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

The release layout is one bundle per system and architecture (Windows, Linux, and macOS
on x64 and arm64), containing both apps. The bundles are framework-dependent: users
must install the .NET 10 Runtime. Publish the two apphosts as framework-dependent single
files:

```powershell
$rid = 'win-x64'

dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r $rid `
  --self-contained false -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -m:1 -o "artifacts/bundle/$rid/cli"

dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r $rid --self-contained false -p:PublishSingleFile=true `
  -p:PublishTrimmed=false -m:1 -o "artifacts/bundle/$rid/frontend"

pwsh -NoProfile -File packaging/package-bundle.ps1 `
  -Rid $rid `
  -CliPublishDirectory "artifacts/bundle/$rid/cli" `
  -FrontendPublishDirectory "artifacts/bundle/$rid/frontend" `
  -OutputDirectory "artifacts/bundles"
```

The script removes PDB and XML documentation files, copies the README/license/notices/
whatsnew/release notes, and writes `PACKAGE.txt` and `SHA256SUMS.txt`. It does not copy
the .NET runtime.

Optional NativeAOT CLI:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:FiveDoNativeAot=true -p:PublishSingleFile=true
```

CI builds `5do-<version>-win-x64.zip`, `5do-<version>-win-arm64.zip`,
`5do-<version>-linux-x64.tar.gz`, `5do-<version>-linux-arm64.tar.gz`,
`5do-<version>-osx-x64.dmg`, and `5do-<version>-osx-arm64.dmg` and publishes them to the
GitHub release. The macOS jobs run natively and `package-bundle.ps1` builds the DMG (the
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
