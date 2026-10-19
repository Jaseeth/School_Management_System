# Automatic annual student promotion

School managers (Admin, Principal and Deputy Principal) configure this under
**Student Promotion** in the sidebar. Academic-year dates, grades and classes
remain under **Academic Setup**.

The main screen defaults to the nearest upcoming academic year. For ordinary
repeat requests: choose a current class, tick students, review and save. Their
current class is used as the repeat destination automatically. A note is optional;
the recorded reason defaults to "Repeat grade after school review". If nobody
must repeat, leave the repeat list empty. One-time class mappings, current placement
corrections, manual checks and history are in expandable sections. Undo remains
visible beside saved student exceptions.

1. Create the next academic year with its real start and end dates. Years must
   not overlap. Exactly one active year must cover the current school date.
2. Save the next placement for each class, including moves between sections.
   Numeric grades must advance by one. Select **Complete school** explicitly for
   final-grade classes. A missing mapping leaves the students pending for review.
3. Before the new year starts, select students who must repeat, choose their
   same or lower grade/class, and save a reason. Exceptions belong to the selected
   academic year; class mappings are reused every year.

The API checks on startup and every five minutes, using Saudi local dates. It
processes current, active, non-graduated students enrolled in the immediately
previous academic year. Older unresolved enrollments require manual review.
The API must be running; on restart it catches up for the current academic year.
**Check promotion now** runs the same check and displays unresolved students.

Each successful promotion ends the previous enrollment's current flag and creates
one enrollment for the target year. Old attendance, results, subject enrollments,
and academic enrollments remain intact. This does not copy last year's subject
choices or teacher assignments into the new year; configure these normally for
the new academic year. Final-grade completion records graduation against the
source year. The staff member who saved the mapping is recorded in promotion
history, with a reason identifying the automatic run.

Under **Student exceptions**, choose **Repeat next academic year** to schedule
a repeat in a future year, or **Correct current placement** to correct an existing
current-year placement. Select the class and students, enter the destination and
reason, then review the named students before saving. Current-year same-class
requests are rejected rather than logged as a false Repeat. Same-grade class
corrections are logged as MoveClass; lower-grade corrections are logged as Demote.
The existing current-year enrollment is updated rather than duplicated. Attendance
and marks retain their original class/subject relationships.

**Undo** in Saved exceptions reverses the most recent save for a student and year,
after confirmation. New saves capture the student's placement and previous
exception settings in `AnnualPromotionChanges`. Undo restores these settings and
adds an Undo history entry, retaining the original history. It does not erase
attendance, results or enrollments. A newly created enrollment, if reversed, is
retained as inactive rather than deleted.

If a future repeat has already been applied automatically, Undo restores the
normal next-year placement (or the previous saved exception). A valid normal class
mapping is required when no previous exception exists. Undo cannot overwrite later
placement actions, undo the same change twice, or change an ended academic year.
It supports one most-recent save per student/year; it is not a general historical
rollback tool. Older same-year/same-class Repeat entries can be cleared safely
without changing placement. Other older applied changes without snapshots require
an explicit correction rather than a guessed reversal.

Database transactions and a transaction-owned SQL application lock serialize
automatic checks and new mapping/exception changes across API instances. The
existing unique student/year enrollment index also prevents duplicate enrollments.
Repeated checks do not repeat successful promotions. No mappings are enabled by
the migration; managers choose placements before automation processes students.

Schema migration: `20261017185015_AutomaticAnnualPromotion` adds only
`AnnualPromotionRules` and `AnnualPromotionExceptions`, with their indexes and
foreign keys. Apply migrations before starting the updated API in other
environments.

Undo schema migration: `20261017195721_PromotionExceptionUndo` adds
`AnnualPromotionChanges` and its indexes/foreign keys; it does not alter existing
student placements or promotion history.
