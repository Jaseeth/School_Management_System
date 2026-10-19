# Feature and role-access audit — 19 October 2026

## Repair update — 19 October 2026

The findings below describe the original audit. The following repairs have now been implemented:

- Student directory, details and enrollment queries use assigned section/year/class/subject scope. Legacy admission requires leadership and Students.Create. Teachers and section heads have view-only admission controls.
- A versioned, additive permission baseline restores the intended Principal/Deputy management and Section Head/Teacher lookup grants. Migration `20261019124753_RoleAccessBaseline` is applied to SchoolManagement_Dev. Existing unrelated grants are preserved.
- JWTs include a security stamp; middleware rejects inactive identities and stale role/stamp claims. Account status/role/password administration revokes old sessions. Near-expiry active portal sessions renew; failures show a notice without clearing other portal sessions. Existing tokens require one sign-in after installing this update.
- General student attendance overview and Audit Logs pages are connected. Unknown URLs show a not-found page. Student import/export, a blank Excel template and validation/confirmation controls work. Import rejects formulas, excessive rows and excessively expanded ZIP content.
- Staff editing, activate/deactivate and Admin password reset are connected with transaction/audit/session invalidation, self-account protection and restrictions on who may manage leadership roles. Account creation follows the same leadership-role restriction. Audit Logs provides staff change history.
- Parents can open their linked child's weekly timetable and approved special classes. Non-teaching staff roles have a basic dashboard with their leave, profile, calendar and notifications.
- All three portals now have paginated notification history, search, date/status filters and ownership-checked read actions. In-app history is available beyond the previous latest-50/latest-100 cap.
- School clocks now use `School:TimeZoneId`, falling back to `Attendance:SchoolTimeZoneId`, then Asia/Colombo. Country selection does not guess a timezone; deployment must set it for the school.
- Frontend API default is same-origin /api; production cross-origin allowances come from Cors:Origins rather than a placeholder domain. New frontend files remain visible in the Visual Studio solution.

Validation: 57 integration cases passed in the first repair run, plus a separate real HTTP authorization check using signed synthetic JWTs. That check exercised the permission provider, account middleware and controller role attributes for teacher/head/principal/deputy and rejected revoked tokens. Browser verification with synthetic data confirmed notification search/read state, staff edit submission, attendance results and audit snapshots. No live school records or notifications were used for these tests. Final full regression run: **60 passed, 0 failed, 0 skipped**. Details are recorded in ROLE_ACCESS_REPAIR_NOTES.md.

Still requiring deployment setup/acceptance: real SMTP and Firebase/mobile/background browser push delivery, hosting-specific CORS/timezone/secrets, a backup/restore rehearsal, and full staging acceptance as every real role. Those are not certified by local tests. The earlier deferred staff-attendance redesign, exams/tests, homework and promotion usability discussions remain deferred.

## Result

The application has substantial working workflows, but it is not ready to call access-complete. Fix authorization consistency and the unfinished existing screens before adding another module. No production application rules, live role grants or live student records were changed during this audit.

The most important findings are **student access outside assignments**, **an unguarded legacy admission action**, **missing principal/deputy/section-head permission grants**, and **existing-token access after account/role changes**.

## What was checked and what this proves

- Inventoried 59 controller source files containing 294 HTTP action declarations, 61 registered frontend route paths, and 28 staff-sidebar links. These are source inventory counts, not a claim that every HTTP permutation was manually exercised.
- Read current role grants from `SchoolManagement_Dev`, recording only role names and permission names. No personal records or secrets are included in the snapshot.
- Reviewed the role guards, permission handler, token/middleware lifecycle, student management, staff/parent administration, academic setup, scheduling, attendance, marks, reports, leave/coverage, announcements, calendar/events and portal integration.
- Ran the existing registration, feature/assignment and controller-discovery tests: **50 existing cases passed** after fixing a test-only connection override.
- Ran **one additional temporary audit probe**, which passed by reproducing the three insecure student-access behaviors described below. This was a characterization of defects, not an assertion that those behaviors are correct. The temporary probe was removed after recording the results.
- Final combined run: **51 passed, 0 failed, 0 skipped**, including that temporary probe. Tests used isolated databases and fake push/email dependencies where provided. No live notifications were sent.
- Frontend production build passed. It still reports the existing large-bundle warning.
- The initial broad run was 46 passed / 4 registration tests unable to connect: that older fixture hard-coded the LocalDB instance address and ignored the working named-pipe override. `StudentRegistrationTests` now supports `SCHOOL_TEST_SQL_SERVER`, matching the other fixture; its default remains unchanged. This is the only durable test-code change made for this audit.
- This is a source, configuration and targeted integration audit. It is not a complete browser acceptance pass as every real user, a penetration test, or a production deployment certification. Actual mobile push, real SMTP delivery, backups/restore and real-device accessibility were not verified.

Supporting files: `ROLE_ACCESS_ENDPOINT_INVENTORY_2026-10-19.json` and `ROLE_PERMISSION_SNAPSHOT_2026-10-19.json` in this directory. The earlier 7 October project review is historical and should not be used as the current completion list.

## Actual role configuration

| Role | Active permission grants | Consequence |
|---|---:|---|
| Admin | 30 | All currently referenced permission names are granted. Missing screens still prevent some workflows. |
| Principal | 0 | Role-protected features can work; permission-protected management features fail. |
| Deputy Principal | 0 | Same mismatch as Principal. |
| Section Head | 0 | Scoped role-based approvals/reports can work; permission-protected student/academic lookups fail. |
| Teacher | 3 | `Students.View`, `Marks.Enter`, `Exams.View`. Student viewing lacks assignment filtering. Academic lookup APIs are not granted. |
| Student | 0 | Normal: own-portal actions use role/profile identity, not staff management permissions. |
| Parent | 0 | Normal: own-portal actions use active guardian identity and child links. |

Do **not** grant every permission to every leader/head as a quick fix. Define an explicit capability matrix first. Principals/deputies need the intended management permissions; heads need scoped read/approval access, not whole-school user/role administration. Shared dropdown data should use a safe lookup policy or scoped endpoints, separate from editing academic setup.

## Confirmed priority fixes

### P1 — Student reads do not enforce assignment boundaries

Evidence: `StudentsController.cs`, `GetStudents` around line 270, `GetStudentsForManagement` around line 301 and `GetStudentDetails` around line 524. Queries use supplied filters/student ID but do not restrict results to the current staff member's assigned section/class/subject. The permission handler checks only permission names. Teachers currently receive `Students.View` in the live permission table and development seeder.

Reproduced: an isolated Teacher identity with `Students.View` and a class-teacher assignment to class A received a student from an unrelated section in the list and received that student's details. The detail response includes enrollment and guardian information. A caller-provided class/section filter is not an authorization boundary.

Implement: one reusable student scope for list, count/summary and detail actions. Leaders get approved whole-school access, heads only assigned section/year, teachers only assigned class/subject students. Check the scope before loading guardian/history details. Add negative tests for foreign student IDs and forged class/section filters. Granting head viewing permission must happen only alongside this scope fix.

### P1 — Legacy student creation bypasses the intended admissions roles

Evidence: `StudentsController.CreateStudent`, line 22, has no `Authorize`/`HasPermission` attribute. It manually finds a staff record by user ID, but checks neither the intended role nor `Students.Create`, assignment scope or active-account status. No global fallback authorization policy guards this action. The newer `StudentRegistrationManagementController` correctly restricts its workflow to Admin/Principal/Deputy Principal; the legacy action remains available independently.

Reproduced: an isolated Teacher identity created a student in an unrelated active class through the legacy action. Anonymous creation was **not** reproduced: the manual missing-user check returns unauthorized.

Implement: apply the agreed admissions role/permission and active account/staff checks, or retire the legacy action in favor of the guarded registration service. Keep create/list actions and frontend Add Student controls consistent. Do not expose admissions to teachers merely because they can view assigned students.

### P1 — Visible leader/head pages disagree with actual permissions

Evidence: the live permission snapshot above; `AcademicController` requires `AcademicSetup.View/Manage`, `StaffController` requires `Staff.View/Create`, `TeacherAssignmentsController` requires `TeacherAssignments.View/Manage`, and the existing exam metadata uses `Exams.View/Manage`. The sidebar/page guards advertise management access to Principal and Deputy Principal. The permission handler has no implicit principal/deputy bypass.

Consequences: these roles can enter the page but receive 403s. Teacher Students screens also request academic dropdown data for which Teacher has no grant. Section Head currently has no `Students.View`, even though the Students sidebar is shown. New role-based leave/calendar/announcement/report features working does not establish that older permission-based pages work.

Implement: a versioned, idempotent role baseline plus safe lookup policies, using tests that actually execute ASP.NET authorization. A successful direct controller unit/integration call does not prove a `HasPermission` attribute authorizes the real request. Preserve intentional Admin-only actions such as section-head assignment administration, calendar editing/allowance setup, and any explicitly Admin-only password reset rules.

### P1 — Existing tokens do not consistently reflect account/role changes

Evidence: `Program.cs` JWT validation checks signature/issuer/audience/lifetime but has no live account/role validation event. `ForcePasswordChangeMiddleware` checks account existence and `MustChangePassword`, but not `IsActive`. `PermissionAuthorizationHandler` trusts token role claims and does not check the user/staff account. Some controllers check current database roles/account state; others rely on attributes or active staff only. `UserRolesController.AssignRole` changes memberships without invalidating old JWT claims.

Reproduced: after setting the test identity account inactive, its existing claims still passed `Students.View` authorization and received student details. Source confirms the global middleware does not reject this inactive account. A downgraded role's old token is also not centrally invalidated; impact varies by endpoint.

Implement: central active-account and current token/session-version checks, consistent role-change/password-reset revocation behavior, and a controlled session renewal/re-login experience. Do not “solve” surprise expiry by indefinitely extending JWT lifetime. Browser cached user data currently has no expiry/renewal handling, so a stale logged-in screen can remain while APIs reject requests.

### P2 — Existing sidebar routes are unfinished

The route inventory found `/attendance`, `/audit-logs`, and `/parent/children` in `DashboardLayout` with no matching route. `/attendance` is advertised to leaders/heads and `/audit-logs` to leaders. They fall through to the wildcard redirect to Dashboard. The parent sidebar item is currently latent because parents normally use their dedicated portal, but it is still an inconsistent route definition.

Implement the general attendance overview with correct school/section scope and an Audit Logs page backed by the existing paginated API. Remove or correctly route the latent parent item. Add a route-inventory regression check and an explicit not-found page rather than silently redirecting all unknown paths.

### P2 — Student import/export buttons do nothing

Evidence: `StudentsPage.jsx` around lines 640–652 renders Import Students and Export Students buttons without click handlers. Backend import preview/confirm/error report and export APIs already exist and are role-restricted to leaders.

Implement the upload → preview → correct errors → confirm flow and a real export download, with leader-only buttons and server-side authorization. Do not add a duplicate import service.

### P2 — Admin-only student actions are shown to other viewing roles

Evidence: `StudentDetailsPage.jsx` guards Reset Password with `hasRole("Admin")`, but Generate Registration Code is rendered unconditionally. Its API is Admin-only. StudentsPage also shows Add Student without a role-aware guard.

Implement capability-aware controls with a clear read-only student view for teachers/heads. Decide explicitly whether leaders other than Admin may issue codes; preserve the current server restriction until that decision is made.

### P2 — School date/time rules are inconsistent across countries

Evidence: `AttendanceWindowService` reads configurable `Attendance:SchoolTimeZoneId` and defaults to `Asia/Colombo`. `AnnualPromotionService.SchoolToday` hard-codes `Arab Standard Time`, also used for leave/cancellation date boundaries; some dashboards and special-class paths use server `DateTime.Today/Now`. Changing a calendar country code does not update any of these clocks.

Implement one configured school timezone/date service for attendance, leave history/cancellation, promotion, schedules and dashboards; test around midnight. This is needed for the stated Sri Lanka/international-school use. It does not require assuming that the application must host multiple schools in a shared database.

## Feature-by-feature review

In the table, “leaders” means Principal and Deputy Principal; Admin is listed separately. “Working path” means meaningful code/tests exist, not that every real-user browser interaction is certified. “Own” means identity-linked data; “scoped” means assigned section/class/subject/year.

| Feature | Admin | Principal / Deputy | Section head | Teacher | Student | Parent | Status / missing work |
|---|---|---|---|---|---|---|---|
| Login, recovery, password change | Own | Own | Own | Own | Own | Own | Implemented; registration tests passed. Central inactive-account/token handling needs P1 fix. |
| Dashboards | Whole school | Management | Section | Own assignments | Own portal | Linked children | Implemented; role summary paths exist. General attendance link is unfinished. |
| Student directory/details | Whole school | Intended whole school, currently permission-blocked | Intended scoped, currently blocked | Currently unscoped | Own portal only | Linked child profile only | P1 scope/grant fixes before expanding access. |
| Admissions/new student | Guarded wizard + unsafe legacy action | Guarded wizard, lookup/grant mismatch | Should not admit by default | Legacy API incorrectly allows creation | Self-account registration only | Account/child portal only | Secure legacy action; hide staff admissions controls from viewers. |
| Student import/export | Backend exists | Backend role access exists | No | No | No | No | Frontend buttons unimplemented. |
| Academic years/sections/grades/classes/subjects | Manage | Shown, permission-blocked | Safe lookup needed | Safe lookup needed | Portal data only | Portal data only | Fix role baseline and split lookup vs edit capabilities. |
| Section-head assignments | Manage | No under current policy | View assigned scope through features | No | No | No | Assignment CRUD/scope tests passed. Admin-only is intentional current policy. |
| Subject/class teacher assignments | Manage | Permission-based subject management blocked | Scoped class-teacher API exists | Own assignments | Own subjects | Child subjects in profile | Tests passed; consolidate consistent UI/capabilities for heads. |
| Weekly timetable and intervals | Manage | Role API exists; lookup grants can block UI | Scope-dependent feeds | Own lessons | Own class timetable | No dedicated child timetable page | Clash/assignment/interval tests passed. Add parent timetable if required. |
| Daily schedule / special classes | Manage | Manage | Section | Assigned teaching scope | Relevant classes | Receives family alerts | Lifecycle tests passed. Parent cannot open a dedicated child special-class schedule. |
| Student attendance entry | Oversight APIs | Oversight APIs | Assigned approval scope | Class teacher / valid temporary cover only | Own attendance | Linked child attendance | Teacher/window/correction tests passed; build leader/head overview page. |
| Attendance correction approval | Existing scope rules | Existing scope rules | Assigned section/year | Request within own scope | No | No | Regression checks passed; review dual-role UI access during acceptance. |
| Official marks entry/review/publishing | Existing leadership/publishing rules | Existing leadership rules | Section review/publish | Assigned entry; approved publishing / delegated review rules | Published own results | Published linked-child results | Existing approval and scoped publishing tests passed. No redesign while exams are on hold. |
| Reports / PDF exports | Whole school + people reports | Whole school + people reports | Scoped academic/class reports | Not provided by current report policy | Own portal views | Child portal views | PDF/data regression checks passed. Teacher reports/certificates are optional policy additions, not required new modules. |
| Analytics | Role-scoped API | Role-scoped API | Scoped API | Assigned API | No management analytics | No management analytics | No dedicated analytics frontend found. Optional after core fixes. |
| Promotion / repeat / undo | Manage | Manage | No management | No management | Own history | Child history/profile | Automatic promotion and undo tests passed; user's usability review remains deferred. Timezone unification still needed. |
| Parent/guardian management | Manage | Role API permitted | No management | No management | Own guardian list | Own profile/linked children | Registration/link workflows implemented; leaders' student lookup dependency needs baseline fix. |
| Staff profiles | Own | Own | Own | Own | Own student profile | Own parent profile | Implemented. Full staff edit/deactivate/reactivate and account-role lifecycle controls are missing. |
| Staff management | Create/list | UI shown, permission-blocked | No management | No management | No | No | Add controlled edit/status/role/reset flows with audit after central account revocation. |
| Staff leave / history / cancellation | Apply + review others | Apply + review others | Apply + section history | Own requests | No | No | Workflow/cancellation/history tests passed. Keep self-review restriction. |
| Leave replacement coverage | Arrange | Arrange | Assigned section | Assigned cover duties | Indirect class coverage | Indirect class coverage | Scope/conflict/history/revocation tests passed. |
| Leave allowance | Manage | Not currently allowed | Own balance | Own balance | No | No | Shared allowance and holiday exclusion tested; keep Admin-only policy unless explicitly changed. |
| Staff attendance | Leader recording | Leader recording | Section read-only | Not currently a page | No | No | Existing tests passed; workflow explicitly ON HOLD, no changes in this audit. |
| Announcements | Whole-school targeting | Whole-school targeting | Own section recipients | Own class/subject recipients | Targeted student feed | Parent-targeted feed | Targeting/privacy tests passed. Student announcements intentionally do not automatically become parent announcements. |
| Notifications | Own inbox | Own inbox | Own inbox | Own inbox | Own inbox | Own inbox | Staff ownership/read-state tests passed. No React FCM registration/service-worker integration found; background push delivery is not complete. |
| School calendar / public holidays / file import | Edit | View | View | View | View | View | Shared calculations/import validation tested. Admin alone edits base calendar; genuine external holiday freshness still depends on provider/government verification. |
| School events | Manage | Manage | Relevant events | Relevant events | Relevant events | Relevant events | Audience/change/cancel/closure tests passed. Calendar/event editing policies are distinct by design. |
| Government exam timetable / internal tests | Existing leader controls | Existing leader controls | Scoped internal tests | Scoped internal tests | Relevant calendar | Relevant calendar | Existing tests passed, but workflow explicitly ON HOLD pending reconsideration. Do not add more exam functionality now. |
| Homework / assignments | Deferred | Deferred | Deferred | Deferred | Deferred | Deferred | NOT IMPLEMENTED; explicitly ON HOLD with reminder. |
| Audit logs | API | API | No global access | No | No | No | API exists; sidebar page is absent. |
| Roles, permissions, delegation, email settings | Backend APIs | Depends on expressly granted capabilities | Scoped delegation use, not admin UI | Explicit delegated capabilities only | No | No | Administration screens absent. Avoid a broad unrestricted permission editor as a workaround. |

## Additional gaps and release work

These are implementation gaps/recommendations, not all authorization vulnerabilities:

1. **Staff account lifecycle:** beyond create/list and self-profile editing, there is no full administrative staff edit/deactivate/reactivate/reset screen/API workflow. Securely revoking access is necessary before exposing account status actions.
2. **Custom/non-teaching staff roles:** Roles API and CreateStaff accept non-Student/non-Parent custom roles, but the sidebar, dashboard and Staff Leave page use fixed lists of the five built-in staff roles. An office-staff/librarian role will not automatically receive a usable dashboard/leave workflow. Decide supported staff categories and assign capabilities explicitly.
3. **Parents' timetable/special-class details:** parents receive relevant special-class messages but have no dedicated child weekly/daily timetable or special-class route. Add a view using verified parent-child relationships; never allow a free student-ID lookup.
4. **Older notifications:** staff inbox fetches the latest 100; parent notification API defaults to 50 and caps at 100. Pagination/date filters for historical alerts are not implemented. Leave history pagination already exists and should be retained.
5. **Background notification delivery:** server push interfaces and device-token endpoints are present, but no browser permission/device-token/service-worker messaging integration was found in the React source/public file inventory. In-app notification success must not be advertised as proven mobile/background delivery.
6. **Operational settings:** production frontend default API URL is still localhost unless overridden; production CORS includes a placeholder domain. Production seeding is not the development seeder. Document bootstrap/migrations, secrets, recovery and backups; perform an actual backup/restore rehearsal before deployment. Review the existing dependency/compiler warnings and bundle splitting as release work.
7. **Acceptance coverage:** most integration checks call controller methods directly. Add real authenticated HTTP authorization tests for each role and a browser acceptance checklist (allowed actions and denied foreign IDs), especially principals/deputies, parent-child boundaries, permission metadata, role changes and disabled accounts.
8. **Do not duplicate reports:** certificates/letters, if later required, can extend Reports; no separate document module is needed now.

## Recommended implementation order

1. **Access baseline:** agree/write the capability matrix; fix unscoped student reads and legacy create; enforce live account/token validity; add HTTP negative-access tests. Provision only the intended leader/head/lookup grants.
2. **Finish existing navigation:** general student attendance overview, Audit Logs, stale parent link, capability-aware student controls, explicit denied/not-found states.
3. **Complete existing administration:** wire student import/export; add staff lifecycle and safe role management with audit and session invalidation.
4. **International settings:** unify school timezone and date boundaries; then finish parent schedule views and notification history/background delivery.
5. **Acceptance/release:** execute all seven role checklists against staging, validate real email/push and backups, then address deployment configuration.
6. Keep staff attendance redesign, exam/test reconsideration, homework and promotion usability review deferred until the user resumes them.

## Next acceptance checklist

For every implemented feature and each role, check: permitted menu visibility; direct-URL behavior; successful authorized HTTP request; rejected wrong-role request; rejected foreign section/class/student/guardian ID; inactive account; changed role with old token; creation/update/cancellation history; notification recipient set with deduplication; and an understandable empty/error state. Use synthetic staging data for mutations and messages.
