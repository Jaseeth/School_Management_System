# School settings

Admin, Principal and Deputy Principal can manage `/school-settings`. Other roles cannot update settings. The shared authenticated read endpoint supplies the school profile without exposing private credentials.

Fields: school name, address, phone, email, website, country, timezone and optional PNG/JPEG logo (250 KB maximum, 4096 pixels per dimension). Data is stored in the singleton SchoolProfiles record. Changes are audited; a row version prevents stale updates from silently replacing another leader's changes.

The staff dashboard name/logo and the five PDF report headers use this profile. The timezone is loaded on API startup and updated after saving; attendance window calculations use it for new requests. Existing historical timestamps are retained. Country does not overwrite saved yearly calendars, import holidays, or change weekends: those remain explicit in School Calendar.

Database migration: `20261019141320_SchoolProfileSettings`. Applied to the local development database during implementation. Apply migrations through the usual deployment process for other databases. Each deployment currently represents one school; this does not introduce multi-school tenancy.

Browser push alerts keep their generic School Management title and privacy-safe message.
