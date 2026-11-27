[CmdletBinding()]
param(
    [string] $InstallDir = 'C:\FiveDoLobby',

    [string] $PublicHost = '',

    [string] $RelayHost = '',

    [int] $HttpPort = 8080,

    [int] $UdpPort = 5700,

    [int] $ControlRelayPortStart = 40000,

    [int] $ControlRelayPortEnd = 40999,

    [int] $UdpRelayPortStart = 41000,

    [int] $UdpRelayPortEnd = 41999,

    [switch] $TrustCloudflareHeaders,

    [string] $OriginSecretFile = '',

    [switch] $SkipPublish,

    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'FiveDoLobby'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $repoRoot 'src\FiveDo.Lobby\FiveDo.Lobby.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    throw "Lobby project not found: $project"
}

if (-not $SkipPublish) {
    Write-Host "Publishing the lobby to $InstallDir..."
    & dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --nologo `
        -o $InstallDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$exe = Join-Path $InstallDir 'FiveDo.Lobby.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "The published lobby executable is missing: $exe"
}

# Production overrides are loaded beside the executable by the Windows service host.
$originSecret = ''
if ($OriginSecretFile) {
    if (-not (Test-Path -LiteralPath $OriginSecretFile)) {
        throw "Origin secret file not found: $OriginSecretFile"
    }

    $originSecret = (Get-Content -LiteralPath $OriginSecretFile -Raw).Trim()
    if ($originSecret.Length -lt 32) {
        throw 'The origin secret must be at least 32 characters.'
    }
}

$settings = [ordered]@{
    Urls = "http://0.0.0.0:$HttpPort"
    Lobby = [ordered]@{
        PublicHost = $PublicHost
        RelayHost = $RelayHost
        UdpPort = $UdpPort
        ControlRelayPortStart = $ControlRelayPortStart
        ControlRelayPortEnd = $ControlRelayPortEnd
        UdpRelayPortStart = $UdpRelayPortStart
        UdpRelayPortEnd = $UdpRelayPortEnd
        RoomLifetimeMinutes = 30
        RoomIdleTimeoutMinutes = 10
        MaxRooms = 500
        MaxRoomsPerIp = 5
        TrustCloudflareHeaders = [bool] $TrustCloudflareHeaders
        OriginSecretHeader = 'X-FiveDo-Origin-Secret'
        OriginSecret = $originSecret
        RoomCreatePerMinute = 5
        RoomJoinPerMinute = 20
        SignalPerMinute = 10
        GlobalRequestsPerMinute = 120
        MaxUdpRelayBytesPerSecond = 1000000
        MaxControlRelayMinutes = 10
        MaxRendezvousDatagramsPerSecond = 5000
        MaxRequestBytes = 65536
        EnableRelay = $true
    }
}
$settingsPath = Join-Path $InstallDir 'appsettings.Production.json'
$json = $settings | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($settingsPath, $json, [Text.UTF8Encoding]::new($false))
Write-Host "Wrote $settingsPath"

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    if (-not $Force) {
        throw "Service '$serviceName' already exists. Re-run with -Force to replace it."
    }

    Write-Host "Stopping and removing the existing $serviceName service..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $serviceName | Out-Null
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 250
        if (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
            break
        }
    }
}

Write-Host "Creating the $serviceName service..."
& sc.exe create $serviceName binPath= "`"$exe`"" start= auto DisplayName= "FiveDo Lobby"
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe create failed with exit code $LASTEXITCODE."
}
& sc.exe description $serviceName 'Emulator-agnostic remote-play lobby for 5DO and XGrid.' | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

$firewallRules = @(
    @{ DisplayName = 'FiveDo Lobby HTTP'; Protocol = 'TCP'; Port = "$HttpPort" },
    @{ DisplayName = 'FiveDo Lobby UDP rendezvous'; Protocol = 'UDP'; Port = "$UdpPort" },
    @{ DisplayName = 'FiveDo Lobby TCP control relay'; Protocol = 'TCP'; Port = "$ControlRelayPortStart-$ControlRelayPortEnd" },
    @{ DisplayName = 'FiveDo Lobby UDP relay'; Protocol = 'UDP'; Port = "$UdpRelayPortStart-$UdpRelayPortEnd" }
)
foreach ($rule in $firewallRules) {
    if (-not (Get-NetFirewallRule -DisplayName $rule.DisplayName -ErrorAction SilentlyContinue)) {
        Write-Host "Adding firewall rule: $($rule.DisplayName)"
        New-NetFirewallRule `
            -DisplayName $rule.DisplayName `
            -Direction Inbound `
            -Action Allow `
            -Protocol $rule.Protocol `
            -LocalPort $rule.Port `
            -Profile Any | Out-Null
    }
}

Start-Service -Name $serviceName
$service = Get-Service -Name $serviceName
Write-Host ''
Write-Host "Service:      $($service.Status) ($serviceName)"
Write-Host "Health:       http://<vps>:$HttpPort/healthz"
Write-Host "UDP:          $UdpPort (rendezvous)"
Write-Host "TCP relay:    $ControlRelayPortStart-$ControlRelayPortEnd"
Write-Host "UDP relay:    $UdpRelayPortStart-$UdpRelayPortEnd"
if ($PublicHost) {
    Write-Host "Public host:  $PublicHost"
} else {
    Write-Host 'Public host:  request Host header (set -PublicHost for a stable address)'
}
if ($RelayHost) {
    Write-Host "Relay host:   $RelayHost (DNS-only; not Cloudflare-proxied)"
}
