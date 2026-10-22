# Assigning teaching staff in batches

Open Teacher Assignments > Subject Teachers.

1. Select the academic year.
2. Select a teaching staff member. Active Admin, Section Head and Teacher accounts marked as able to teach are available.
3. Select a subject from that staff member's saved teaching subjects, then select section, grade and class.
4. Use Add another assignment for additional subjects or classes. The same subject can be assigned across different sections and grades, and multiple subjects can be assigned to one class. Multiple staff can also be assigned the same subject and class to support co-teaching.
5. Review the assignments and save them together.

The timetable then determines which assigned teacher teaches each period. The timetable prevents a class from having overlapping periods and a teacher from being scheduled in two classes at overlapping times. For example, T001 can teach ICT to Grade 11 A in period 2, and T004 can teach ICT to that same class in period 7.

For existing staff without teaching information, open Staff Management and use Teaching subjects. Admin can configure their own teaching subjects using this button.

## Validation and history

The server validates all rows together. Duplicate rows, unavailable subjects/classes, and repeating an already active assignment for the same staff member reject the entire batch. The same subject/class may be assigned to multiple staff members. Concurrent saves are serialized. An inactive matching assignment is reactivated using its existing record. Removing teaching capabilities used by active assignments is blocked.

Changing the selected year or staff clears unfinished assignment rows. Each save supports up to 100 rows. Existing assignment search, status and deactivation remain available.

## Scope

This updates subject assignment management. The Class Teachers workflow remains unchanged. Teaching capabilities do not add account roles or independently grant access to additional role-restricted teaching pages. No database migration is required for this update; it uses the existing staff teaching capability tables.

## Verification

The batch assignment and capability workflows were previously checked with API, frontend and browser coverage. The co-teaching rule was added afterward; build the projects and run the integration/browser suite before releasing that change.
