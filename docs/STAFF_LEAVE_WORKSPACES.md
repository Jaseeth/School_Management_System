# Staff leave workspaces

The Staff Leave page separates personal leave from managing other staff members.

| Workspace | Contents |
| --- | --- |
| My leave | My requests, Request leave, My balance, Extra leave days, Withdraw / cancel, My cover duties |
| Staff management | Pending approvals, Staff leave history, Cancellation requests, Arrange cover, Staff balances, Extra leave days |
| Section management | Section requests, Staff leave history, Cancellation requests, Arrange cover, Extra leave days |

Admin, Principal and Deputy Principal open the management workspace by default. Section heads open Section management. Teachers and other staff open their own requests without a workspace switch. Deputy Principal does not receive extra-day approval powers.

Personal extra-day and cancellation views contain only the signed-in staff member's records. Management histories exclude that person's own records. Existing section restrictions, approval rules, notifications, leave calculations and audit history still apply. Staff balances retain the existing authorized staff selector.

The selected workspace and category are saved in the URL, so reload and browser navigation retain the view. Older notification links continue to resolve. Leave notification details distinguish personal requests from requests about another staff member.

## Verification

- Scoped SQL integration checks cover personal and management separation, teacher access restrictions, history exclusion and notification ownership.
- Browser checks exercise Admin, teacher and section-head navigation, legacy links, reload persistence, personal and management forms, and mobile overflow in Chrome and WebKit.
- Frontend production build and targeted lint checks run separately.

Browser verification uses disposable test databases. WebKit provides engine coverage; it is not a native Safari device test.
