# Phase 1 verification report — 2026-10-19

## Decision and scope

This report records executed automated checks and their limits. Passing these checks is not a promise of zero defects or a claim that every possible form input and device was tested. Existing workflows were preserved; changes were limited to navigation/performance polish, accessibility labels, garbled text and verified defects.

## Executed suites

| Suite | Passed | Failed | Skipped | Total | Evidence |
|---|---:|---:|---:|---:|---|
| UnitTests | 11 | 0 | 0 | 11 | .local-tools/qa/results/UnitTests.trx |
| IntegrationTests | 66 | 0 | 0 | 66 | .local-tools/qa/results/IntegrationTests.trx |
| BrowserTests | 28 | 0 | 0 | 28 | .local-tools/qa/results/BrowserTests.trx |
| RestoreUiTests | 2 | 0 | 0 | 2 | .local-tools/qa/results/RestoreUiTests.trx |
| FileUploadUiTests | 2 | 0 | 0 | 2 | .local-tools/qa/results/FileUploadUiTests.trx |

**Automated suite status: PASS for the executed cases. Manual/device release checks below remain open.**

Additionally, the five Node browser-push tests passed. The production frontend build passed. Frontend lint completed with existing React-hook/style warnings, not a warning-free result. The NuGet transitive audit after the package repair reported no known vulnerable packages; this is dependency scanning, not a penetration test.
The saved npm audit also reported zero known vulnerabilities (info/low/moderate/high/critical all zero). Dependency audit files are in `.local-tools/qa/results/nuget-audit.json` and `npm-audit.json`.

## Browser, role and viewport matrix

- Actual installed Google Chrome, headless.
- Installed Chrome version: 155.0.8059.82. Playwright .NET: 1.62.0.
- Playwright WebKit on Windows. This tests Safari engine compatibility, not native Safari on macOS/iOS.
- Desktop: 1440 × 1000. Mobile emulation: 390 × 844, touch enabled, device scale factor 2. No physical phone was connected.
- Roles: Admin, Principal, Deputy Principal, Section Head, Teacher, Student, Parent and a custom staff role (Librarian). Separate contexts isolate accounts.
- Separate API and production frontend on temporary ports, using generated synthetic users and a disposable SQL database. The working SchoolManagement_Dev database was not used for test mutations or restore.
- Chrome checks HTTPS certificate trust without bypass. Windows WebKit uses a localhost-only certificate exception because its trust store differs; this is not a trusted-certificate check in native Safari.
- Tests respect the real sign-in rate limiter by pacing logins. Deliberate wait time is not application load latency.

| Engine / viewport | Page and tab checks | Failures | Evidence |
|---|---:|---:|---|
| chrome / desktop | 241 | 0 | .local-tools/qa/browser/navigation-chrome-False.json |
| chrome / mobile emulation | 241 | 0 | .local-tools/qa/browser/navigation-chrome-True.json |
| webkit / desktop | 241 | 0 | .local-tools/qa/browser/navigation-webkit-False.json |
| webkit / mobile emulation | 241 | 0 | .local-tools/qa/browser/navigation-webkit-True.json |

Each navigation check opens the route, waits for page/network completion, checks document overflow, records JavaScript exceptions and API responses ≥400, and rejects missing/access-restricted/blank pages where access is expected. Named navigation tabs are clicked and checked as well. Mobile menus are explicitly opened and closed. Example dashboards and failures are captured as PNGs.

### Role coverage

| Role | Recorded page/tab checks | Distinct URLs |
|---|---:|---:|
| Admin | 224 | 28 |
| Deputy Principal | 176 | 23 |
| Librarian | 52 | 5 |
| Parent | 52 | 10 |
| Principal | 176 | 23 |
| Section Head | 116 | 16 |
| Student | 68 | 14 |
| Teacher | 100 | 15 |

## Links, forms and buttons: what was checked

- Source inventory: 66 registered routes, 114 literal internal links, 0 unmatched links, 38 form-containing source files. Template/dynamic links are not proven correct by a literal-link scan; seeded student/parent detail routes are additionally exercised in the browser.
- Runtime route coverage: 65 / 65 non-wildcard route patterns represented by executed navigation/public/password-flow cases. The unknown-route fallback is checked separately.
- Public pages, unknown-route fallback, empty-login validation, password visibility controls, enabled-control pointer cursors, and secure-context status are exercised in both engines and viewports.
- Submitted browser workflows: school settings save/reload; announcement publish and student/parent audience separation; academic section creation/reload; manual holiday save; teacher leave application, Admin approval and staff notification; forced temporary-password change.
- Actual HTTP authentication/authorization checks verify Admin-only backups, leader-only settings options and publishing access for school leaders. Role controls are not checked only by hiding menu items.
- Database tests cover registration and rollback, class/subject assignments, scoped access, leave approval/rejection/cancellation/coverage, yearly allowance and working-day calculations, calendar import/deduplication, school events, assessments, promotion/repeat/graduation/undo, marks review/publication, reports/PDFs, notification ownership/recipients, school settings concurrency and real native backup/restore.
- Backup restoration is executed only in an isolated database and checks safety-backup retention, recovered state, changed security stamps, file tampering and schedule idempotence.
- A focused Chrome/WebKit browser suite additionally exercises backup creation/download, restore-dialog cancellation, a confirmed restore and successful sign-in with recovery of the earlier school name. Its evidence is reported separately when run outside the main browser batch.
- Focused mobile Chrome/WebKit cases download a government-calendar CSV template, upload a CSV, preview selected rows and import them; a direct read verifies the resulting holiday persisted in that isolated database.
- Browser-push worker tests check consent/account/portal separation and delayed messages after sign-out. They do not prove real OS notification delivery.

**Coverage limit:** this is not a claim that every button and every conditional form state was submitted through the UI. Destructive operations, OTP delivery and external-provider/device behaviour need the checks below. Form inventory is included so remaining cases can be tracked explicitly.

### Form inventory

| Source file | Form declarations |
|---|---:|
| school-management-web/src/components/TemporaryAttendanceTeacher.jsx | 1 |
| school-management-web/src/pages/academic/AcademicSetupPanel.jsx | 1 |
| school-management-web/src/pages/academic/GovernmentCalendarImport.jsx | 1 |
| school-management-web/src/pages/academic/SchoolCalendarPage.jsx | 3 |
| school-management-web/src/pages/academic/SchoolEvents.jsx | 2 |
| school-management-web/src/pages/announcements/AnnouncementsPage.jsx | 1 |
| school-management-web/src/pages/auth/LoginPage.jsx | 1 |
| school-management-web/src/pages/auth/StaffForcedChangePasswordPage.jsx | 1 |
| school-management-web/src/pages/auth/StaffForgotPasswordPage.jsx | 3 |
| school-management-web/src/pages/exams/ExamsTestsPage.jsx | 2 |
| school-management-web/src/pages/exams/TermsExamsManagementPage.jsx | 1 |
| school-management-web/src/pages/management/AttendanceOverviewPage.jsx | 1 |
| school-management-web/src/pages/management/AuditLogsPage.jsx | 1 |
| school-management-web/src/pages/management/BackupsPage.jsx | 2 |
| school-management-web/src/pages/management/SchoolSettingsPage.jsx | 1 |
| school-management-web/src/pages/parents/ParentChangePasswordPage.jsx | 1 |
| school-management-web/src/pages/parents/ParentLoginPage.jsx | 1 |
| school-management-web/src/pages/parents/ParentManagementActions.jsx | 2 |
| school-management-web/src/pages/parents/ParentProfilePage.jsx | 1 |
| school-management-web/src/pages/parents/ParentsManagementPage.jsx | 2 |
| school-management-web/src/pages/parents/ParentStudentLinks.jsx | 2 |
| school-management-web/src/pages/sectionHeads/SectionHeadAssignmentsPage.jsx | 1 |
| school-management-web/src/pages/specialClasses/SpecialClassesPage.jsx | 1 |
| school-management-web/src/pages/staff/AddStaffForm.jsx | 1 |
| school-management-web/src/pages/staff/StaffActions.jsx | 1 |
| school-management-web/src/pages/staff/StaffAttendancePage.jsx | 1 |
| school-management-web/src/pages/staff/StaffLeaveCancellation.jsx | 1 |
| school-management-web/src/pages/staff/StaffLeavePage.jsx | 1 |
| school-management-web/src/pages/staff/StaffLeavePolicy.jsx | 1 |
| school-management-web/src/pages/staff/StaffProfilePage.jsx | 1 |
| school-management-web/src/pages/students/StudentChangePasswordPage.jsx | 1 |
| school-management-web/src/pages/students/StudentForgotPasswordPage.jsx | 3 |
| school-management-web/src/pages/students/StudentLoginPage.jsx | 1 |
| school-management-web/src/pages/students/StudentProfilePage.jsx | 1 |
| school-management-web/src/pages/students/StudentRegistrationPage.jsx | 3 |
| school-management-web/src/pages/teachers/TeacherAssignmentsManagementPage.jsx | 1 |
| school-management-web/src/pages/teachers/TeacherAttendancePage.jsx | 1 |
| school-management-web/src/pages/timetable/TimetableManagementPage.jsx | 1 |

## Fixes made during verification

| Finding | Change | Verification |
|---|---|---|
| Enabled native controls had inconsistent cursors | Shared pointer cursor rule; disabled controls use not-allowed | Browser computed-style assertions |
| All portal pages loaded in the initial bundle | Lazy page imports with an accessible loading state | Production build and browser navigation |
| Dashboard contained garbled punctuation | Replaced the damaged separator/dash | Source scan and screenshots |
| Principal/Deputy Principal results page called a forbidden publishing API | Added missing Marks.Review and Marks.Publish grants to the baseline and additive migration | Role navigation, HTTP access tests, integration baseline assertions |
| Existing transitive cryptography package had known advisories | Pinned System.Security.Cryptography.Pkcs 9.0.21, the patched maintenance version | Build, regression tests and saved NuGet audit |
| Staff sign-in labels were not associated with their inputs; eye button had no accessible name | Added label associations and show/hide password label | Browser sign-in/visibility checks and forced-password workflow |
| Local frontend used HTTP | Added ignored localhost certificate export/HTTPS launcher; Vite detects the certificate | Trusted Chrome HTTPS checks |

The permission migration is additive and was applied to the development database. It changes grants, not student records. The tests run with separate build output so the running Debug API does not lock test assemblies.

## Performance evidence

The initial JavaScript entry decreased from approximately **938 KB to 300 KB** (about **68% smaller**, uncompressed). The current entry is approximately 94 KB gzip. Other pages now load when visited. This is an asset-size improvement; no claim is made about a measured percentage improvement on real mobile networks. Per-route local timings are in the browser JSON.

## HTTPS usage

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-web.ps1`, then open **https://localhost:5173**. Private certificate files remain in ignored `.local-tools/https/`. Browser security indicators depend on a valid trusted TLS certificate; no fake padlock was added to the page. Public deployment still needs its real domain, reverse-proxy/server HTTPS configuration and certificate.
The existing frontend was verified over HTTPS. Do not launch a second server on port 5173; stop the existing frontend first when using the launcher.
HTTP and HTTPS have separate browser storage. On the first HTTPS visit, sign in again and enable browser alerts there if desired. If notifications were previously enabled for the old HTTP site, remove that old site permission in browser settings to avoid stale/duplicate alerts. Existing HTTP sessions/consent cannot be copied across origins automatically.

## Required manual / environment-specific release checks

| Check | Why it remains separate |
|---|---|
| Native Safari on supported macOS/iPhone/iPad | Apple hardware/Safari was not available on this Windows host |
| Physical Android and iOS touch, keyboard, rotation and file-picker behaviour | Viewport/touch emulation does not reproduce every device interaction |
| Real Firebase OS/browser notifications for all recipient roles | Tests intentionally do not send messages to working school accounts/devices |
| Real email/OTP arrival and password-reset completion through the configured mail provider | External delivery and inbox/spam behaviour require a working test inbox/provider |
| Full destructive UI scenarios and every conditional form state | Automated evidence covers the named scenarios, not every possible action sequence |
| Production domain TLS, network speed, realistic large datasets and concurrent-user load | Local synthetic-data tests do not certify a deployment or capacity |

These limits prevent calling the entire production system mistake-free. Existing deferred workflow redesigns (staff attendance and exam/test flow) remain deferred.

## Repeat after future changes

Yes—more features and changes can be added, and the same regression suite can be rerun. Add cases for new behaviour rather than relying on old coverage. Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-phase1.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\write-phase1-report.ps1
```

See `tests/README.md` for setup, evidence paths and focused runs. Review failed cases before release; a report generated from failing results must not be treated as a pass.

## Executed xUnit test cases

| Suite | Case | Outcome |
|---|---|---|
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.InvalidRowsAreMarkedWithoutImporting(from: "2025-12-31", through: "2026-01-02", name: "Cross year") | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.UnsupportedUploadsAreRejected | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.InvalidRowsAreMarkedWithoutImporting(from: "2026-02-30", through: "2026-03-01", name: "Invalid date") | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.SingleHolidayAndQuotedNamesAreRead | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.LeapDayIsSupported | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.InvalidRowsAreMarkedWithoutImporting(from: "2026-01-01", through: "", name: "") | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.InvalidRowsAreMarkedWithoutImporting(from: "2026-03-02", through: "2026-03-01", name: "Reversed range") | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.MissingHeadersAreRejected | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.EmptyFileIsRejected | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.MonthLongBreakIncludesBothEndpoints | Passed |
| UnitTests | SchoolManagement.UnitTests.CalendarImportTests.PromotionUndoPreservesGraduationAndEnrollmentState | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SpecialClassNotifiesLeadershipSectionTeacherAndFamilies(creatorRole: "Admin") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.LeaveCancellationPreservesEarlierDaysAndRevokesOnlyAffectedCoverAfterApproval | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TargetedStudentAnnouncementDoesNotReachOtherStudents | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TimetablePeriodsSupportAdjacentTimesUpdateAndDeactivation | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.LeaveAllowancesExcludeConfiguredWeekendsAndWeekdayHolidaysAcrossYears | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.AutomaticPromotionHonorsDatesRepeatsGraduationCorrectionsAndReruns | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.BrowserPushOwnershipMovesWithinPortalAndCannotBeDisabledOrTestedByAnotherAccount | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TimetableDrivesTeacherFeedAndFirstPeriodAttendanceWindow | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.GovernmentCalendarReaderValidatesCsvAndExcelWithoutEvaluatingFormulas | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TargetedAnnouncementsAreVisibleOnlyToSelectedStaff(audience: "SpecificSectionHeads") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.StaffLifecycleProtectsSelfAndLeadershipAndInvalidatesChangedAccounts | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.StudentExportMatchesSearchAndActiveFilters | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SectionHeadAssignmentsEditRemoveReactivateAndRejectDuplicates | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SubjectTeacherGetsAttendanceAccessOnlyAfterClassTeacherAssignment | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.StaffNotificationsArePersonalAndReadActionsCannotAffectOthers | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SubjectAssignmentRejectsInactiveYearsAndNonTeacherOrInactiveAccounts | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.PublishingRequiresApprovalAndScopedAccess(actor: "unrelated") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SchoolSettingsPersistValidateAndRejectStaleEditsAndTeachers | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SchoolEventsScopeRecipientsNotifyChangesAndRecalculateClosures | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TimetableRejectsClassAndTeacherOverlapsAndUnassignedTeachers | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.DatabaseBackupRestoresOnlyItsIsolatedDatabaseAndKeepsSafetyHistory | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SpecialClassNotifiesLeadershipSectionTeacherAndFamilies(creatorRole: "Teacher") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.LeadershipPermissionBaselineIsIdempotentAndInactiveAccountsCannotUseGrants | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.PublishingRequiresApprovalAndScopedAccess(actor: "subject") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.StudentRegistrationTests.PartialRegistrationRequiresExplicitMatchingCompletion | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.StudentImportTemplateRoundTripsAndRejectsFormulasAndOversizedRowRanges | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.PeopleReportsIncludeAssignmentsAndParentsAndRejectSectionHeads | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.HttpAuthorizationEnforcesRolePermissionsAndRevokedSessions | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SpecialClassNotifiesLeadershipSectionTeacherAndFamilies(creatorRole: "Section Head") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.RecipientLookupReturnsNumbersAndParentFeedIsPrivate | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.StudentRegistrationTests.RegistrationSavesAllPartsAndRejectsARepeat | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.AllStaffCanRequestLeaveAndReviewNotificationsReachOnlyTheCorrectStaff | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.LeaveCoverChecksScopeConflictsNotificationsRevocationAndDateExpiry | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.CommonIntervalsReserveEveryClassSupportEditingAndDoNotCountAsLessons | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.StudentRegistrationTests.FailureAfterSavingRollsBackAllPartsAndAllowsRetry | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.DisabledSubjectAssignmentIsReactivatedWithoutDuplicateRecords | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.StudentScopeRequiresAssignedClassOrSubjectAndAdmissionRequiresLeadership | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SectionHeadTemporaryTeacherCanAttendOnlyAssignedClassUntilExpiry | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TargetedAnnouncementsAreVisibleOnlyToSelectedStaff(audience: "SpecificStaff") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SectionHeadClassTeacherAccessIsScopedToSectionAndYear | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.DevelopmentTeacherCanReadExamsButCannotManageThemAndSeedingIsIdempotent | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.AssessmentsRespectAssignmentsNotifyBothDirectionsAndProtectOfficialExams | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.LiveSessionRejectsRevokedStampRoleAndInactiveAccountButAllowsPublicLogin | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.TimetableIndependentClassesRespectDailyLimitsAndReleaseRemovedSlots | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.ControllerDiscoveryTests.ApiControllersCanBeDiscoveredWithoutExposingReportFilter | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.StudentRegistrationTests.DuplicateParentDoesNotCreateStudentOrSubjects | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.BrowserPushSeparatesParentStudentStaffConsentAndRejectsWrongPortalsAndInactiveProfiles | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.HolidayImportIsAdminOnlyDeduplicatesAndPreservesExistingSchoolDates | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.ClassTeacherReplacementRequiresCurrentAssignmentAndPreservesHistory | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.PublishingRequiresApprovalAndScopedAccess(actor: "class") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.ScopedAnnouncementCreatorsCannotReachUnassignedRecipients | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.AttendancePermissionReportsMissingHeadAndRemainsPendingWithoutChangingAttendance | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.BrowserConfigExposesOnlyPublicWebSettingsAndRequiresMatchingProject | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.ParentScheduleAndNotificationHistoryCannotReadOtherRecipients | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.ReportsIncludeLateAttendanceAndExportStudentPdf | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.AttendanceFirstEntryCorrectionsCutoffAndTeacherScopedPermission | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.MailpitEmailTests.DevelopmentEmailRejectsNonLocalSmtpHost | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.BrowserMessageCannotLeakSchoolDetailsOrTriggerAutomaticFirebaseNotifications | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SpecialClassRequestReviewConflictRescheduleAndCancel | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.StaffAttendanceShowsApprovedLeaveFlagsConflictsAndRetainsCorrections | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.MarksReviewLoadsWithoutFirebaseCredentials | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.GovernmentCalendarUploadPreviewsBeforeImportAndAppliesClosuresToSharedCalendar | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.SpecialClassNotifiesLeadershipSectionTeacherAndFamilies(creatorRole: "Principal") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.PromotionMistakesCanBeUndoneWithoutLosingHistoryOrAffectingOtherStudents | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.TeacherAssignmentTests.PublishingRequiresApprovalAndScopedAccess(actor: "head") | Passed |
| IntegrationTests | SchoolManagement.IntegrationTests.MailpitEmailTests.DevelopmentOtpUsesStudentRecipientAndEscapesHtml | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1AccessTests.HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(role: "Librarian") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.PublicLinksLoginValidationCursorAndHttps(engine: "chrome", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AcademicCalendarAndLeaveFormsSaveThroughTheBrowser(engine: "chrome", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1AccessTests.HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(role: "Teacher") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.SchoolSettingsAndAnnouncementFormsPersistAndTargetRecipients(engine: "webkit", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.TemporaryPasswordMustBeChangedBeforeEnteringPortal(engine: "chrome") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.DashboardCardsAndHeaderBellActuallyNavigate(engine: "webkit", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.PublicLinksLoginValidationCursorAndHttps(engine: "chrome", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AllRoleNavigationAndMobileLayout(engine: "webkit", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AcademicCalendarAndLeaveFormsSaveThroughTheBrowser(engine: "webkit", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.PublicLinksLoginValidationCursorAndHttps(engine: "webkit", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.DashboardCardsAndHeaderBellActuallyNavigate(engine: "webkit", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1AccessTests.HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(role: "Deputy Principal") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AllRoleNavigationAndMobileLayout(engine: "chrome", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.PublicLinksLoginValidationCursorAndHttps(engine: "webkit", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AllRoleNavigationAndMobileLayout(engine: "chrome", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.SchoolSettingsAndAnnouncementFormsPersistAndTargetRecipients(engine: "chrome", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.SchoolSettingsAndAnnouncementFormsPersistAndTargetRecipients(engine: "chrome", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AllRoleNavigationAndMobileLayout(engine: "webkit", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.DashboardCardsAndHeaderBellActuallyNavigate(engine: "chrome", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1AccessTests.HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(role: "Principal") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AcademicCalendarAndLeaveFormsSaveThroughTheBrowser(engine: "chrome", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1AccessTests.HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(role: "Section Head") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1AccessTests.HttpPipelineEnforcesBackupAndSettingsAndPublishingRoles(role: "Admin") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.DashboardCardsAndHeaderBellActuallyNavigate(engine: "chrome", mobile: False) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.AcademicCalendarAndLeaveFormsSaveThroughTheBrowser(engine: "webkit", mobile: True) | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.TemporaryPasswordMustBeChangedBeforeEnteringPortal(engine: "webkit") | Passed |
| BrowserTests | SchoolManagement.E2ETests.Phase1BrowserTests.SchoolSettingsAndAnnouncementFormsPersistAndTargetRecipients(engine: "webkit", mobile: False) | Passed |
| RestoreUiTests | SchoolManagement.E2ETests.BackupRestoreBrowserTests.BackupDownloadCancelAndConfirmedRestoreWorkInAnIsolatedSchool(engine: "webkit") | Passed |
| RestoreUiTests | SchoolManagement.E2ETests.BackupRestoreBrowserTests.BackupDownloadCancelAndConfirmedRestoreWorkInAnIsolatedSchool(engine: "chrome") | Passed |
| FileUploadUiTests | SchoolManagement.E2ETests.GovernmentFileImportBrowserTests.GovernmentCsvTemplateDownloadPreviewAndImportPersist(engine: "chrome") | Passed |
| FileUploadUiTests | SchoolManagement.E2ETests.GovernmentFileImportBrowserTests.GovernmentCsvTemplateDownloadPreviewAndImportPersist(engine: "webkit") | Passed |

## Technical references

- [Playwright browser and WebKit scope](https://playwright.dev/dotnet/docs/browsers)
- [Microsoft cryptography package advisory and patched versions](https://github.com/advisories/GHSA-846f-f764-76mx)
