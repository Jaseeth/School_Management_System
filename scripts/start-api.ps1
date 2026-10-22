param(
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\SchoolManagement.API\SchoolManagement.API.csproj'
$settingsPath = Join-Path $projectRoot 'src\SchoolManagement.API\appsettings.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$connectionSettings = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.ConnectionStrings.DefaultConnection)
if ($connectionSettings.DataSource -ne '(localdb)\MSSQLLocalDB') {
    throw 'This development launcher expects the existing MSSQLLocalDB instance.'
}

function Invoke-LocalDbCommand {
    param([string[]]$CommandArguments)
    # Windows PowerShell 5.1 turns native stderr into terminating errors when
    # ErrorActionPreference is Stop. Capture the complete diagnostic first.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $commandOutput = & sqllocaldb @CommandArguments 2>&1
        $commandExitCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previousPreference }
    return @{ ExitCode = $commandExitCode; Output = ($commandOutput | ForEach-Object { $_.ToString() }) -join "`n" }
}

$pipe = ''
$instanceInfo = ''
# Registry lookup can disagree with an already-running LocalDB process.
# Only reuse a process owned by this account and using this exact instance path.
$instanceDirectory = Join-Path $env:LOCALAPPDATA 'Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB'
$accountName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$sqlProcesses = Get-CimInstance Win32_Process -Filter "name='sqlservr.exe'" -ErrorAction SilentlyContinue
foreach ($sqlProcess in $sqlProcesses) {
    if (-not $sqlProcess.CommandLine -or $sqlProcess.CommandLine.IndexOf($instanceDirectory, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
    $owner = Invoke-CimMethod -InputObject $sqlProcess -MethodName GetOwner -ErrorAction SilentlyContinue
    if ("$($owner.Domain)\$($owner.User)" -ne $accountName) { continue }
    $instanceMatch = [regex]::Match($sqlProcess.CommandLine, '(?i)-s(LOCALDB#[A-Za-z0-9]+)')
    if (-not $instanceMatch.Success) { continue }
    $candidatePipe = "np:\\.\pipe\$($instanceMatch.Groups[1].Value)\tsql\query"
    $candidateSettings = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionSettings.ConnectionString)
    $candidateSettings['Data Source'] = $candidatePipe
    $candidateSettings['Connect Timeout'] = 5
    $candidateConnection = [System.Data.SqlClient.SqlConnection]::new($candidateSettings.ConnectionString)
    try {
        $candidateConnection.Open()
        $pipe = $candidatePipe
        Write-Host 'Using the verified running LocalDB instance.'
        break
    }
    catch { continue }
    finally { $candidateConnection.Dispose() }
}

if (-not $pipe) {
$startup = Invoke-LocalDbCommand -CommandArguments @('start', 'MSSQLLocalDB')
if ($startup.ExitCode -ne 0) {
    throw "LocalDB could not start for Windows account '$([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)'. Run from a regular PowerShell window under your normal account. Full error:`n$($startup.Output)"
}
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    $info = Invoke-LocalDbCommand -CommandArguments @('info', 'MSSQLLocalDB')
    if ($info.ExitCode -ne 0) { throw "Cannot read LocalDB instance information:`n$($info.Output)" }
    $instanceInfo = $info.Output
    # Some versions return a bare named pipe; others prefix it with np:.
    $pipeMatch = [regex]::Match($instanceInfo, '(?i)(?:np:)?\\\\\.\\pipe\\[^\s\x00]+')
    if ($pipeMatch.Success) {
        $pipe = $pipeMatch.Value
        if (-not $pipe.StartsWith('np:', [StringComparison]::OrdinalIgnoreCase)) { $pipe = "np:$pipe" }
        break
    }
    Start-Sleep -Milliseconds 500
}
if (-not $pipe) {
    # Keep the original instance address if this runtime omits pipe details.
    # The connection check below still verifies the actual database.
    Write-Host 'LocalDB did not expose its pipe. Checking the existing instance address.'
    $pipe = $connectionSettings.DataSource
}
}

# Resolve the pipe at each launch; it changes when LocalDB restarts.
$connectionSettings['Data Source'] = $pipe
$connectionSettings['Connect Timeout'] = 15
$connection = [System.Data.SqlClient.SqlConnection]::new($connectionSettings.ConnectionString)
try {
    $connection.Open()
    Write-Host "Database connection verified: $($connectionSettings.InitialCatalog)"
}
catch {
    throw "Could not connect to LocalDB for '$([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)'. Instance information:`n$instanceInfo`nDatabase error: $($_.Exception.Message)"
}
finally { $connection.Dispose() }

if ($CheckOnly) { return }

# A previous `dotnet run` can still be serving the API while this launcher tries
# to rebuild the same Debug output. Stop only this project's apphost so its DLLs
# are released before the build starts; leave other SchoolManagement.API
# processes (for example, another checkout) untouched.
$apiOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'src\SchoolManagement.API\bin\Debug\net10.0')).TrimEnd('\')
$apiProcesses = @(Get-Process -Name 'SchoolManagement.API' -ErrorAction SilentlyContinue)
$projectApiProcesses = @($apiProcesses | Where-Object {
    try {
        $processPath = $_.Path
        $processPath -and [System.IO.Path]::GetFullPath($processPath).StartsWith(
            "$apiOutputDirectory\",
            [StringComparison]::OrdinalIgnoreCase
        )
    }
    catch { $false }
})

foreach ($apiProcess in $projectApiProcesses) {
    Write-Host "Stopping the existing API process (PID $($apiProcess.Id)) before rebuilding."
    Stop-Process -Id $apiProcess.Id -ErrorAction Stop
    try { $apiProcess.WaitForExit(10000) } catch { }
}

& dotnet run --project $projectPath --launch-profile https -- --ConnectionStrings:DefaultConnection $connectionSettings.ConnectionString
if ($LASTEXITCODE -ne 0) { throw "API exited with code $LASTEXITCODE. Check the final error above." }
