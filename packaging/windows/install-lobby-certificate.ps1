[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $CertificatePath,

    [Parameter(Mandatory = $true)]
    [string] $PrivateKeyPath,

    [string] $SiteName = 'FiveDoLobby',

    [string] $HostName = 'lobby.purelogiccode.com',

    [string] $OldThumbprintToRemove = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell session.'
}

Import-Module WebAdministration -ErrorAction Stop
foreach ($path in @($CertificatePath, $PrivateKeyPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "File not found: $path"
    }
}

$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
$pem = Get-Content -LiteralPath $PrivateKeyPath -Raw
$rsa = [System.Security.Cryptography.RSA]::Create()
$pfxPath = $null
try {
    $rsa.ImportFromPem($pem)
    $withKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey(
        $cert,
        $rsa)
    if (-not $withKey.HasPrivateKey) {
        throw 'The private key did not attach to the certificate.'
    }

    $pfxPassword = [Guid]::NewGuid().ToString('N')
    $pfxPath = Join-Path $env:TEMP 'fivedo-lobby-origin.pfx'
    [IO.File]::WriteAllBytes(
        $pfxPath,
        $withKey.Export(
            [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
            $pfxPassword))

    $securePassword = ConvertTo-SecureString $pfxPassword -AsPlainText -Force
    $imported = Import-PfxCertificate `
        -FilePath $pfxPath `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -Password $securePassword

    $binding = Get-WebBinding -Name $SiteName -Protocol https -HostHeader $HostName -ErrorAction SilentlyContinue
    if (-not $binding) {
        $null = New-WebBinding `
            -Name $SiteName `
            -Protocol https `
            -Port 443 `
            -HostHeader $HostName `
            -SslFlags 1
    }

    $appId = '{A7B0F7F0-6B8E-4F1B-9D2A-3C5E7F9A1B2C}'
    & netsh http delete sslcert hostnameport="${HostName}:443" 2>$null | Out-Null
    & netsh http add sslcert `
        hostnameport="${HostName}:443" `
        certhash=$($imported.Thumbprint) `
        appid="$appId" `
        certstorename=MY | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "netsh could not bind the certificate (exit $LASTEXITCODE)."
    }

    Restart-WebAppPool -Name (Get-Website -Name $SiteName).ApplicationPool

    $health = & curl.exe -s -k --resolve "${HostName}:443:127.0.0.1" "https://$HostName/healthz"
    if ($health -notmatch '"status"\s*:\s*"ok"') {
        throw "The lobby did not return a healthy response after rebinding: $health"
    }

    if ($OldThumbprintToRemove) {
        foreach ($store in @('My', 'CA')) {
            Get-ChildItem "Cert:\LocalMachine\$store" -ErrorAction SilentlyContinue |
                Where-Object { $_.Thumbprint -eq $OldThumbprintToRemove } |
                ForEach-Object { Remove-Item -LiteralPath $_.PSPath -Force }
        }
    }

    $san = ($imported.Extensions |
        Where-Object { $_.Oid.FriendlyName -eq 'Subject Alternative Name' }).Format($false)
    [pscustomobject]@{
        Thumbprint = $imported.Thumbprint
        Subject = $imported.Subject
        Issuer = $imported.Issuer
        NotAfter = $imported.NotAfter
        SAN = $san
        Health = $health
    } | Format-List
}
finally {
    $rsa.Dispose()
    if ($pfxPath -and (Test-Path -LiteralPath $pfxPath)) {
        Remove-Item -LiteralPath $pfxPath -Force
    }
}
