# Parent password recovery

Parents open **Forgot password?** on `/parent/login`, enter their registered email, verify a six-digit email code, then choose and confirm a new password. After success they return to login. If they cannot access the registered email, they must contact the school to update their account.

## Conditions

- Recovery is limited to active login accounts with the Parent role and an active linked parent/guardian record.
- Unknown emails, inactive accounts and other roles receive the same generic request confirmation. No account details or codes are returned by the API.
- Codes expire five minutes after the request, allow five incorrect attempts, and can be resent after 60 seconds. Resending invalidates previous codes and verified reset grants.
- Verification returns a short-lived, unguessable reset grant, kept only in React memory. The server requires this grant to reset the password. An email address or previously verified database record alone cannot authorize a reset.
- The challenge is bound to the account, its current email and security stamp. Email/account changes, expiry, reuse and invalid grants prevent reset.
- Passwords must match and satisfy ASP.NET Identity password rules, with a minimum of eight and maximum of 128 characters for this workflow.
- Success consumes pending challenges, changes the security stamp (invalidating prior sessions), clears account lockout and records an audit entry without passwords or codes.
- Failed email delivery invalidates the challenge and shows a delivery error. Email uses the school's existing configured provider; no new service or database migration is required.
- All three endpoints use the existing OTP rate limit of five requests per IP per minute.

## Validation

`ParentPasswordResetTests.cs` tests the actual Identity and SQL flow with a fake email sender: request/cooldown, verification, grant requirement, password mismatch and weak passwords, successful reset, session-stamp change, reuse, five-attempt limit, expiry, resend invalidation, unknown/inactive/non-parent accounts and failed email delivery.

`ParentPasswordRecoveryBrowserTests.cs` tests the page flow at mobile size on Chrome and Playwright WebKit with synthetic API responses: login link, invalid code, verified code, mismatched confirmation, success, return to login and horizontal overflow. It does not prove actual inbox delivery or native Safari behavior.

After deployment, verify receipt of a code using a real parent test account and the school's email provider. Never paste verification codes, passwords or reset grants into logs or tickets.
