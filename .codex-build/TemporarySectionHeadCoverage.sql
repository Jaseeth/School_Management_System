BEGIN TRANSACTION;
ALTER TABLE [SectionHeadAssignments] ADD [CreatedByUserId] nvarchar(max) NULL;

ALTER TABLE [SectionHeadAssignments] ADD [EndsOn] date NULL;

ALTER TABLE [SectionHeadAssignments] ADD [IsTemporary] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [SectionHeadAssignments] ADD [Reason] nvarchar(500) NULL;

ALTER TABLE [SectionHeadAssignments] ADD [StartsOn] date NULL;

DROP INDEX [IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId] ON [SectionHeadAssignments];

CREATE UNIQUE INDEX [IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId] ON [SectionHeadAssignments] ([StaffId], [SectionId], [AcademicYearId]) WHERE [IsTemporary] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261021090000_TemporarySectionHeadCoverage', N'10.0.12');

COMMIT;
GO

