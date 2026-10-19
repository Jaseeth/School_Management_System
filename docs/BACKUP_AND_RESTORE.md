# Backup and Restore

Admin-only **School Settings → Backup and Restore** section (`/school-settings?tab=backups`). The old `/backups` URL redirects here. Full SQL Server COPY_ONLY backups are created with checksums, checked using RESTORE VERIFYONLY, then hashed with SHA-256. Verification is not a substitute for periodically testing recovery on a separate database.

## Behaviour

- Manual backup, protected download, paginated backup history, daily school-time schedule (disabled initially), failure history, recent restore history.
- Automatic checks run every minute while the API runs. On startup after the selected time, a missing backup for that day is attempted. A failed automatic attempt is recorded and not repeatedly retried that day; retry manually. Stopped-day backups cannot be recreated retrospectively.
- Restore replaces **all database records**, not just one incorrect action. Admin enters their current password, reason and `RESTORE`. The password check is rate limited and participates in Identity lockout.
- Only a verified file created by this installation for this exact database/server can be restored. Its hash is checked again. Database migration versions must match.
- Before restore, a new verified safety backup is mandatory. If this fails, restore does not begin. Normal API requests pause during recovery. Everyone’s security stamp changes after restore, requiring sign-in again. Accounts/passwords otherwise return to their backup state.
- Backup and restore metadata and the schedule live outside the database and therefore survive restoring an older database. File names are generated GUIDs; clients cannot supply filesystem paths or SQL identifiers.

## Storage and deployment

Default location: repository `.local-data/backups`, outside webroot and ignored by Git. `Backups:Directory` can set another absolute directory. Both the API account and SQL Server service account need read/write access to it. This implementation expects API and SQL Server to share that path; remote/managed SQL deployments need deployment-specific backup integration. Azure SQL Database does not support this native file-backup workflow.

Run one API instance per installation for this initial workflow. Protect the directory with operating-system permissions. Backups contain personal data and password hashes and are **not encrypted by this feature**. Download secure off-device copies; a disk failure can destroy local backups along with the database. No automatic deletion/retention policy is enabled.

These backups contain database data only. External student uploads, server configuration, encryption/data-protection keys, Firebase credentials and other secrets require separate secure backups. The school profile logo is in the database and is included.

## Recovery failures

Keep the page open during long operations. If interrupted, inspect backup/restore history and server logs before retrying. A `Running` / `Restoring` entry after a server crash requires operator review. Native commands attempt to return the database to MULTI_USER even when recovery fails. If the database cannot come online or application startup fails, a database operator must recover it from a verified `.bak` using SQL Server tooling and the matching application version. Do not delete the safety backup.

Implementation references: [SQL Server backups](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/backup-overview-sql-server?view=sql-server-ver17), [copy-only backups](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/copy-only-backups-sql-server?view=sql-server-ver15), [RESTORE VERIFYONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql?view=sql-server-ver17).
