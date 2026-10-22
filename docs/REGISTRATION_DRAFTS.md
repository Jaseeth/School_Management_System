# Staff and student registration drafts

Admin, Principal and Deputy Principal can save their own unfinished staff and student forms in the database. Drafts are separate from active staff accounts, students, parents and enrollment records. Other users cannot open or delete a creator's drafts.

## Use

1. Start Add Staff or Add Student and enter any available details.
2. Choose **Save draft** at any time, even when required fields are incomplete.
3. When navigating elsewhere in the app or cancelling an unsaved form, choose **Save as draft and leave**, **Discard and leave**, or **Keep editing**.
4. Open **Saved drafts** on Staff Management or Students and select **Edit draft**. Student drafts restore the wizard step, academic selections, subjects and parent details.
5. Complete normal registration to create the actual record and remove its draft. All normal registration rules still apply.
6. Use **Delete draft** and confirm to remove a saved draft. Cancelling deletion leaves it intact.

Discarding edits to an existing draft retains the last saved version. Saving again updates the same draft. Conflicting updates from another window are rejected instead of silently overwriting them.

Staff temporary passwords and confirmations are excluded from both the draft request and stored data. They must be entered again when completing registration. Drafts are not stored in localStorage.

## Closing the browser

Browsers prohibit custom Save/Discard dialogs during tab closing, window closing and reload. An unsaved form triggers the browser's native leave warning. To save, stay on the page and choose **Save draft** before closing. Choosing to leave discards unsaved changes; an existing saved draft remains available.

## Implementation and verification

- Migration: `20261020131616_RegistrationDrafts`, an additive draft table with owner, kind, sanitized data, update time and row version.
- API checks cover incomplete forms, password exclusion, invalid payloads, owner isolation, stale versions, deletion and absence of active records created by draft saves.
- Browser checks cover staff completion/removal, student wizard and academic-selection restoration, Back navigation, native unload protection, keep editing, discard and confirmed deletion in Chrome and WebKit at mobile width.
- The app uses React Router's data router to support its navigation blocker. Existing page routes and role restrictions remain in place.

WebKit engine testing is not a native Safari device test.
