# Extra staff leave allowance

The shared annual base allowance still covers all leave types. Approved extra days are added for one staff member and one calendar year. For example, 40 base days plus 6 approved extra days gives that staff member 46 days; other staff keep their own existing totals.

## Flows

| Action | Review | Notifications |
| --- | --- | --- |
| Staff requests extra days for themselves with a reason | Principal approves or rejects | Request alerts go to Admin and eligible Principals |
| Admin requests extra days for a selected staff member | Principal approves or rejects | Request alerts go to eligible Principals |
| Principal grants extra days directly to another staff member | Applied immediately with a required reason | Staff member, active Admin accounts and relevant section heads |
| Principal approves a request | Approved amount is added to the allowance | Staff member, requesting staff/Admin, active Admin accounts and relevant section heads |
| Principal rejects a request | Balance is unchanged | Staff member, requester and active Admin accounts |

Principal may approve a different number of days from the number requested. Both the requested and approved amounts are retained, along with the original reason, decision reason, actor names and timestamps.

## Where to use it

- **Staff Leave → Extra leave days:** available to staff. Staff can request their own days and view their history. Section heads can also view requests belonging to staff in their assigned sections.
- **Staff Leave Allowance:** Admin sets the default for all staff. Admin and Principal can view extra requests; Principal can review or grant them.
- **Staff Leave → Leave balances:** shows the base allowance, approved extra days, total allowance, usage and remaining days.
- Notifications open the Extra leave days tab.

## Rules

- Days, staff member, calendar year and reason are required. Reasons and decision remarks are limited to 1,000 characters.
- The base allowance must be configured before extra days can be requested or granted.
- Extra days must be positive whole days. The effective annual allowance may not exceed 366 days.
- Pending requests never increase the balance. Only one pending request per staff/year is allowed.
- Only an active Principal can approve or directly grant days. Admin cannot directly save an individual increase through the old allowance endpoint.
- A Principal cannot approve their own request or their own allowance, or directly grant days to themselves. Another active Principal must handle it.
- A second approval cannot add the same days twice. Requests, decisions, audit records and in-app notifications are committed together. Push delivery failure does not reverse a saved decision.
- Existing individual base allowances are preserved as legacy values; this change does not rewrite historical entitlements or prior leave records.
- Weekends and school holidays remain excluded using the existing leave calendar. Granting an allowance does not create an absence or reserve leave dates: staff must still request actual leave dates through Staff Leave.
- A reason such as government-approved maternity entitlement records the school's decision. The application does not infer legal entitlement or automatically grant days from the reason text.

## Tests

`ExtraLeaveAllowanceTests.cs` covers Admin requests, staff requests, adjusted approval amounts, Principal direct grants, rejection, self-approval restrictions, pending duplicates, recipient scope, history scope and annual totals. The existing calendar/balance test preserves its legacy-base scenario while checking that new direct individual saves are refused.

`ExtraLeaveAllowanceBrowserTests.cs` exercises a real staff request, Principal approval, direct grant, resulting balance and notification inbox on Chrome and Playwright WebKit at mobile size, using isolated test accounts and databases. External operating-system push delivery must still be checked on real devices.
