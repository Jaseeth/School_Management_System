param([string]$ReportDate = '2026-10-19')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$resultRoot = Join-Path $repoRoot '.local-tools\qa\results'
$browserRoot = Join-Path $repoRoot '.local-tools\qa\browser'
$inventory = Get-Content (Join-Path $repoRoot 'docs\PHASE1_NAVIGATION_INVENTORY.json') -Raw | ConvertFrom-Json
$lines = [System.Collections.Generic.List[string]]::new()
function Add([string]$value='') { $lines.Add($value) }
Add "# Phase 1 verification report — $ReportDate"
Add ''
Add '## Decision and scope'
Add ''
Add 'This report records executed automated checks and their limits. Passing these checks is not a promise of zero defects or a claim that every possible form input and device was tested. Existing workflows were preserved; changes were limited to navigation/performance polish, accessibility labels, garbled text and verified defects.'
Add ''
Add '## Executed suites'
Add ''
Add '| Suite | Passed | Failed | Skipped | Total | Evidence |'
Add '|---|---:|---:|---:|---:|---|'
$allResults = @()
$suites = @('UnitTests','IntegrationTests','BrowserTests')
if (Test-Path (Join-Path $resultRoot 'RestoreUiTests.trx')) { $suites += 'RestoreUiTests' }
if (Test-Path (Join-Path $resultRoot 'FileUploadUiTests.trx')) { $suites += 'FileUploadUiTests' }
foreach ($suite in $suites) {
    [xml]$trx = Get-Content (Join-Path $resultRoot "$suite.trx")
    $counter = $trx.TestRun.ResultSummary.Counters
    Add "| $suite | $($counter.passed) | $($counter.failed) | $($counter.notExecuted) | $($counter.total) | .local-tools/qa/results/$suite.trx |"
    foreach ($result in $trx.TestRun.Results.UnitTestResult) { $allResults += [pscustomobject]@{Suite=$suite;Name=$result.testName;Outcome=$result.outcome} }
}
Add ''
$failedCases = @($allResults | Where-Object Outcome -ne 'Passed')
if ($failedCases.Count -eq 0) { Add '**Automated suite status: PASS for the executed cases. Manual/device release checks below remain open.**' }
else { Add "**Automated suite status: NOT PASSED — $($failedCases.Count) cases are failed or not executed. Do not treat this report as sign-off.**" }
Add ''
Add 'Additionally, the five Node browser-push tests passed. The production frontend build passed. Frontend lint completed with existing React-hook/style warnings, not a warning-free result. The NuGet transitive audit after the package repair reported no known vulnerable packages; this is dependency scanning, not a penetration test.'
Add 'The saved npm audit also reported zero known vulnerabilities (info/low/moderate/high/critical all zero). Dependency audit files are in `.local-tools/qa/results/nuget-audit.json` and `npm-audit.json`.'
Add ''
Add '## Browser, role and viewport matrix'
Add ''
Add '- Actual installed Google Chrome, headless.'
if (Test-Path 'C:\Program Files\Google\Chrome\Application\chrome.exe') { Add "- Installed Chrome version: $((Get-Item 'C:\Program Files\Google\Chrome\Application\chrome.exe').VersionInfo.ProductVersion). Playwright .NET: 1.62.0." }
Add '- Playwright WebKit on Windows. This tests Safari engine compatibility, not native Safari on macOS/iOS.'
Add '- Desktop: 1440 × 1000. Mobile emulation: 390 × 844, touch enabled, device scale factor 2. No physical phone was connected.'
Add '- Roles: Admin, Principal, Deputy Principal, Section Head, Teacher, Student, Parent and a custom staff role (Librarian). Separate contexts isolate accounts.'
Add '- Separate API and production frontend on temporary ports, using generated synthetic users and a disposable SQL database. The working SchoolManagement_Dev database was not used for test mutations or restore.'
Add '- Chrome checks HTTPS certificate trust without bypass. Windows WebKit uses a localhost-only certificate exception because its trust store differs; this is not a trusted-certificate check in native Safari.'
Add '- Tests respect the real sign-in rate limiter by pacing logins. Deliberate wait time is not application load latency.'
Add ''
Add '| Engine / viewport | Page and tab checks | Failures | Evidence |'
Add '|---|---:|---:|---|'
$visits = @()
foreach ($file in Get-ChildItem $browserRoot -Filter 'navigation-*.json') {
    $data=Get-Content $file.FullName -Raw | ConvertFrom-Json
    $label=$file.BaseName.Replace('navigation-','').Replace('-False',' / desktop').Replace('-True',' / mobile emulation')
    Add "| $label | $($data.checks.Count) | $($data.failures.Count) | .local-tools/qa/browser/$($file.Name) |"
    $visits += $data.checks
}
Add ''
Add 'Each navigation check opens the route, waits for page/network completion, checks document overflow, records JavaScript exceptions and API responses ≥400, and rejects missing/access-restricted/blank pages where access is expected. Named navigation tabs are clicked and checked as well. Mobile menus are explicitly opened and closed. Example dashboards and failures are captured as PNGs.'
Add ''
Add '### Role coverage'
Add ''
Add '| Role | Recorded page/tab checks | Distinct URLs |'
Add '|---|---:|---:|'
foreach ($group in $visits | Group-Object role | Sort-Object Name) { Add "| $($group.Name) | $($group.Count) | $(($group.Group.path | Sort-Object -Unique).Count) |" }
Add ''
Add '## Links, forms and buttons: what was checked'
Add ''
Add "- Source inventory: $($inventory.routes.Count) registered routes, $($inventory.links.Count) literal internal links, $($inventory.unmatched.Count) unmatched links, $($inventory.forms.Count) form-containing source files. Template/dynamic links are not proven correct by a literal-link scan; seeded student/parent detail routes are additionally exercised in the browser."
$concretePaths=@($visits.path | ForEach-Object { ($_ -split '\?')[0] } | Sort-Object -Unique)
$concretePaths += @('/','/login','/student/login','/parent/login','/student/forgot-password','/staff/forgot-password','/student/register','/change-password')
$missingRoutes=@($inventory.routes | Where-Object { $route=$_; $route -ne '*' -and !($concretePaths | Where-Object { $_ -match ('^'+($route -replace ':[^/]+','[0-9]+')+'$') }) })
Add "- Runtime route coverage: $(($inventory.routes | Where-Object { $_ -ne '*' }).Count - $missingRoutes.Count) / $(($inventory.routes | Where-Object { $_ -ne '*' }).Count) non-wildcard route patterns represented by executed navigation/public/password-flow cases. The unknown-route fallback is checked separately."
if($missingRoutes.Count) { Add "- Route patterns without a recorded visit: $($missingRoutes -join ', ')." }
Add '- Public pages, unknown-route fallback, empty-login validation, password visibility controls, enabled-control pointer cursors, and secure-context status are exercised in both engines and viewports.'
Add '- Submitted browser workflows: school settings save/reload; announcement publish and student/parent audience separation; academic section creation/reload; manual holiday save; teacher leave application, Admin approval and staff notification; forced temporary-password change.'
Add '- Actual HTTP authentication/authorization checks verify Admin-only backups, leader-only settings options and publishing access for school leaders. Role controls are not checked only by hiding menu items.'
Add '- Database tests cover registration and rollback, class/subject assignments, scoped access, leave approval/rejection/cancellation/coverage, yearly allowance and working-day calculations, calendar import/deduplication, school events, assessments, promotion/repeat/graduation/undo, marks review/publication, reports/PDFs, notification ownership/recipients, school settings concurrency and real native backup/restore.'
Add '- Backup restoration is executed only in an isolated database and checks safety-backup retention, recovered state, changed security stamps, file tampering and schedule idempotence.'
Add '- A focused Chrome/WebKit browser suite additionally exercises backup creation/download, restore-dialog cancellation, a confirmed restore and successful sign-in with recovery of the earlier school name. Its evidence is reported separately when run outside the main browser batch.'
Add '- Focused mobile Chrome/WebKit cases download a government-calendar CSV template, upload a CSV, preview selected rows and import them; a direct read verifies the resulting holiday persisted in that isolated database.'
Add '- Browser-push worker tests check consent/account/portal separation and delayed messages after sign-out. They do not prove real OS notification delivery.'
Add ''
Add '**Coverage limit:** this is not a claim that every button and every conditional form state was submitted through the UI. Destructive operations, OTP delivery and external-provider/device behaviour need the checks below. Form inventory is included so remaining cases can be tracked explicitly.'
Add ''
Add '### Form inventory'
Add ''
Add '| Source file | Form declarations |'
Add '|---|---:|'
foreach ($form in $inventory.forms) { Add "| $($form.file) | $($form.forms) |" }
Add ''
Add '## Fixes made during verification'
Add ''
Add '| Finding | Change | Verification |'
Add '|---|---|---|'
Add '| Enabled native controls had inconsistent cursors | Shared pointer cursor rule; disabled controls use not-allowed | Browser computed-style assertions |'
Add '| All portal pages loaded in the initial bundle | Lazy page imports with an accessible loading state | Production build and browser navigation |'
Add '| Dashboard contained garbled punctuation | Replaced the damaged separator/dash | Source scan and screenshots |'
Add '| Principal/Deputy Principal results page called a forbidden publishing API | Added missing Marks.Review and Marks.Publish grants to the baseline and additive migration | Role navigation, HTTP access tests, integration baseline assertions |'
Add '| Existing transitive cryptography package had known advisories | Pinned System.Security.Cryptography.Pkcs 9.0.21, the patched maintenance version | Build, regression tests and saved NuGet audit |'
Add '| Staff sign-in labels were not associated with their inputs; eye button had no accessible name | Added label associations and show/hide password label | Browser sign-in/visibility checks and forced-password workflow |'
Add '| Local frontend used HTTP | Added ignored localhost certificate export/HTTPS launcher; Vite detects the certificate | Trusted Chrome HTTPS checks |'
Add ''
Add 'The permission migration is additive and was applied to the development database. It changes grants, not student records. The tests run with separate build output so the running Debug API does not lock test assemblies.'
Add ''
Add '## Performance evidence'
Add ''
Add 'The initial JavaScript entry decreased from approximately **938 KB to 300 KB** (about **68% smaller**, uncompressed). The current entry is approximately 94 KB gzip. Other pages now load when visited. This is an asset-size improvement; no claim is made about a measured percentage improvement on real mobile networks. Per-route local timings are in the browser JSON.'
Add ''
Add '## HTTPS usage'
Add ''
Add 'Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-web.ps1`, then open **https://localhost:5173**. Private certificate files remain in ignored `.local-tools/https/`. Browser security indicators depend on a valid trusted TLS certificate; no fake padlock was added to the page. Public deployment still needs its real domain, reverse-proxy/server HTTPS configuration and certificate.'
Add 'The existing frontend was verified over HTTPS. Do not launch a second server on port 5173; stop the existing frontend first when using the launcher.'
Add 'HTTP and HTTPS have separate browser storage. On the first HTTPS visit, sign in again and enable browser alerts there if desired. If notifications were previously enabled for the old HTTP site, remove that old site permission in browser settings to avoid stale/duplicate alerts. Existing HTTP sessions/consent cannot be copied across origins automatically.'
Add ''
Add '## Required manual / environment-specific release checks'
Add ''
Add '| Check | Why it remains separate |'
Add '|---|---|'
Add '| Native Safari on supported macOS/iPhone/iPad | Apple hardware/Safari was not available on this Windows host |'
Add '| Physical Android and iOS touch, keyboard, rotation and file-picker behaviour | Viewport/touch emulation does not reproduce every device interaction |'
Add '| Real Firebase OS/browser notifications for all recipient roles | Tests intentionally do not send messages to working school accounts/devices |'
Add '| Real email/OTP arrival and password-reset completion through the configured mail provider | External delivery and inbox/spam behaviour require a working test inbox/provider |'
Add '| Full destructive UI scenarios and every conditional form state | Automated evidence covers the named scenarios, not every possible action sequence |'
Add '| Production domain TLS, network speed, realistic large datasets and concurrent-user load | Local synthetic-data tests do not certify a deployment or capacity |'
Add ''
Add 'These limits prevent calling the entire production system mistake-free. Existing deferred workflow redesigns (staff attendance and exam/test flow) remain deferred.'
Add ''
Add '## Repeat after future changes'
Add ''
Add 'Yes—more features and changes can be added, and the same regression suite can be rerun. Add cases for new behaviour rather than relying on old coverage. Run:'
Add ''
Add '```powershell'
Add 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-phase1.ps1'
Add 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\write-phase1-report.ps1'
Add '```'
Add ''
Add 'See `tests/README.md` for setup, evidence paths and focused runs. Review failed cases before release; a report generated from failing results must not be treated as a pass.'
Add ''
Add '## Executed xUnit test cases'
Add ''
Add '| Suite | Case | Outcome |'
Add '|---|---|---|'
foreach ($result in $allResults) { Add "| $($result.Suite) | $($result.Name.Replace('|','/')) | $($result.Outcome) |" }
Add ''
Add '## Technical references'
Add ''
Add '- [Playwright browser and WebKit scope](https://playwright.dev/dotnet/docs/browsers)'
Add '- [Microsoft cryptography package advisory and patched versions](https://github.com/advisories/GHSA-846f-f764-76mx)'
$output=Join-Path $repoRoot "docs\PHASE1_QA_REPORT_$ReportDate.md"
Set-Content -LiteralPath $output -Value $lines -Encoding utf8
Write-Output "Report written: $output"
