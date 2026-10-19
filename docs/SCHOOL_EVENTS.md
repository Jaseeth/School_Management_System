# School events

Open **School Calendar → School Events**. Admin, Principal and Deputy Principal accounts with an active staff profile can create, edit and cancel events. Other staff, students and parents can view only events addressed to them.

An event includes a title, description, location, inclusive dates within one calendar year, and optional start/end times in school local time. Leave both times blank for an all-day event. Timed multi-day events use the same time window each day.

Choose Whole school, Section, Grade or Class, then Staff, Students and/or Parents. Scoped students come from their current active enrollment. Parents come from active guardian relationships to those students. Scoped staff include active subject/class teachers and section heads assigned in an active academic year. Whole-school selection includes active profiles. Recipient lists are saved when publishing or editing, so later changes to student placement do not silently change an existing invitation; edit the event to refresh its audience.

Publishing, editing and cancellation create in-app notifications atomically with the event. Optional push failures do not lose the saved event or notification. An audience edit notifies newly/currently invited recipients and tells removed recipients that the event no longer applies. Calendar notification links open the Events tab. Choose the relevant calendar year for events outside the current year.

An ordinary event does not change school-day status, including on weekends. **School closed for this event** requires the whole school and all three recipient groups. Its dates are excluded once from planned school-day totals and staff leave working-day calculations. Updating the event moves/removes its closure automatically; cancelling removes only that event's closure. Independently saved public holidays, other holiday events, weekends and school opening dates continue to apply. Existing attendance records are not changed.

Cancelled events remain visible to their recipients and managers, with the cancellation reason. They cannot be edited or cancelled again; create a new event to reschedule a cancelled activity. Concurrent edits use a revision check and return a refresh message instead of overwriting a newer decision.

Migration: `20261019094120_SchoolEvents` adds the SchoolEvents table. No existing school records are deleted. Tests use a separate temporary database, and browser checks use sample data without notifying real users.
