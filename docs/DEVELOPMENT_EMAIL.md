# Development email with Mailpit

The API uses Mailpit in Development and Resend by default in other environments.
OTP validation, registration codes, passwords and expiry checks are unchanged.

From the project root in PowerShell:

```powershell
.\scripts\start-mailpit.ps1
dotnet run --project src\SchoolManagement.API\SchoolManagement.API.csproj --launch-profile https
```

Restart an already running API to load the new email provider.
Open http://localhost:8025 to read captured OTP messages. Emails retain the student or
parent recipient address, but are stored locally instead of delivered to real inboxes.

The start script downloads the pinned official Windows amd64 binary and verifies its
SHA-256 digest on first use. Later runs reuse it. Both listeners bind only to localhost.
The binary and captured messages live in ignored `.local-tools/mailpit`.

To stop it, stop the `mailpit` process in Task Manager. Start it again with the script.
Never commit captured messages or OTPs. In other environments, Mailpit configuration
is rejected. Production continues to require Resend credentials and a verified sender.

If OTP delivery fails locally, run the start script, check the inbox, and retry requesting
a code. No Resend API key, sender-domain verification, or EmailSettings database row
is needed for Mailpit delivery.
