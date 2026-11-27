[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [string] $SiteName = 'FiveDoLobby',

    [string] $HostName = 'lobby.purelogiccode.com',

    [string] $RelayHost = 'lobby-direct.purelogiccode.com',

    [string] $PhysicalPath = 'C:\inetpub\purelogiccode.lobby',

    [string] $AppPool = 'FiveDoLobby',

    [string] $CertificateThumbprint = '',

    [int] $UdpPort = 5700,

    [int] $ControlRelayPortStart = 40000,

    [int] $ControlRelayPortEnd = 40999,

    [int] $UdpRelayPortStart = 41000,

    [int] $UdpRelayPortEnd = 41999,

    [switch] $TrustCloudflareHeaders,

    [string] $OriginSecretFile = '',

    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}

Import-Module WebAdministration -ErrorAction Stop
if (-not (Test-Path -LiteralPath $PackagePath)) {
    throw "Package not found: $PackagePath"
}

$staging = 'C:\Deploy\fivedo-lobby-publish'
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $staging | Out-Null
Write-Host "Extracting $PackagePath..."
& C:\Windows\System32\tar.exe -xzf $PackagePath -C $staging
if ($LASTEXITCODE -ne 0) {
    throw "tar extraction failed with exit code $LASTEXITCODE."
}

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if ($site) {
    if (-not $Force) {
        throw "IIS site '$SiteName' already exists. Re-run with -Force to replace it."
    }

    Write-Host "Stopping and removing IIS site $SiteName..."
    Stop-Website -Name $SiteName
    Remove-Website -Name $SiteName
}

$pool = Get-IISAppPool -Name $AppPool -ErrorAction SilentlyContinue
if ($pool) {
    if (-not $Force) {
        throw "IIS app pool '$AppPool' already exists. Re-run with -Force to replace it."
    }

    Stop-WebAppPool -Name $AppPool
    Remove-WebAppPool -Name $AppPool
}

New-Item -ItemType Directory -Force -Path $PhysicalPath | Out-Null
Write-Host "Syncing the lobby to $PhysicalPath..."
& robocopy $staging $PhysicalPath /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE."
}

$settingsPath = Join-Path $PhysicalPath 'appsettings.Production.json'
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
    Lobby = [ordered]@{
        PublicHost = $HostName
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
$json = $settings | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText($settingsPath, $json, [Text.UTF8Encoding]::new($false))

$webConfigPath = Join-Path $PhysicalPath 'web.config'
[xml] $webConfig = Get-Content -LiteralPath $webConfigPath
$systemWebServer = $webConfig.SelectSingleNode('/configuration/system.webServer')
if (-not $systemWebServer) {
    $systemWebServer = $webConfig.SelectSingleNode('/configuration/location/system.webServer')
}

$aspNetCore = if ($systemWebServer) { $systemWebServer.SelectSingleNode('aspNetCore') } else { $null }
if (-not $aspNetCore) {
    throw 'The package web.config has no aspNetCore handler.'
}

$environmentVariables = $aspNetCore.SelectSingleNode('environmentVariables')
if (-not $environmentVariables) {
    $environmentVariables = $webConfig.CreateElement('environmentVariables')
    [void] $aspNetCore.AppendChild($environmentVariables)
}

$environment = $environmentVariables.SelectSingleNode("environmentVariable[@name='ASPNETCORE_ENVIRONMENT']")
if (-not $environment) {
    $environment = $webConfig.CreateElement('environmentVariable')
    $environment.SetAttribute('name', 'ASPNETCORE_ENVIRONMENT')
    [void] $environmentVariables.AppendChild($environment)
}
$environment.SetAttribute('value', 'Production')
$webConfig.Save($webConfigPath)

Write-Host "Creating app pool $AppPool..."
New-Item "IIS:\AppPools\$AppPool" -Force | Out-Null
Set-ItemProperty "IIS:\AppPools\$AppPool" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$AppPool" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\AppPools\$AppPool" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

if ($CertificateThumbprint) {
    Write-Host "Creating HTTPS site $SiteName for $HostName..."
    New-Website `
        -Name $SiteName `
        -PhysicalPath $PhysicalPath `
        -ApplicationPool $AppPool `
        -Port 443 `
        -HostHeader $HostName `
        -Ssl `
        -SslFlags 1 | Out-Null
    $binding = Get-WebBinding -Name $SiteName -Protocol https -HostHeader $HostName
    $binding.AddSslCertificate($CertificateThumbprint, 'My')
} else {
    Write-Warning 'No certificate thumbprint supplied; creating an HTTP binding on port 80.'
    New-Website `
        -Name $SiteName `
        -PhysicalPath $PhysicalPath `
        -ApplicationPool $AppPool `
        -Port 80 `
        -HostHeader $HostName | Out-Null
}

& icacls $PhysicalPath /grant "IIS APPPOOL\${AppPool}:(OI)(CI)RX" /T | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "icacls failed with exit code $LASTEXITCODE."
}

$firewallRules = @(
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

Start-WebAppPool -Name $AppPool
Start-Website -Name $SiteName
Start-Sleep -Seconds 2

$health = if ($CertificateThumbprint) {
    & curl.exe -s -k --resolve "${HostName}:443:127.0.0.1" "https://$HostName/healthz"
} else {
    & curl.exe -s -H "Host: $HostName" 'http://127.0.0.1/healthz'
}

Write-Host ''
Write-Host "IIS site:     $SiteName ($HostName)"
Write-Host "Physical:     $PhysicalPath"
Write-Host "App pool:     $AppPool"
Write-Host "Health:       $health"
Write-Host "Relay host:   $RelayHost (DNS-only)"
Write-Host "UDP:          $UdpPort"
