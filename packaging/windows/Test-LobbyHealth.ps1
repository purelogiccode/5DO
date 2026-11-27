[CmdletBinding()]
param(
    [string] $Url = 'https://lobby.purelogiccode.com/healthz',

    [int] $TimeoutSeconds = 15,

    [switch] $Quiet
)

$ErrorActionPreference = 'Stop'
try {
    $health = Invoke-RestMethod -Uri $Url -TimeoutSec $TimeoutSeconds
    if ($health.status -ne 'ok') {
        throw "Unexpected health status '$($health.status)'."
    }

    if (-not $Quiet) {
        $health | ConvertTo-Json -Compress
    }

    exit 0
}
catch {
    if (-not $Quiet) {
        Write-Error "FiveDo lobby health check failed: $($_.Exception.Message)"
    }

    exit 1
}
