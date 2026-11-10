param(
    [Parameter(Mandatory = $true)]
    [string] $Disc,

    [Parameter(Mandatory = $true)]
    [string] $Bios,

    [double] $Seconds = 600,

    [ValidateSet('null', 'default')]
    [string] $Device = 'null',

    [string] $Output = 'artifacts/plat07/frontend-aot-soak.json',

    [string] $AotDirectory = 'artifacts/frontend-aot-plat07',

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$discPath = (Resolve-Path -LiteralPath $Disc).Path
$biosPath = (Resolve-Path -LiteralPath $Bios).Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
$exe = Join-Path $repoRoot (Join-Path $AotDirectory 'FiveDo.Frontend.Avalonia.exe')

if (-not (Test-Path -LiteralPath $exe)) {
    if ($SkipBuild) {
        throw "NativeAOT frontend not found: $exe"
    }

    dotnet publish (Join-Path $repoRoot 'src/FiveDo.Frontend.Avalonia/FiveDo.Frontend.Avalonia.csproj') `
        -c Release -r win-x64 --self-contained true `
        -p:FiveDoNativeAot=true -m:1 `
        -o (Join-Path $repoRoot $AotDirectory) | Out-Host
}

$secondsText = $Seconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
$argumentLine = "`"$discPath`" --bios `"$biosPath`" --audio-soak $secondsText --audio-soak-output `"$outputPath`""
if ($Device -eq 'null') {
    $env:ALSOFT_DRIVERS = 'null'
}

try {
    $process = Start-Process -FilePath $exe -ArgumentList $argumentLine -Wait -PassThru
}
finally {
    if ($Device -eq 'null') {
        Remove-Item Env:ALSOFT_DRIVERS -ErrorAction SilentlyContinue
    }
}

if ($process.ExitCode -ne 0) {
    throw "Frontend audio soak failed with exit code $($process.ExitCode). See $outputPath."
}

Get-Content -LiteralPath $outputPath -Raw
