[CmdletBinding()]
param(
    [string] $InstallDir = 'C:\FiveDoLobby',

    [switch] $RemoveFirewallRules,

    [switch] $RemoveFiles
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'FiveDoLobby'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping and deleting $serviceName..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $serviceName | Out-Null
}

if ($RemoveFirewallRules) {
    $names = @(
        'FiveDo Lobby HTTP',
        'FiveDo Lobby UDP rendezvous',
        'FiveDo Lobby TCP control relay',
        'FiveDo Lobby UDP relay'
    )
    foreach ($name in $names) {
        Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    }
}

if ($RemoveFiles -and (Test-Path -LiteralPath $InstallDir)) {
    Remove-Item -LiteralPath $InstallDir -Recurse -Force
    Write-Host "Removed $InstallDir"
}

Write-Host "Uninstalled $serviceName."
