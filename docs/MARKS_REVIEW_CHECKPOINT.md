# Marks Review checkpoint — 7 October 2026

## Confirmed cause

Read-only queries against the configured development database found:

| Record | Actual value |
|---|---|
| sectionhead@school.com | Active staff SH001, Secondary Section Head, staff ID 3, linked to its Identity account and Section Head role |
| Existing assignment | Active Secondary assignment for 2026/2027; assignment ID 1 |
| Target submission | ID 8, first exam, First Term, Mathematics, Grade 11 A, teacher T001 |
| Submission status | Submitted (enum value 2); approval/publication not yet performed |
| Submission academic year | 2030/2031, year ID 5 |
| Submission section | Secondary, section ID 1 |
| Matching reviewer assignment | None |

These IDs were obtained from the database, not assumed. The pending query correctly excludes submission 8 because SH001 has no active Secondary assignment for 2030/2031. The older published Parent Result Test submission is a different exam.

Account resolution: the JWT NameIdentifier resolves to Staff.ApplicationUserId, with Staff.IsActive required. Submission scope resolves through TeacherAssignment.AcademicYearId and TeacherAssignment.SchoolClass.Grade.SectionId. Pending also requires Submitted status and excludes the reviewer's own teaching submissions. Details/approval checks enforce the same section/year assignment, so changing the frontend query would not resolve authorization correctly.

## Implementation

- `school-management-web/src/api/sectionHeadAssignmentsApi.js`: reuses GET /Staff, GET /section-head-assignments and POST /section-head-assignments.
- `school-management-web/src/pages/sectionHeads/SectionHeadAssignmentsPage.jsx`: Admin screen with staff, section, and year selectors; existing assignments/status; filtering; create/reactivate; duplicate prevention; loading/error/success states; responsive cards and forms; cursor-pointer controls. Uses existing academic lookup APIs. The staff dropdown shows active linked accounts and their emails so the intended account can be selected; account roles are not changed by this screen.
- `school-management-web/src/routes/AppRoutes.jsx`: staff-layout route /section-head-assignments.
- `school-management-web/src/components/layout/DashboardLayout.jsx`: Admin sidebar entry.
- `school-management-web/src/pages/sectionHeads/SectionHeadMarksReviewPage.jsx`: empty-state guidance explaining section/year scope.
- `scripts/Test-MarksReviewCheckpoint.ps1`: repeatable read-only diagnostic, loading configuration without printing credentials. It performs SELECT statements only and never starts the API, seeds data, applies migrations, or updates records.

No backend business logic, authorization rule, database record, or migration was changed. Existing local feature edits were preserved.

## Confirmation test — stop after this checkpoint

Use the running frontend at http://localhost:5173 and API at https://localhost:7063. Browser automation could not reach the local frontend in this session, so role login, assignment creation, mobile rendering, and pending-list behavior must be confirmed manually.

1. Sign in through the staff login as Admin.
2. Open **Section Head Assignments** in the sidebar, or visit http://localhost:5173/section-head-assignments.
3. Select **SH001 — Secondary Section Head (sectionhead@school.com)** and **Secondary**. Leave the year blank initially: the existing list should show the active **2026/2027** assignment.
4. Select **2030/2031**. Before saving, the list should have no matching assignment and the summary should say no existing assignment. Verify all three selections.
5. Click **Assign Section Head**. This uses the existing backend to create the additional active assignment for SH001, Secondary, 2030/2031. The 2026/2027 assignment remains active. Expected: success message and an Active 2030/2031 assignment card; the save button becomes Already assigned and is disabled.
6. Click Refresh. Expected: the new assignment remains Active. Repeated saves must not create another assignment.
7. Sign out, then sign in through staff login as **sectionhead@school.com**.
8. Open **Marks Review** and click **Refresh**. Expected: **first exam — Mathematics**, year **2030/2031**, Secondary, Grade 11 A, teacher T001 appears in Pending submissions.
9. Open that submission using its existing review button. Expected: the submitted student marks are visible, including **ST002 / Test Student Two / 56 of 100**. The checkpoint supplied that mark; it was not separately queried during this assignment diagnosis. Do not use ST0001, who is in Grade 12 A.
10. At a mobile browser width (for example 390 px), verify the assignment fields stack, controls remain usable, and assignment cards do not overflow. As Section Head, directly opening /section-head-assignments should show the Admin-only message and should not load assignment-management data.

Please confirm the assignment and pending submission visibility before approval/publication. After confirmation, the next checkpoint is Section Head approval; then Admin publication, followed by ST002 and the verified linked parent's published-results checks. Do not approve or publish a different exam to substitute for this submission.

## Verification

API build passed with zero warnings/errors. Frontend production build passed with the existing large-bundle warning. Lint passes with pre-existing warnings; the new assignment page/API wrapper are checked separately. These checks establish compilation/static validity, not a successful role-based browser workflow.
