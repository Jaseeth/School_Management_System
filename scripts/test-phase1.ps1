param([switch]$SkipBrowser, [switch]$BrowserOnly)
$ErrorActionPreference = 'Stop'
if ($SkipBrowser -and $BrowserOnly) { throw 'Choose SkipBrowser or BrowserOnly, not both.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$instanceDirectory = Join-Path $env:LOCALAPPDATA 'Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB'
$accountName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
foreach ($sqlProcess in (Get-CimInstance Win32_Process -Filter "name='sqlservr.exe'")) {
    if (!$sqlProcess.CommandLine -or !$sqlProcess.CommandLine.Contains($instanceDirectory)) { continue }
    $owner = Invoke-CimMethod -InputObject $sqlProcess -MethodName GetOwner
    if ("$($owner.Domain)\$($owner.User)" -ne $accountName) { continue }
    $instanceMatch = [regex]::Match($sqlProcess.CommandLine, '(?i)-s(LOCALDB#[A-Za-z0-9]+)')
    if ($instanceMatch.Success) { $env:SCHOOL_TEST_SQL_SERVER = "np:\\.\pipe\$($instanceMatch.Groups[1].Value)\tsql\query"; break }
}
if (!$env:SCHOOL_TEST_SQL_SERVER) { throw 'Start the existing LocalDB instance before running tests. No school data is used by the tests.' }
$resultDirectory = Join-Path $repoRoot '.local-tools\qa\results'
New-Item -ItemType Directory -Force $resultDirectory | Out-Null
function Check-Exit([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed. Review the test output and .local-tools\qa artifacts." } }
if (!$BrowserOnly) {
Push-Location (Join-Path $repoRoot 'school-management-web')
try {
    & node ../scripts/audit-navigation.mjs; Check-Exit 'Navigation source audit'
    & npm.cmd run build; Check-Exit 'Frontend build'
    & npm.cmd run lint; Check-Exit 'Frontend lint'
    & npm.cmd run test:push; Check-Exit 'Browser push tests'
} finally { Pop-Location }
}
Push-Location $repoRoot
try {
    if (!$BrowserOnly) { foreach ($suite in @('UnitTests', 'IntegrationTests')) {
        & dotnet test "tests/SchoolManagement.$suite" -c Phase1Qa -p:UseAppHost=false --logger "trx;LogFileName=$suite.trx" --results-directory $resultDirectory --nologo -m:1
        Check-Exit $suite
    } }
    if (!$SkipBrowser) {
        $certDirectory = Join-Path $repoRoot '.local-tools\https'
        New-Item -ItemType Directory -Force $certDirectory | Out-Null
        & dotnet dev-certs https --export-path (Join-Path $certDirectory 'localhost.pem') --format Pem --no-password; Check-Exit 'Local HTTPS certificate'
        & dotnet build tests/SchoolManagement.E2ETests -c Phase1Qa -p:UseAppHost=false --nologo -m:1; Check-Exit 'Browser test build'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/SchoolManagement.E2ETests/bin/Phase1Qa/net10.0/playwright.ps1 install chromium webkit; Check-Exit 'Browser installation'
        & dotnet test tests/SchoolManagement.E2ETests -c Phase1Qa --no-build --logger 'trx;LogFileName=BrowserTests.trx' --results-directory $resultDirectory --nologo
        Check-Exit 'Browser tests'
    }
} finally { Pop-Location }
