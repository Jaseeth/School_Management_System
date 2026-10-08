param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))

# Read-only diagnosis. Never print connection strings or authentication material.
$ErrorActionPreference = 'Stop'
try {
    $apiPath = Join-Path $ProjectRoot 'src/SchoolManagement.API'
    $baseConfig = Get-Content (Join-Path $apiPath 'appsettings.json') -Raw | ConvertFrom-Json
    $devConfig = Get-Content (Join-Path $apiPath 'appsettings.Development.json') -Raw | ConvertFrom-Json
    $connectionValue = $baseConfig.ConnectionStrings.DefaultConnection
    if ($devConfig.ConnectionStrings.DefaultConnection) { $connectionValue = $devConfig.ConnectionStrings.DefaultConnection }
    $projectXml = [xml](Get-Content (Join-Path $apiPath 'SchoolManagement.API.csproj') -Raw)
    $secretId = $projectXml.Project.PropertyGroup.UserSecretsId | Select-Object -First 1
    $secretPath = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretId/secrets.json"
    if (Test-Path -LiteralPath $secretPath) {
        $secretConfig = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
        if ($secretConfig.'ConnectionStrings:DefaultConnection') { $connectionValue = $secretConfig.'ConnectionStrings:DefaultConnection' }
    }
    if ($env:ConnectionStrings__DefaultConnection) { $connectionValue = $env:ConnectionStrings__DefaultConnection }
    if (!$connectionValue) { throw 'Database configuration missing' }
    $connectionBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($connectionValue)
    $connectionBuilder['Connect Timeout'] = 8
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionBuilder.ConnectionString)
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 15
    $command.CommandText = @'
SELECT s.Id AS StaffId, s.StaffNumber, s.FullName, s.IsActive,
       CASE WHEN s.ApplicationUserId=u.Id THEN 1 ELSE 0 END AS AccountLinked,
       r.Name AS Role
FROM AspNetUsers u
LEFT JOIN Staff s ON s.ApplicationUserId=u.Id
LEFT JOIN AspNetUserRoles ur ON ur.UserId=u.Id
LEFT JOIN AspNetRoles r ON r.Id=ur.RoleId
WHERE u.NormalizedEmail='SECTIONHEAD@SCHOOL.COM';

SELECT sha.Id, s.StaffNumber, sec.Name AS SectionName,
       ay.Name AS AcademicYear, sha.IsActive
FROM SectionHeadAssignments sha
JOIN Staff s ON s.Id=sha.StaffId
JOIN AspNetUsers u ON u.Id=s.ApplicationUserId
JOIN Sections sec ON sec.Id=sha.SectionId
JOIN AcademicYears ay ON ay.Id=sha.AcademicYearId
WHERE u.NormalizedEmail='SECTIONHEAD@SCHOOL.COM';

SELECT ms.Id AS SubmissionId, ms.Status, ms.SubmittedAt,
       ay.Name AS AcademicYear, t.Name AS TermName, e.Name AS ExamName,
       sec.Name AS SectionName, g.Name AS GradeName, c.Name AS ClassName,
       sub.Name AS SubjectName, s.StaffNumber AS TeacherNumber,
       ta.AcademicYearId, sec.Id AS SectionId, s.Id AS TeacherStaffId,
       CASE WHEN EXISTS (
           SELECT 1 FROM SectionHeadAssignments sha
           JOIN Staff reviewer ON reviewer.Id=sha.StaffId
           JOIN AspNetUsers u ON u.Id=reviewer.ApplicationUserId
           WHERE u.NormalizedEmail='SECTIONHEAD@SCHOOL.COM'
             AND reviewer.IsActive=1 AND sha.IsActive=1
             AND sha.AcademicYearId=ta.AcademicYearId
             AND sha.SectionId=sec.Id AND reviewer.Id<>s.Id
       ) THEN 1 ELSE 0 END AS MatchingReviewerAssignment
FROM MarksSubmissions ms
JOIN Exams e ON e.Id=ms.ExamId
JOIN AcademicTerms t ON t.Id=e.AcademicTermId
JOIN TeacherAssignments ta ON ta.Id=ms.TeacherAssignmentId
JOIN AcademicYears ay ON ay.Id=ta.AcademicYearId
JOIN Staff s ON s.Id=ta.StaffId
JOIN SchoolClasses c ON c.Id=ta.SchoolClassId
JOIN Grades g ON g.Id=c.GradeId
JOIN Sections sec ON sec.Id=g.SectionId
JOIN Subjects sub ON sub.Id=ta.SubjectId
WHERE s.StaffNumber='T001' AND ay.Name='2030/2031';
'@
    $reader = $command.ExecuteReader()
    $labels = @('Reviewer account', 'Reviewer assignments', 'Teacher submissions')
    $resultIndex = 0
    do {
        Write-Output $labels[$resultIndex]
        $rows = @()
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($column = 0; $column -lt $reader.FieldCount; $column++) {
                $row[$reader.GetName($column)] = if ($reader.IsDBNull($column)) { $null } else { $reader.GetValue($column) }
            }
            $rows += [pscustomobject]$row
        }
        ConvertTo-Json -InputObject $rows -Depth 4
        $resultIndex++
    } while ($reader.NextResult())
    $reader.Close()
} catch {
    $failure = $_.Exception
    while ($failure.InnerException) { $failure = $failure.InnerException }
    Write-Output ('Read-only database check failed: ' + $failure.GetType().Name)
    Write-Output ('Script line: ' + $_.InvocationInfo.ScriptLineNumber)
    if ($failure -is [System.ArgumentException]) {
        Write-Output ('Argument: ' + $failure.ParamName)
        if ($failure.Message -match "Keyword not supported: '([a-zA-Z ]+)'") {
            Write-Output ('Unsupported connection keyword: ' + $Matches[1])
        }
    }
    if ($failure -is [System.Data.SqlClient.SqlException]) {
        Write-Output ('SQL error number: ' + $failure.Number)
    }
    exit 1
} finally {
    if ($connection) { $connection.Dispose() }
}
