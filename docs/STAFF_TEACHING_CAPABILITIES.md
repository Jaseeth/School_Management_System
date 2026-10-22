# Teaching capabilities during staff creation

In **Staff → Add Staff**, choosing Admin, Section Head or Teacher now requires answering **Can this staff member teach?**

- **No** creates the account without teaching subjects.
- **Yes** displays active subjects from Academic Setup. Search and select one or more subjects. Filtering the list preserves earlier selections.
- Principal and Deputy Principal retain the previous creation flow without this question. Other roles do not receive the teaching question.

Selected subjects are recorded as teaching capabilities, and are visible on the Staff Management card after creation. They do not automatically add another role, assign classes, or change timetable/marks permissions. Teacher Assignments continues to manage actual class/year/subject assignments under its existing access rules.

Drafts retain the teaching answer and selected subject IDs. Passwords remain excluded. A resumed draft with unavailable subjects requires reviewing those selections before submission. Changing role resets the teaching answer and selected subjects; choosing No clears subjects.

Server validation requires a Yes/No answer for the three applicable roles and at least one active subject for Yes. Invalid or inactive subjects are rejected before creating a login. Duplicate subject IDs are saved once. Identity creation, role assignment, staff record and teaching subjects are saved in a transaction. Existing staff records remain unrecorded rather than assuming they can or cannot teach.

Migration: `20261021070024_StaffTeachingCapabilities`, applied to the local development database.

## Verification

- Five SQL integration cases passed, covering Admin/Section Head/Teacher, required answers, multiple subjects, invalid/inactive subject rejection, No, Principal/Deputy Principal, role preservation and draft storage/password exclusion.
- Playwright covers role-specific visibility, searchable multiple selection, draft save/resume, successful account creation and saved subject names in Chrome and WebKit at 390 × 844. Existing registration-draft browser cases are included in the run.
- Browser results: `.local-tools/qa/results/StaffTeaching.trx` (new feature cases); `.local-tools/qa/results/StaffTeachingDraftRecheck.trx` (draft regression recheck). An initial draft assertion timed out while staff creation completed successfully; the recheck waits for the actual API response. Screenshots: `.local-tools/qa/browser/staff-teaching-{chrome,webkit}.png`.
- Production frontend build passed. Targeted lint reports one React state-in-effect warning for subject loading.

Manual check: open Add Staff, choose Teacher → Yes, select two subjects and create. Confirm both appear on the staff card. Try No and verify subjects disappear. Choose Principal or Deputy Principal and verify the question is absent.
