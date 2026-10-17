$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$toolPath = Join-Path $workspacePath '.local-tools/mailpit'
$executablePath = Join-Path $toolPath 'mailpit.exe'
$archivePath = Join-Path $toolPath 'mailpit-windows-amd64.zip'
$version = 'v1.31.5'
$expectedHash = '9637e21c02b33185fe69268ba05317152a5d8ac7877115e8a70e29b7e199e8dc'

if (!(Test-Path -LiteralPath $executablePath)) {
    New-Item -ItemType Directory -Path $toolPath -Force | Out-Null
    Invoke-WebRequest -Uri "https://github.com/axllent/mailpit/releases/download/$version/mailpit-windows-amd64.zip" -OutFile $archivePath
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'The Mailpit download checksum does not match the official release.'
    }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $toolPath -Force
}

$health = $null
try { $health = Invoke-RestMethod -Uri 'http://127.0.0.1:8025/api/v1/info' -TimeoutSec 2 } catch { }
if (!$health) {
    if (Get-NetTCPConnection -State Listen -LocalPort 8025,1025 -ErrorAction SilentlyContinue) {
        throw 'Port 8025 or 1025 is already in use. Stop the conflicting service before starting Mailpit.'
    }
    Start-Process -FilePath $executablePath -WorkingDirectory $toolPath -WindowStyle Hidden -ArgumentList @(
        '--listen', '127.0.0.1:8025', '--smtp', '127.0.0.1:1025', '--database', 'mailpit.db'
    )
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try { $health = Invoke-RestMethod -Uri 'http://127.0.0.1:8025/api/v1/info' -TimeoutSec 2; break } catch { Start-Sleep -Milliseconds 250 }
    }
    if (!$health) { throw 'Mailpit did not start. Run the executable with --help to check its configuration.' }
}
Write-Host 'Mailpit is ready: http://localhost:8025 (SMTP: 127.0.0.1:1025).'
