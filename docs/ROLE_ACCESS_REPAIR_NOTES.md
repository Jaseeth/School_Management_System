# Access and feature repair notes — 19 October 2026

## What changed

Student reads now respect teacher class/subject assignments and section-head assignments. Admissions require school leadership. Principal/Deputy permissions are restored, with Admin-only operations preserved. Staff administration protects self accounts and leadership accounts, records audit history and invalidates older sessions. Parent schedules only resolve linked children. Student import/export, attendance overview, Audit Logs, explicit not-found screens and notification history are connected.

Migration `20261019124753_RoleAccessBaseline` was applied to SchoolManagement_Dev. It adds missing role-permission links only and does not alter students, marks, attendance or leave data. The migration is additive; its Down deliberately preserves grants because they might predate this release. Use explicit audited policy changes to remove grants later.

## Running the update

1. Stop the current API and restart it with the existing scripts/start-api.ps1 command.
2. Refresh the frontend. Vite will rebuild the changed source; production needs a fresh npm run build and deployment.
3. Sign in once again in each portal in use. Older JWTs lack the new security-stamp claim. This one-time sign-in is expected; the update does not erase other portal sessions.
4. In later deployments run dotnet ef database update against the intended database using the Infrastructure project and API startup project. Development seeding is not a production bootstrap process.

## Configuration for a school

- `School:TimeZoneId`: an installed IANA/Windows timezone ID, for example Asia/Colombo. Fallback is Attendance:SchoolTimeZoneId, then Asia/Colombo. Calendar country alone does not select a timezone.
- Frontend `VITE_API_BASE_URL`: optional; defaults to same-origin /api. Use the existing Vite API proxy locally. If deploying the API on a different origin, set the correct base URL and `Cors:Origins` to the exact frontend HTTPS origins. No wildcard/placeholder origin is enabled by this change.
- Keep JWT signing keys, database credentials, SMTP credentials, Calendarific keys and Firebase service-account credentials in the deployment secret store. Do not put them in frontend environment variables.
- Browser cached user profiles do not authorize an API request. Live identity status, roles and security stamp are verified by middleware; active near-expiry sessions renew, while expired/revoked sessions display a sign-in notice.

## Verification

The repair's initial integration run passed 57 cases. A further signed-JWT HTTP test passed for teacher/head/principal/deputy, route authorization and revoked sessions. The final regression run also includes blank Excel template round-trip and rejection of formulas/excessive row ranges. Final complete integration suite: **60 passed, 0 failed, 0 skipped**. Backend compilation and frontend production build passed. Frontend lint exits successfully with existing warnings; the newly added transfer helper no longer adds a fast-refresh warning. Visual Studio solution inventory contains every frontend source file without duplicate entries.

Browser checks used a temporary synthetic-data fixture and verified notification search/read status, staff edit submission, attendance results and audit snapshots. The fixture is removed after verification. This is not a claim that every role was manually signed in against live school data.

## Remaining setup and release checks

- Browser push is now implemented and locally configured; see BROWSER_PUSH_NOTIFICATIONS.md. Firebase server authentication is verified, and the browser/device Allow + Send test alert check remains necessary. Existing SMTP and real delivery on each target device still need staging acceptance.
- Production hosting, secret provisioning and a backup/restore rehearsal depend on the actual hosting/database environment. Before release, take a SQL backup, restore it into a separate disposable database, verify counts/migrations and test the application against that restored database. Never rehearse by overwriting the live school database.
- Existing dependency warnings include System.Security.Cryptography.Pkcs 9.0.11 advisories. Resolve against the deployment dependency policy and rerun regression tests before public release; this repair did not silently upgrade unrelated packages.
- The application still needs staged acceptance as all actual roles, especially newly created non-teaching staff accounts and browser/device combinations.
- Staff attendance redesign, exam/test workflow reconsideration, homework and promotion usability remain on hold as requested.
