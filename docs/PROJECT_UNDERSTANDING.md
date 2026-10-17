# Project understanding and development conventions

Reviewed from the current checkout on 9 October 2026.

## Scope and confidence

This document records a source-based understanding of the architecture, coding conventions, visual language, and request flows. A full-text structural scan covered 432 text files (including documentation, configuration, migrations, and the SQL snapshot), excluding dependencies, build outputs, Git internals, IDE caches, and the npm lockfile. The accompanying SOURCE_FILE_MAP.csv identifies those files and extracted declarations/routes. Closer reading focused on startup, authentication, authorization, domain entities, persistence configuration, API wrappers and guards, layout, representative pages, and important workflow branches.

This is not an exhaustive line-by-line correctness audit. Reading code establishes intended behavior, not successful runtime behavior. No new build, browser session, database query, migration, seeding, email, or push test was performed in this review. Existing checkpoint documents describe earlier verification and must not be interpreted as verification performed today. The visual assessment comes from JSX/CSS rather than rendered screenshots. Binary images were inventoried by filename rather than visually reviewed.

## Structure

```text
SchoolManagementSystem.slnx
src/
  SchoolManagement.Domain/          41 entities; workflow enums
  SchoolManagement.Application/     Feature DTOs; integration/service interfaces
  SchoolManagement.Infrastructure/  EF Core, Identity, permissions, integrations
  SchoolManagement.API/             47 controllers; middleware; reports; startup
school-management-web/
  src/api/                         Shared Axios client; feature API wrappers
  src/services/                    Staff login and localStorage handling
  src/context/                     Staff authentication context
  src/routes/                      Route tree and three portal guards
  src/components/layout/           Staff navigation and account menu
  src/pages/                       48 JSX files, including supporting components
tests/                             Unit, integration, and E2E project shells
docs/                              Reviews and workflow checkpoints
scripts/                           Read-only marks-review diagnostic
```

The backend targets .NET 10 and uses ASP.NET Core, SQL Server EF Core, ASP.NET Identity, and JWT. Frontend declarations use React 19, Vite 8, React Router 7, Tailwind 4, Axios, Lucide, and Oxlint. These are the versions declared locally, not claims about current external releases.

Project dependencies follow Domain <- Application <- Infrastructure, with API referencing Application and Infrastructure. The folder arrangement resembles Clean Architecture, but the implementation is controller-centered: controllers usually validate requests, resolve the acting account, enforce scope, query EF, update entities, and project responses directly. Application is predominantly contracts rather than an independent business-use-case layer. No general repository pattern, mediator pipeline, or frontend global server-state library is evident in the reviewed structure.

## Your coding pattern

C# uses descriptive PascalCase classes and methods, private readonly underscore-prefixed dependencies, constructor injection, async Task methods, LINQ, explicit early-return validation, and DTO request types. Models initialize strings and navigation properties explicitly. IsActive commonly represents deactivation; important actions also preserve histories, actor IDs, timestamps, and status enums. Database configuration is centralized in ApplicationDbContext with foreign keys, delete behavior, precision, unique indexes, and filtered indexes. There are 35 migration changes, paired with generated designer files and a model snapshot.

Read endpoints commonly use AsNoTracking and Select projections. Larger operations sometimes use explicit transactions within EF's retry execution strategy, as in student creation and import. Responses include anonymous JSON projections as well as dedicated report/dashboard DTOs. Authorization varies by feature: permission attributes, role attributes, and controller-level assignment/delegation checks all occur. A new endpoint must follow the specific feature's authorization rules rather than assume a role check is enough.

React uses JavaScript/JSX function components, useState/useEffect, controlled forms, local validation, async handlers, and explicit loading/error/success/empty states. Pages own much of their data loading and UI state. Feature API objects expose semantic methods and return Axios responses; pages generally consume response.data. Some pages call Axios directly. Student listing has debounced search, cascading academic filters, and server pagination; staff listing filters and paginates in the browser.

Many older files use large divider comments, uppercase section labels, extensive blank lines, and expressions split over several lines. Newer staff components use much denser formatting. Visual conventions are more consistent than formatting conventions. Match the surrounding file when changing existing code.

## Theme and visual style

The working theme is implemented primarily through Tailwind classes in JSX. index.css imports Tailwind; App.jsx does not import the starter App.css, so its Vite demo styles are not the application's main design system.

| Element | Recurring treatment |
|---|---|
| Page canvas | bg-slate-50, dark slate headings, muted slate supporting text |
| Cards | White backgrounds, slate-200 borders, rounded-xl/rounded-2xl, modest shadows |
| Main action | Blue-600 fill, white text, blue-700 hover |
| Secondary action | White fill, slate border/text, light hover background |
| Inputs | Rounded borders, blue focus border/ring, disabled state styling |
| Status | Green success/active, amber warning/pending, red errors, slate neutral |
| Icons | Lucide outline icons, commonly h-4/h-5 sizes |
| Layout | Responsive grids, stacked small-screen fields, max-width portal content |
| Staff shell | Slate-950 sidebar, blue active links, sticky white header |

DashboardLayout uses a 72-unit desktop sidebar or collapsed 20-unit sidebar, remembers collapse state in localStorage, renders mobile navigation with an overlay, filters links by role, and places nested staff pages through Outlet. Student and parent pages generally render their own portal headers and content rather than share that staff shell. The public landing page uses a dark hero with blue/indigo glow effects and portal selection. There is no centralized reusable component/token system for all repeated cards, buttons, fields, and portal shells.

## Request and authentication flow

```text
React page -> feature API wrapper/shared Axios -> /api controller
           -> request validation + authorization + account/assignment scope
           -> EF DbContext / Identity / infrastructure service
           -> SQL Server or external integration
           -> JSON response -> React state -> UI
```

main.jsx mounts StrictMode, BrowserRouter, and AuthProvider; App delegates to AppRoutes. Staff login uses authService and AuthContext. Student and parent login keep separate studentUser/parentUser metadata. All three share the accessToken storage key, so the browser's portal sessions share a token slot.

axios.js uses https://localhost:7063/api and adds Authorization: Bearer from localStorage. It has no shared response interceptor for expiry/401 handling. Staff authentication restores cached metadata and uses its existence as the authenticated state; the staff route guard also redirects temporary-password users. Student and parent guards inspect their cached portal role. Cached metadata is a UI convenience; backend JWT and permission checks determine access.

JwtTokenService issues HMAC-SHA256 tokens containing Identity user ID, full name, email, and roles. The API validates issuer, audience, signing key, and expiry with zero clock skew. Identity supplies password/account facilities. ForcePasswordChangeMiddleware looks up the user and blocks authenticated requests until the temporary password is changed, except for the password-change endpoint.

HasPermission maps a name into Permission:<name>; the dynamic policy provider creates a requirement; the handler checks active permissions assigned to JWT roles through RolePermissions. StaffPermissionDelegation is a separate mechanism consumed by feature-specific controller logic, including marks workflows, rather than automatically included by that shared handler.

Program.cs registers database retries, Identity, JWT, services, CORS, Swagger, rate limiting, upload limits, health checks, compression, timeouts, and security headers. Development startup invokes DevelopmentDataSeeder. Running startup solely for inspection can therefore write records. Production validates required configuration and uses HSTS; CORS still contains a placeholder domain.

## Data model and workflow relationships

Academic structure is Section -> Grade -> SchoolClass. AcademicYear contains terms, and terms contain exams. TeacherAssignment connects a staff member, subject, class, and academic year. ClassTeacherAssignment is distinct from subject teaching and controls class responsibility; temporary assignments support coverage.

Student stores a current class reference, while StudentAcademicEnrollment preserves academic-year/class history. StudentSubjectEnrollment tracks year-specific subjects, StudentPromotionHistory records lifecycle actions, and graduation fields distinguish completed students. ParentGuardian connects to students through StudentParentGuardian, which carries relationship, primary/emergency flags, and activation state. Staff, students, and guardians can link to Identity accounts through ApplicationUserId.

Marks entry uses the teacher assignment and exam to identify the roster and submission. Drafts are saved, then submitted. Section heads review within their active section/year assignment and cannot review their own teaching submission; global or delegated permissions are considered for other eligible reviewers. Approval and rejection record reviewer details. Publication marks the submission Published and sets associated marks IsPublished. Student result queries check publication state before exposing results. A Section Head role without the relevant assignment does not grant that section/year's review scope.

Attendance uses the permanent or active temporary class-teacher assignment. AttendanceWindowService computes the first timetable period for the selected class/date/active term; the normal direct-save window ends one hour after that start. A date/class/year extension can prolong access. Direct save is restricted to today's school date and the computed window. Outside it, attendance change requests preserve proposed and previous values and go through section-head review. School time comes from Attendance:SchoolTimeZoneId, falling back to Asia/Colombo; this is independent of the machine or chat timezone.

Enrollment endpoints cover initial enrollment, promotion, bulk promotion, overrides, graduation, subject assignment/removal, promotion preview, and batch confirmation. Scheduling covers regular timetable entries, teacher assignments, temporary coverage, special-class requests/reviews/cancellation/rescheduling, and unified regular/special schedules. Staff leave and announcements have their own review/audience rules.

Excel import separates reading, validation, reference resolution, persistence, and error workbook generation. Confirmation revalidates and resolves submitted rows again; it does not trust the earlier preview. Import persistence uses a transaction and retry strategy. ClosedXML also handles exports. ReportsController assembles report data and separate QuestPDF document classes format five report types. AnalyticsController calculates comparisons, summaries, and performance trends through EF queries.

ResendEmailService reads sender settings and sends OTP emails through Resend's HTTP API. FirebasePushNotificationService sends to registered device tokens and handles failures. Notifications also persist as application records with recipients and read status; push delivery and the inbox are separate parts of the system. API wwwroot has Firebase test/service-worker assets, but the React source has no corresponding production Firebase registration/messaging integration identified in this review. AuditLogService stores actor/request context and action details when called by workflows.

## Current boundaries to remember

- Staff management and section-head assignment pages are now registered; the older status review's claim that /staff is missing is stale.
- Sidebar destinations /attendance, /timetable, /announcements, /notifications, /reports, /audit-logs, and /parent/children still have no exact route in AppRoutes. The wildcard redirects to /dashboard.
- Student-list Import Students and Export Students buttons have no click handlers, despite backend import/export implementation.
- Frontend API base URL is hardcoded to localhost, and production CORS has a placeholder origin.
- The solution still represents the React directory as an old ASP.NET Website. Prior documents report a resulting solution-build failure; that build was not rerun here.
- Test folders contain project shells and one empty E2E test, rather than meaningful workflow coverage.
- Large controllers/pages combine many responsibilities: AnalyticsController is about 5,954 lines, StudentEnrollmentController 3,330, and AddStudentPage 2,471. Future changes require narrow feature tracing.
- studentAuthApi.login contains a second unreachable return after its first return.
- Sidebar visibility and frontend guards are not proof that every backend endpoint has correct authorization. This review did not establish complete endpoint security or production readiness.

## Conventions for future work

Extend the existing feature folders and semantic API wrappers; preserve established endpoint contracts. Use the local component's formatting, Tailwind palette, responsive behavior, and loading/error states. Resolve Identity accounts to the correct domain record and check active assignments, year, section, ownership, and state transitions for the workflow being changed. Keep student, subject-teacher, class-teacher, and section-head assignments distinct. Preserve enrollment/review history and published-result visibility rules. Use service interfaces for integrations and the established import/export separation. Do not assume a visible screen or available endpoint means the whole workflow has been runtime-verified.
