# Phase 1 regression testing

Run from the repository root in PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-phase1.ps1
```

The existing LocalDB instance must be running, Google Chrome installed, Node dependencies restored, and the .NET SDK available. The script installs Playwright Chromium/WebKit when needed. `-SkipBrowser` runs the frontend checks, xUnit unit tests and integration tests; `-BrowserOnly` reruns the browser suite against the already-built frontend (`npm run build` first after frontend changes).

## Safety

Tests create uniquely named disposable databases (`SchoolManagement_E2E_*`, `SchoolManagement_AssignmentTests_*`, student-registration fixture databases). Browser tests start an actual separate API on a free port and serve the production frontend on a separate free port. Generated test account passwords exist only in memory and child-process environment variables. Your development school database and logged-in browser profiles are not used. Test fixture processes are terminated and their database is removed on completion. Backup/restore testing restores only its own fixture database.

## Suites and evidence

- `SchoolManagement.UnitTests`: calendar-file parsing and validation, ranges, leap dates and promotion undo state serialization.
- `SchoolManagement.IntegrationTests`: real SQL transactions and data, role scopes, assignments, registration, leave/cancellation/cover, holidays, promotion/undo, marks, reports/PDF, notifications, school settings, native backup/restore.
- `SchoolManagement.E2ETests`: real HTTP/authentication middleware and Playwright Chrome/WebKit tests, desktop 1440×1000 and touch/mobile 390×844; role navigation, rendered tabs, runtime/API errors, document overflow, login validation, pointer cursors, settings and announcement persistence/recipient separation, academic setup, manual holidays and teacher-leave approval.
- `school-management-web/tests`: Node tests of real browser-push worker and IndexedDB ownership/consent behaviour.
- `node scripts/audit-navigation.mjs`: inventories literal internal links and form-containing source files, verifies links match registered routes. This is source analysis, not proof every interaction has been executed.

TRX results: `.local-tools/qa/results/`. Browser JSON and screenshots: `.local-tools/qa/browser/`. These local artifacts are ignored by Git and can contain synthetic test data. Latest human-readable audit: `docs/PHASE1_QA_REPORT_2026-10-19.md`.

## HTTPS and browser limits

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-web.ps1` to export the existing development certificate and serve the frontend over HTTPS. Open **https://localhost:5173**. If the certificate is not trusted, run `dotnet dev-certs https --trust` and accept the Windows prompt. Private development certificate files stay in ignored `.local-tools/https/`.

If the frontend is already running, use its HTTPS URL; stop that server before starting a second one on port 5173.

HTTP and HTTPS are separate browser origins: sign in again on the first HTTPS visit and enable notifications there. If the old HTTP site had notification permission, remove that old permission in browser settings to avoid old-origin alerts opening an obsolete HTTP URL.

Chrome tests verify certificate trust without ignoring certificate errors. Windows WebKit requires a local-test certificate exception because it does not consume the Windows development-certificate store. WebKit is a Safari-compatible rendering-engine check, not native Apple Safari certification. Physical iPhone/iPad and Android testing, native Safari, real browser push delivery, external email/OTP delivery and the public deployment certificate need separate environment/device checks. Production HTTPS requires the deployment's real domain and certificate; a padlock cannot be added with frontend markup.

## Future changes

Add a regression case for each bug or new behaviour, run the relevant tests while implementing, then run the full script before release. Keep expected role access aligned with the agreed workflow. Passing tests demonstrate the listed scenarios; they do not prove every possible input, network condition, device or user action.
