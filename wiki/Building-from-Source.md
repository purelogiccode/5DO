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

Trimmed, self-contained CLI:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

Trimmed desktop frontend:

```powershell
dotnet publish src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=true
```

Optional NativeAOT CLI:

```powershell
dotnet publish src/FiveDo.Cli/FiveDo.Cli.csproj -c Release -r win-x64 `
  --self-contained true -p:FiveDoNativeAot=true -p:PublishSingleFile=true
```

CI publishes `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and
`osx-arm64` portable archives.

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
