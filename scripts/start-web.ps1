param([int]$Port = 5173)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$certDirectory = Join-Path $repoRoot '.local-tools\https'
New-Item -ItemType Directory -Force -Path $certDirectory | Out-Null
$certPath = Join-Path $certDirectory 'localhost.pem'
& dotnet dev-certs https --export-path $certPath --format Pem --no-password
if ($LASTEXITCODE -ne 0) { throw 'Unable to export the development HTTPS certificate.' }
$env:SCHOOL_HTTPS_CERT = $certPath
$env:SCHOOL_HTTPS_KEY = Join-Path $certDirectory 'localhost.key'
Push-Location (Join-Path $repoRoot 'school-management-web')
try { & npm.cmd run dev -- --host localhost --port $Port --strictPort }
finally { Pop-Location }
