# Student leadership

Open **Student Leadership** in the staff sidebar and choose an academic year.

## Responsibilities and access

- A class teacher can appoint one **Class Monitor** and one **Assistant Class Monitor** per assigned class/year. Only currently enrolled, active students in that class are eligible. One student cannot hold both monitor roles at the same time.
- **Admin** appoints **Prefects**, **Senior Prefects** and **Assistant Senior Prefects** across the school, individually or in batches. Each student can hold one active prefect role per academic year. A monitor may also be a prefect.
- Admin, Principal and Deputy Principal can view school-wide appointments/history. Section heads see their sections; class teachers see their assigned classes. Prefect changes are restricted to Admin. Monitor changes are restricted to the relevant class teacher.
- Appointments are student responsibilities; they do not grant application permissions.

## Appoint and replace

Select a role, class (required for monitors), start date, optional responsibilities and students. Search students by partial name or index number. Confirm the appointment before saving. If a monitor position is occupied, a second confirmation identifies the current holder before replacement. The previous appointment remains in history.

## Individual and bulk changes

Select eligible rows on a page, or use **Select all matching prefects** to include all active prefect appointments across pages matching the current year/class/role/search filters. Monitors are always excluded from that bulk-prefect option. Changing filters clears the selection.

Use **Update role / responsibilities**, **Complete appointment**, **Completed — left school**, or **Remove appointment**. A reason and confirmation are required; endings also require an end date. Ending an appointment never changes student enrollment, graduation or account status. Inactive appointments are preserved, and new appointments can be made afterward.

Choose **All appointments / history**, then **View history** to see actions, previous/new roles, responsibilities, reasons, person and time. Names and index numbers are retained in change records. Database constraints and revision checks protect occupied positions and stale changes.

## Notifications

- Monitor appointments/changes/endings: affected students, relevant class teacher, section head, Admin and Principal.
- Prefect appointments/changes/endings: affected students and all active staff accounts, including teachers, section heads and school leaders.
- Parents receive no leadership portal notifications or push alerts.

Portal notifications are saved with the appointment change. Browser push is attempted after commit; browser permission/registration is still required for device delivery.

## Verification

SQL integration tests cover teacher scope, Principal read-only prefect management, occupied monitor confirmation, replacement/history, duplicate roles, stale updates, bulk prefect role changes/endings, reappointment, unchanged enrollment and notification audiences without parents. Browser tests cover both monitor roles, Admin bulk appointments/endings, history and mobile width in Chrome and WebKit. WebKit is engine coverage, not a native Safari device test.
