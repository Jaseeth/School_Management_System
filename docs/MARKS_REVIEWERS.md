# Section marks reviewers

Section Heads can open **Marks Review → Section marks reviewer**, choose one of their section/academic-year assignments, choose an active teacher working in that section, enter a reason, and confirm the assignment. One reviewer is active per section/year. They can replace or remove the reviewer; assignment history remains available.

When a teacher submits a complete marks sheet, the system creates portal notifications for the relevant section heads and assigned reviewer, excluding the submitting teacher. It also attempts browser push delivery. Notifications include the exam, subject, grade, class, academic year, and submitting teacher. Delivery to a physical browser still depends on that browser's subscription and Firebase availability.

The assigned teacher opens **Marks Review** to inspect student marks and approve or return them for correction. The section head keeps review access. Either can decide; the other cannot overwrite a completed decision. Returning marks requires a comment and allows the submitting teacher to correct and resubmit. A reviewer cannot approve or reject their own marks. A review assignment does not grant publishing permission.

Access is checked on the server for the exact section and academic year. Removing the assignment, deactivating the reviewer/account, removing their Teacher role, or removing their active teaching/class-teacher assignment in that section removes delegated access. The assigning section head must retain an active account, Section Head role, and matching section/year assignment. Already submitted sheets become available to a newly assigned reviewer, who receives a portal reminder.

## Validation on 20 October 2026

- Eight SQL integration cases passed in disposable databases: reviewer assignment/scope/removal, submission recipients and push-service calls, head and reviewer decisions, rejection/resubmission, self-review prevention, other-year denial, simultaneous decisions, and existing publishing/section-head rules.
- Two Playwright cases passed, using Chrome and WebKit at a 390 × 844 viewport: assign reviewer, teacher opens student marks and approves, head sees no pending submission, remove reviewer, and inspect retained assignment history. No horizontal page overflow was detected. WebKit checks browser-engine compatibility; this is not a physical Safari/iPhone test.
- Production frontend and normal API builds passed. Targeted frontend lint reports an existing `set-state-in-effect` warning in the review page.
- Migration `20261020192703_SectionMarksReviewers` was applied to the local development database. It adds reviewer assignments and row-version protection against conflicting decisions.

Test sources: `MarksReviewerTests.cs`, `MarksReviewerBrowserTests.cs`. Browser results: `.local-tools/qa/results/MarksReviewerBrowser.trx`; screenshots: `.local-tools/qa/browser/marks-reviewer-{chrome,webkit}.png`.
