# Staff leave

Open **Staff Leave** in the staff sidebar. All existing staff roles can apply:
Admin, Principal, Deputy Principal, Section Head and Teacher. The account must be
linked to an active staff record and an active user account.

## Apply and track

Use **Request leave**, select the type and dates, and provide a reason. Dates are
inclusive, including single-day leave. A pending or approved overlapping request
for the same staff member is rejected. Reasons and review remarks support up to
1,000 characters. **My requests** shows the result and reviewer remarks.

## Review

Admin, Principal and Deputy Principal use **Pending approvals** to approve or
reject, with a confirmation step. They can request their own leave, but another
school leader must review it. Section Heads see pending requests from teachers
assigned to their section during the leave's academic year; this view does not
grant approval permission. Use Refresh to update the list.

## Leave history

School leaders and Section Heads have a **Leave history** tab. Leaders see the
whole school; Section Heads see staff linked to their assigned sections and
academic years, including inactive historical teaching assignments. Teachers can
still view their own requests through My requests but cannot access other staff's
history. History is read-only and does not grant approval rights.

Filter by staff name/number, status, and an overlapping leave date range. Results
include the staff designation, leave type, inclusive calendar days, reason,
request/review dates, reviewer, remarks and cancellation date when present.
Counts reflect the selected filters; pages contain up to 25 records in the UI.
Approved periods are labelled Scheduled, On approved leave or Approved dates ended
based on Saudi school dates. These are approved periods, not proof of actual
attendance. Existing Pending, Approved, Rejected and Cancelled records are included.

## Notifications

| Event | In-app recipients |
| --- | --- |
| Request submitted | Active Admin, Principal and Deputy Principal accounts, excluding the requester |
| Request approved | The requesting staff member, active school leaders, and relevant active Section Heads |
| Request rejected | The requesting staff member, including review remarks |

Relevant Section Heads are derived from subject/class teacher assignments or the
requester's Section Head assignments in academic years overlapping the leave.
Approval of an Admin, Principal or Deputy Principal's leave notifies all active
Section Heads with assignments during those dates, since leadership absence
affects the whole school. Recipients are deduplicated across roles.

Alerts are saved in the same transaction as the approval/rejection. Optional push
delivery runs afterward; push failures are logged and do not lose the decision or
the in-app notification. Approval/rejection locks prevent repeated or concurrent
reviews from generating duplicate decision alerts. Notification **View details**
opens Staff Leave; submitted-request alerts open Pending approvals.

No schema migration is needed: this uses the existing leave and notification
tables. `NotificationType.LeaveRequested` is a new stored enum value (13).

## Covering approved leave

Admin, Principal, Deputy Principal and Section Head use **Arrange cover** inside
Staff Leave. Pick a school date; the page lists approved absences with affected
class-attendance duties, regular timetable periods and approved special classes.
Section Heads see only classes in their assigned section and academic year.
Choose a replacement teacher separately for each duty on that date. Multiple
leave days are arranged date by date. Duties without a schedule on the selected
date are not invented or copied from another weekday.

The replacement must have an active user, active staff record and Teacher role.
Assignments reject the original absent teacher, teachers on approved leave,
duplicate replacements and overlapping regular/special/cover lessons. Attendance
cover rejects another class-teacher duty and lessons in a different class during
the attendance window. Covering a lesson and attendance for the same class is
allowed. Availability is checked when the assignment is saved; after timetable
changes, review and reassign affected duties as needed.

The teacher receives an in-app notification, with optional push, and sees duties
under **My cover duties** and **Today & Next Class**. Teaching duties do not grant
marks access or change permanent teacher assignments. Class-attendance duties
appear in Class Attendance only on the assigned school date, using the configured
attendance school timezone. Permission checks also enforce that same date and
continue to apply the normal first-entry window and correction approvals.
Future duties grant no early attendance access; access ends automatically after
the assigned date, if removed, if the source leave is no longer approved, or if
the replacement is put on approved leave. Removal notifies the replacement.

The earlier two-hour temporary-attendance tool remains available for unrelated
short-term access. Leave cover uses a separate audited table and does not extend
that two-hour permission.

Schema migration `20261018183336_LeaveTeacherCoverage` adds `LeaveTeacherCoverages`
with indexes and foreign keys. Apply it before using the coverage screens in
another environment. It does not change existing leave or student records.

In Arrange cover, **Change teacher** replaces an active assignment atomically:
availability and scope are checked before the original is revoked. Failed changes
leave the current replacement in place. The old and new teachers are notified;
relevant section heads are informed of changes and removals (excluding the acting
head and duplicate recipients). Leader-created assignments also notify section
heads. **Remove** ends access without deleting the assignment. Ended duties cannot
be edited or removed.

Coverage history below the arrangement form retains assignments, reassignments,
removals and ended duties, with actor names and timestamps. Leaders see all;
section heads see their assigned section and academic year only. Search matches
replacement/absent teacher names and staff numbers; date filters use cover dates.
Older removals without a recorded actor display “Not recorded”. Migration
`LeaveCoverageHistory` adds nullable removal-actor and replacement-link columns;
no existing records are deleted.

## Withdrawal and approved-leave cancellation

All staff use **Staff Leave → Withdraw / cancel leave** to withdraw their own
pending request immediately or request cancellation of approved leave. A return
date must be today or later within the approved period. Until another Admin,
Principal or Deputy Principal approves it, leave and coverage remain unchanged.
No leader can review their own cancellation. Rejection preserves both leave and
coverage and permits a new request. If the requested return date has passed, the
reviewer must reject it and the staff member must select a new date.

Full cancellation marks the leave Cancelled. Early return shortens its end date
to the day before return, preserving earlier approved days. The original end
date, reason, decisions, reviewer and timestamps remain in cancellation history.
Coverage on or after the return date is revoked in the same transaction; earlier
coverage remains. Teachers losing coverage receive alerts, and the requester,
school leaders and relevant section heads receive approval updates. Rejected
cancellations notify the requester. Pending withdrawal informs school leaders.

Cancellation history is visible to the owner, school leaders and section heads
within their assigned scope. History defaults to requests made during the last
six calendar months through today (Saudi local dates), with 25 requests per page.
Requested-date filters query older records directly from the database; no records
are deleted. Staff number/name search and status filters apply before pagination.
Migration
`StaffLeaveCancellation` adds the history table without deleting existing leave
or coverage data.

## Staff attendance and approved leave

Open **Staff Attendance** from the sidebar. Admin, Principal and Deputy Principal
can record Present, Late or Absent for today or earlier dates. Section heads have
read-only access to their assigned section/year and themselves. Search matches
staff number or name. Student attendance and its permissions remain separate.

Approved leave covering the selected date automatically appears as **On approved
leave**, unless a Present/Late entry conflicts with it. Unrecorded staff without
approved leave remain **Not marked**. Pending/rejected/cancelled leave does not
count as approved absence. Cancellation and early-return date changes are reflected
on the next refresh. Approval after an existing Present/Late entry flags it for
review automatically.

Saving Present/Late during approved leave requires a school leader to confirm
the conflict and record a reason. This records actual attendance without silently
cancelling leave; use the cancellation workflow to correct leave dates. Corrections
require a reason, reject stale edits, and append an audit record instead of
overwriting previous entries. Each staff/date has a History action showing status,
reason, recorder, time and leave-conflict confirmation. Daily totals separate
Present, Late, Absent, approved leave, unmarked and conflicts requiring review.
The dashboard absence card links to Staff Attendance and shows the leave/conflict
counts separately. **Not marked** is never automatically counted as absent.

Migration `StaffAttendanceRecords` adds only the new append-only table and indexes;
it does not create historical attendance records or change existing leave.

## Leave allowances and country-specific school calendars

Admin uses the separate **School Calendar** sidebar page. For each calendar year,
save the school's two-letter country code, optional region and weekend weekdays.
Country/region describe the school calendar; they do not silently select holidays
or enforce government leave laws. Admin enters the approved allowances and holiday
dates. No government entitlement values or public-holiday dates are preloaded.
If a year has no saved calendar, Saturday/Sunday are excluded with a visible warning.

Admin uses the separate **Staff Leave Allowance** sidebar page to save one shared
yearly total for all leave types, with an optional individual staff override. For
example 40 days minus 5 Sick and 3 Casual leaves 32 remaining. Leave types are only
usage categories, not separate budgets. The individual total replaces the default.
Old per-type allowance rows are retained but no longer applied; Admin must enter
the shared total explicitly, since adding old limits could inflate entitlements.
Zero is valid; missing configuration is shown as Not set. Allowances use whole
days and calendar years without automatic carry-over. Pending days appear separately;
Remaining is shared Allowance minus approved days across all types. Excess requests
show warnings without automatically blocking a school leader's approval decision.

Add public/school holidays by date and name, including holidays on working weekdays.
Saving the same date updates it; removing a holiday deactivates it in the database.
Weekends and holidays are excluded once per date, including a holiday falling on a
weekend. Date spans across New Year use each year's calendar and allowance. Holiday
edits recalculate balances, including existing approved leave. Approved cancellations
and early-return changes restore the affected counted days automatically.

**Leave balances** shows each staff member their own totals; school leaders can
select another staff member. Request forms preview counted days and excess-balance
warnings; pending review cards show the same calculation excluding the current
request from the baseline. Leave history displays both counted leave days and the
full calendar-day span. The existing leave date span is retained for coverage and
attendance checks.

Public holidays can be loaded through Calendarific after a private API key is
configured on the API server. Admin uses **School Calendar → Load public holidays**,
reviews the returned nationwide dates, then imports selected dates. Calendarific
supports Sri Lanka (LK). Existing active dates, school breaks and manually removed
dates are preserved; repeated imports do not duplicate dates. Nationwide public/
national/federal holidays are included; observance-only, bank-only and regional-only
entries are excluded. Add regional dates, school closures and newly announced
changes manually. Provider data is cached for 12 hours by country/year.

Development setup (replace the placeholder locally; do not put the key in React
or source control):

```powershell
dotnet user-secrets set "PublicHolidays:CalendarificApiKey" "YOUR_API_KEY" --project "D:\School_Management_System\src\SchoolManagement.API\SchoolManagement.API.csproj"
```

Restart the API after configuring the key. Production can use the server secret
environment variable `PublicHolidays__CalendarificApiKey`. Request logging is
disabled for this HTTP client because the provider authenticates via a URL query
parameter. Provider errors return sanitized messages and do not modify the calendar.
Imports use a transaction and validate the selected dates against the provider list.
No schema migration is needed for the import feature. Provider fixture tests cover
permission checks, duplicate dates, preserved manual changes, quota errors and caching;
a live provider fetch requires a real key.

Migration `StaffLeaveCalendarAndAllowances` adds calendar, holiday and allowance
tables without changing existing leave requests or automatically applying a policy.

**School Calendar** displays a selectable month grid with previous/next navigation,
holiday names on dates, yearly/monthly planned school-day totals and a holiday list.
Active staff can view it; Admin alone edits settings and holiday dates/ranges. Admin
sets opening/closing dates within the selected January–December year, weekly holidays,
and named public/school closures including term-break ranges. Dates outside that
opening period, holidays and weekends do not count toward school days or staff leave.
Holiday/weekend overlaps count once. Totals are planned days, not actual attendance.
For a school year spanning December, configure each calendar year's portion separately.
Migration `SchoolCalendarOpeningDates` adds nullable opening/closing columns; existing
calendars retain full-year dates until Admin specifies them.

## Government / school calendar file upload

Admin uses **School Calendar → Upload government / school calendar**. Download the
Excel or CSV template, enter approved holiday/closure dates, upload, preview and
select valid rows, then import. The first worksheet/header row must contain `From`
(aliases Date, StartDate, FromDate), optional `Through` (To, ToDate, EndDate), and
`Name` (Holiday, HolidayName, Description). Dates must be YYYY-MM-DD text or real
Excel date cells; ambiguous numeric date strings are rejected. A blank Through
means one date. All imported holidays apply to **both staff and students**, including
school vacation ranges; they reduce planned school days and staff leave charges.
Rows must describe holidays/closures, not government term working-date ranges.
The upload does not replace the school's opening/closing dates or weekend settings.

Supported formats are UTF-8 CSV and unencrypted .xlsx, up to 2 MB, 1,000 data rows
and 32 columns. The first Excel worksheet is read; formulas are rejected. Templates
have headers/instructions only, with no fabricated holiday examples. PDF files are
not extracted automatically: transfer their approved dates into the template first.
No provider API key is required for file upload.

Preview saves nothing to the database. Invalid rows show errors and cannot be
selected; valid rows may be imported separately. A preview belongs to the uploading
Admin and expires after 20 minutes. Imports reject unknown/invalid row selections
and country changes after preview. Overlapping ranges become unique holiday dates;
names on newly imported overlapping dates are combined. Existing dates (including
manually deactivated holidays) are preserved. Import is transactional, and consumed
previews cannot be reused. Uploaded files are not saved to disk; validated preview
rows are held temporarily in memory. No database migration is required.

Manual checks: submit as a teacher, approve as a leader, verify the teacher,
relevant Section Heads and leaders receive the alert, then reject a separate
request and verify the requester sees the rejection and remarks. Also submit as
Admin, Principal and Section Head, and verify leaders cannot review their own
requests.

### Simplified calendar navigation

The School Calendar opens on Calendar, with the month view, holiday list and
school-day totals. Admin has Add holidays and Calendar settings tabs. Add holidays
shows one selected method: manual entry, national public-holiday loading, or
previewed government Excel/CSV upload. Calendar settings contains country,
opening/closing dates and weekends. Clicking a date opens an Add/Edit holiday
popup for Admin; saving returns to the relevant calendar month. Other staff see
date details without editing controls. Existing calendar/import rules are unchanged.
