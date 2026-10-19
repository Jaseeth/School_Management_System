# Exams and tests

Open **Exams & Tests** from the staff sidebar.

## Government Exam Timetable

Exam papers and the official subject timetable come from the government. This system records the supplied timetable; it does not author term-exam papers.

1. Leaders open **Step 1: Record the government exam period**, select the academic year and term, then register the government's exam name, start date, end date and maximum marks. Academic terms remain ordinary school setup metadata.
2. **Add government timetable entry** records each class, subject, date and start/end time exactly as provided. Dates must be inside the announced government exam period, which must itself be inside the academic term. School leaders can record/correct these entries; teachers and section heads cannot create government exam schedules.
3. Publish the entry to notify its recipients and show it on their calendars. Existing clash and notification rules apply.

**Update government dates** corrects the recorded period for an existing exam. A period cannot be shortened around active timetable entries; first correct/cancel those entries. Updating period metadata alone does not move existing subject slots. Existing exam records without an end date initially use their stored exam date for both ends; use Update government dates to enter the actual announced range.

Existing marks approval, publishing and result reports continue to use the official Exam record. A schedule changes the timetable and notifications, not existing marks. Cancelling a timetable entry does not delete the government exam record or previously entered results.

## Internal Tests

Leaders can create tests for any active class. Section heads can create/manage tests only for classes in their assigned section and academic year. A class teacher can create whole-class tests or a subject test in their assigned class. A subject teacher can create tests only for their assigned subject/class/year. Teachers may edit/cancel their own tests while they retain the assignment; they cannot edit another teacher's test. Section heads may manage tests within their section; leaders may manage all schedules. Only leaders manage official exams.

Tests store their own title, instructions and maximum marks. They do not create an official Exam or StudentMark, and are not included in term totals. This feature schedules and communicates tests; it does not introduce a separate classroom-test mark-entry/result module.

## Audience and notifications

Students come from active current academic enrollments in the selected class/year. Subject tests additionally require active subject enrollment. Parents/guardians are selected from active links to those students. Staff recipients include the class teacher, the relevant subject teacher(s), the relevant section head(s), and the creator. Whole-class tests include all subject teachers for the class. Recipients are saved on publishing/editing; editing refreshes the audience.

- Section head publishes: class/subject teachers, students and parents are notified.
- Teacher publishes: section heads, other relevant class/subject teachers, students and parents are notified.
- Leader publishes: relevant section heads, teachers, students and parents are notified.

The acting staff member is excluded from their own alert. Staff holding multiple relevant assignments receive one notification; a guardian linked to multiple included students receives one notification. Updates and cancellations notify recipients. Removed recipients receive an alert that the assessment no longer applies. In-app alerts are committed in the same transaction as the schedule; optional push failure does not roll back the schedule. Notification links open **School Calendar → Exams & Tests**.

## Calendar, clashes and corrections

Staff, students and parents see only relevant schedules in their calendar, with date, local start/end times, class, subject, location, instructions and maximum marks. Leaders can see all schedules; current section heads can see their section's schedules. Calendar year filters the dates displayed; academic year controls assignment and term validation during creation.

A class cannot have overlapping exams/tests across either category, regardless of subject. Adjacent times are allowed. Saving uses a database transaction lock to prevent simultaneous conflicting publications. Updates use revision checks so stale edits cannot overwrite newer decisions. Cancelled schedules retain their reason/history and stop blocking the slot. Exams/tests do not make a day a school holiday.

Migration **20261019103407_GovernmentExamPeriod** adds a nullable exam end date without modifying existing marks. Migration **20261019101610_AssessmentSchedules** adds a table and foreign keys only. No existing marks, official exams, or enrollments are removed. Integration tests cover assignment boundaries, official-exam restrictions, both notification directions, deduplication, calendar visibility, cancellation, revisions, and cross-category clashes. Browser checks use sample data without messaging live recipients.
