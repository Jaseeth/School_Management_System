# Staff Management checkpoint

Implemented 7 October 2026. This checkpoint covers staff listing and account creation; it does not add edit, deletion, or assignment workflows.

## Changes

- `school-management-web/src/api/staffManagementApi.js`: wraps existing GET /Staff, GET /Roles, and POST /Staff; formats validation/permission errors without logging credentials.
- `school-management-web/src/pages/staff/StaffManagementPage.jsx`: searchable staff list, active/inactive/password-change filters, client-side pagination, roles, designation/email, account status, and creation confirmation. Uses the existing staff layout and management-role menu.
- `school-management-web/src/pages/staff/AddStaffForm.jsx`: existing-role lookup, staff account creation, temporary-password confirmation, duplicate-submit protection, responsive inputs, permission/error handling, and masked credentials. Passwords are never placed in persistent storage, notifications, or logs; the form clears them after requests or when closed.
- `school-management-web/src/routes/AppRoutes.jsx`: registers /staff so the existing Staff sidebar link opens the new screen.
- `src/SchoolManagement.API/Controllers/StaffController.cs`: trims non-password creation fields, rejects Student/Parent roles for staff creation, and adds role names and Identity account active status to the existing list response. Existing Staff.View and Staff.Create permissions and first-login password-change requirement are retained.
- `src/SchoolManagement.Application/Staff/DTOs/CreateStaffRequest.cs`: adds required-field, email, and length validation.

No new entities, endpoints, migrations, or database records were created. Existing project changes were preserved.

## First confirmation

1. Restart the API to load its updated controller/DTO, and use the current frontend.
2. Sign in as Admin and open Staff. Check that T001 and SH001 appear with their roles. Search T001 and verify its email and designation; clear the search afterward.
3. Open Add Staff. Student and Parent must not appear as role choices.
4. Choose a unique staff number and email. For example, use TTEST01 / Staff Workflow Test / staffworkflowtest@school.com only if they are not already listed. Select Teacher and enter a strong temporary password and matching confirmation. Do not send the password in chat.
5. Click Create Staff once. Expected: a success message, the form closes, and the new staff account appears in the searchable list with Active status, Teacher role, and Password change required.
6. Refresh the list and search the new staff number to confirm persistence. Test at a narrow mobile width too: inputs and cards should stack without horizontal overflow.

Confirm this creation checkpoint before continuing. First-login acceptance will use the new staff number/email and temporary password, require a password change, then check sign-in using the new password. Browser creation/login and rendered mobile behavior have not been verified by the agent.

## Checks

Frontend production build passed, with the existing bundle-size warning. Focused lint for the new page, form, and API wrapper passed without findings. Git diff whitespace checks passed. The normal API build could not copy assemblies because the running API locks them; an isolated build using `dotnet build src/SchoolManagement.API/SchoolManagement.API.csproj --no-restore --nologo -m:1 -o src/SchoolManagement.API/bin/StaffVerification -p:UseAppHost=false` passed with zero errors and five warnings in unchanged Firebase, special-class, exception-middleware and startup code. Stop and rebuild/restart the running backend before testing. No test accounts were created during these checks.
