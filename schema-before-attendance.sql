IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AcademicYears] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_AcademicYears] PRIMARY KEY ([Id])
);

CREATE TABLE [Sections] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Sections] PRIMARY KEY ([Id])
);

CREATE TABLE [Subjects] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Subjects] PRIMARY KEY ([Id])
);

CREATE TABLE [Grades] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [SectionId] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Grades] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Grades_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [SchoolClasses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [GradeId] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_SchoolClasses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SchoolClasses_Grades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [Grades] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Grades_SectionId] ON [Grades] ([SectionId]);

CREATE INDEX [IX_SchoolClasses_GradeId] ON [SchoolClasses] ([GradeId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915102228_InitialCreate', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915104606_AddIdentity', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Permissions] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [RolePermissions] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [PermissionId] int NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RolePermissions_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_Permissions_Name] ON [Permissions] ([Name]);

CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);

CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions] ([RoleId], [PermissionId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915105744_AddPermissions', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Staff] (
    [Id] int NOT NULL IDENTITY,
    [StaffNumber] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Designation] nvarchar(max) NULL,
    [ApplicationUserId] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Staff] PRIMARY KEY ([Id])
);

CREATE TABLE [Students] (
    [Id] int NOT NULL IDENTITY,
    [IndexNumber] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [DateOfBirth] datetime2 NULL,
    [SchoolClassId] int NOT NULL,
    [ApplicationUserId] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Students] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Students_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id])
);

CREATE UNIQUE INDEX [IX_Staff_StaffNumber] ON [Staff] ([StaffNumber]);

CREATE UNIQUE INDEX [IX_Students_IndexNumber] ON [Students] ([IndexNumber]);

CREATE INDEX [IX_Students_SchoolClassId] ON [Students] ([SchoolClassId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915110443_AddStudentAndStaff', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [OtpVerifications] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(max) NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Purpose] nvarchar(max) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OtpVerifications] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915113948_AddOtpVerification', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [EmailSettings] (
    [Id] int NOT NULL IDENTITY,
    [SenderName] nvarchar(max) NOT NULL,
    [SenderEmail] nvarchar(max) NOT NULL,
    [ReplyToEmail] nvarchar(max) NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EmailSettings] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915123414_AddEmailSettings', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EmailSettings]') AND [c].[name] = N'ReplyToEmail');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [EmailSettings] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [EmailSettings] DROP COLUMN [ReplyToEmail];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915124427_RemoveEmailReplyTo', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [AspNetUsers] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915191749_AddMustChangePassword', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [TeacherAssignments] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [StaffId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_TeacherAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TeacherAssignments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_TeacherAssignments_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_TeacherAssignments_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_TeacherAssignments_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id])
);

CREATE UNIQUE INDEX [IX_TeacherAssignments_AcademicYearId_StaffId_SchoolClassId_SubjectId] ON [TeacherAssignments] ([AcademicYearId], [StaffId], [SchoolClassId], [SubjectId]);

CREATE INDEX [IX_TeacherAssignments_SchoolClassId] ON [TeacherAssignments] ([SchoolClassId]);

CREATE INDEX [IX_TeacherAssignments_StaffId] ON [TeacherAssignments] ([StaffId]);

CREATE INDEX [IX_TeacherAssignments_SubjectId] ON [TeacherAssignments] ([SubjectId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916091012_AddTeacherAssignments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AcademicTerms] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [AcademicYearId] int NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_AcademicTerms] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AcademicTerms_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id])
);

CREATE TABLE [Exams] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [AcademicTermId] int NOT NULL,
    [ExamDate] datetime2 NOT NULL,
    [MaximumMarks] decimal(10,2) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Exams] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Exams_AcademicTerms_AcademicTermId] FOREIGN KEY ([AcademicTermId]) REFERENCES [AcademicTerms] ([Id])
);

CREATE UNIQUE INDEX [IX_AcademicTerms_AcademicYearId_Name] ON [AcademicTerms] ([AcademicYearId], [Name]);

CREATE UNIQUE INDEX [IX_Exams_AcademicTermId_Name] ON [Exams] ([AcademicTermId], [Name]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916093815_AddTermsAndExams', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StudentMarks] (
    [Id] int NOT NULL IDENTITY,
    [ExamId] int NOT NULL,
    [StudentId] int NOT NULL,
    [TeacherAssignmentId] int NOT NULL,
    [MarksObtained] decimal(10,2) NOT NULL,
    [IsSubmitted] bit NOT NULL,
    [SubmittedAt] datetime2 NULL,
    [IsPublished] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_StudentMarks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentMarks_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]),
    CONSTRAINT [FK_StudentMarks_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]),
    CONSTRAINT [FK_StudentMarks_TeacherAssignments_TeacherAssignmentId] FOREIGN KEY ([TeacherAssignmentId]) REFERENCES [TeacherAssignments] ([Id])
);

CREATE UNIQUE INDEX [IX_StudentMarks_ExamId_StudentId_TeacherAssignmentId] ON [StudentMarks] ([ExamId], [StudentId], [TeacherAssignmentId]);

CREATE INDEX [IX_StudentMarks_StudentId] ON [StudentMarks] ([StudentId]);

CREATE INDEX [IX_StudentMarks_TeacherAssignmentId] ON [StudentMarks] ([TeacherAssignmentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916103628_AddStudentMarks', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [MarksSubmissions] (
    [Id] int NOT NULL IDENTITY,
    [ExamId] int NOT NULL,
    [TeacherAssignmentId] int NOT NULL,
    [Status] int NOT NULL,
    [SubmittedByStaffId] int NOT NULL,
    [SubmittedAt] datetime2 NULL,
    [ReviewedByStaffId] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ReviewComment] nvarchar(1000) NULL,
    [PublishedByStaffId] int NULL,
    [PublishedAt] datetime2 NULL,
    CONSTRAINT [PK_MarksSubmissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MarksSubmissions_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]),
    CONSTRAINT [FK_MarksSubmissions_Staff_PublishedByStaffId] FOREIGN KEY ([PublishedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_MarksSubmissions_Staff_ReviewedByStaffId] FOREIGN KEY ([ReviewedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_MarksSubmissions_Staff_SubmittedByStaffId] FOREIGN KEY ([SubmittedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_MarksSubmissions_TeacherAssignments_TeacherAssignmentId] FOREIGN KEY ([TeacherAssignmentId]) REFERENCES [TeacherAssignments] ([Id])
);

CREATE UNIQUE INDEX [IX_MarksSubmissions_ExamId_TeacherAssignmentId] ON [MarksSubmissions] ([ExamId], [TeacherAssignmentId]);

CREATE INDEX [IX_MarksSubmissions_PublishedByStaffId] ON [MarksSubmissions] ([PublishedByStaffId]);

CREATE INDEX [IX_MarksSubmissions_ReviewedByStaffId] ON [MarksSubmissions] ([ReviewedByStaffId]);

CREATE INDEX [IX_MarksSubmissions_SubmittedByStaffId] ON [MarksSubmissions] ([SubmittedByStaffId]);

CREATE INDEX [IX_MarksSubmissions_TeacherAssignmentId] ON [MarksSubmissions] ([TeacherAssignmentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916111243_AddMarksSubmissionWorkflow', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StaffPermissionDelegations] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [PermissionId] int NOT NULL,
    [SectionId] int NULL,
    [GrantedByStaffId] int NOT NULL,
    [GrantedAt] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [RevokedByStaffId] int NULL,
    [RevokedAt] datetime2 NULL,
    CONSTRAINT [PK_StaffPermissionDelegations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffPermissionDelegations_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]),
    CONSTRAINT [FK_StaffPermissionDelegations_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]),
    CONSTRAINT [FK_StaffPermissionDelegations_Staff_GrantedByStaffId] FOREIGN KEY ([GrantedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StaffPermissionDelegations_Staff_RevokedByStaffId] FOREIGN KEY ([RevokedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StaffPermissionDelegations_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
);

CREATE INDEX [IX_StaffPermissionDelegations_GrantedByStaffId] ON [StaffPermissionDelegations] ([GrantedByStaffId]);

CREATE INDEX [IX_StaffPermissionDelegations_PermissionId] ON [StaffPermissionDelegations] ([PermissionId]);

CREATE INDEX [IX_StaffPermissionDelegations_RevokedByStaffId] ON [StaffPermissionDelegations] ([RevokedByStaffId]);

CREATE INDEX [IX_StaffPermissionDelegations_SectionId] ON [StaffPermissionDelegations] ([SectionId]);

CREATE INDEX [IX_StaffPermissionDelegations_StaffId_PermissionId_SectionId] ON [StaffPermissionDelegations] ([StaffId], [PermissionId], [SectionId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916112427_AddStaffPermissionDelegations', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SectionHeadAssignments] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [SectionId] int NOT NULL,
    [AcademicYearId] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_SectionHeadAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SectionHeadAssignments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_SectionHeadAssignments_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]),
    CONSTRAINT [FK_SectionHeadAssignments_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
);

CREATE INDEX [IX_SectionHeadAssignments_AcademicYearId] ON [SectionHeadAssignments] ([AcademicYearId]);

CREATE INDEX [IX_SectionHeadAssignments_SectionId] ON [SectionHeadAssignments] ([SectionId]);

CREATE UNIQUE INDEX [IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId] ON [SectionHeadAssignments] ([StaffId], [SectionId], [AcademicYearId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916130713_AddSectionHeadAssignments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ClassTeacherAssignments] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [StaffId] int NOT NULL,
    [AssignedByStaffId] int NOT NULL,
    [AssignedAt] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_ClassTeacherAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ClassTeacherAssignments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_ClassTeacherAssignments_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_ClassTeacherAssignments_Staff_AssignedByStaffId] FOREIGN KEY ([AssignedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_ClassTeacherAssignments_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
);

CREATE TABLE [StudentAttendances] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [StudentId] int NOT NULL,
    [AttendanceDate] date NOT NULL,
    [Status] int NOT NULL,
    [Remarks] nvarchar(500) NULL,
    [MarkedByStaffId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_StudentAttendances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentAttendances_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_StudentAttendances_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_StudentAttendances_Staff_MarkedByStaffId] FOREIGN KEY ([MarkedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StudentAttendances_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id])
);

CREATE TABLE [TemporaryClassTeacherAssignments] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [StaffId] int NOT NULL,
    [AssignedByStaffId] int NOT NULL,
    [AssignedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsRevoked] bit NOT NULL,
    [RevokedAt] datetime2 NULL,
    [RevokedByStaffId] int NULL,
    [Reason] nvarchar(500) NULL,
    CONSTRAINT [PK_TemporaryClassTeacherAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAssignments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAssignments_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAssignments_Staff_AssignedByStaffId] FOREIGN KEY ([AssignedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAssignments_Staff_RevokedByStaffId] FOREIGN KEY ([RevokedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAssignments_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
);

CREATE TABLE [TemporaryClassTeacherAccessRequests] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [RequestedByStaffId] int NOT NULL,
    [RequestedAt] datetime2 NOT NULL,
    [Reason] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [ReviewedByStaffId] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ReviewRemarks] nvarchar(500) NULL,
    [TemporaryClassTeacherAssignmentId] int NULL,
    CONSTRAINT [PK_TemporaryClassTeacherAccessRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAccessRequests_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAccessRequests_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAccessRequests_Staff_RequestedByStaffId] FOREIGN KEY ([RequestedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAccessRequests_Staff_ReviewedByStaffId] FOREIGN KEY ([ReviewedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_TemporaryClassTeacherAccessRequests_TemporaryClassTeacherAssignments_TemporaryClassTeacherAssignmentId] FOREIGN KEY ([TemporaryClassTeacherAssignmentId]) REFERENCES [TemporaryClassTeacherAssignments] ([Id])
);

CREATE UNIQUE INDEX [IX_ClassTeacherAssignments_AcademicYearId_SchoolClassId_StaffId] ON [ClassTeacherAssignments] ([AcademicYearId], [SchoolClassId], [StaffId]);

CREATE INDEX [IX_ClassTeacherAssignments_AssignedByStaffId] ON [ClassTeacherAssignments] ([AssignedByStaffId]);

CREATE INDEX [IX_ClassTeacherAssignments_SchoolClassId] ON [ClassTeacherAssignments] ([SchoolClassId]);

CREATE INDEX [IX_ClassTeacherAssignments_StaffId] ON [ClassTeacherAssignments] ([StaffId]);

CREATE INDEX [IX_StudentAttendances_AcademicYearId] ON [StudentAttendances] ([AcademicYearId]);

CREATE INDEX [IX_StudentAttendances_MarkedByStaffId] ON [StudentAttendances] ([MarkedByStaffId]);

CREATE INDEX [IX_StudentAttendances_SchoolClassId] ON [StudentAttendances] ([SchoolClassId]);

CREATE UNIQUE INDEX [IX_StudentAttendances_StudentId_AttendanceDate] ON [StudentAttendances] ([StudentId], [AttendanceDate]);

CREATE INDEX [IX_TemporaryClassTeacherAccessRequests_AcademicYearId] ON [TemporaryClassTeacherAccessRequests] ([AcademicYearId]);

CREATE INDEX [IX_TemporaryClassTeacherAccessRequests_RequestedByStaffId] ON [TemporaryClassTeacherAccessRequests] ([RequestedByStaffId]);

CREATE INDEX [IX_TemporaryClassTeacherAccessRequests_ReviewedByStaffId] ON [TemporaryClassTeacherAccessRequests] ([ReviewedByStaffId]);

CREATE INDEX [IX_TemporaryClassTeacherAccessRequests_SchoolClassId] ON [TemporaryClassTeacherAccessRequests] ([SchoolClassId]);

CREATE INDEX [IX_TemporaryClassTeacherAccessRequests_TemporaryClassTeacherAssignmentId] ON [TemporaryClassTeacherAccessRequests] ([TemporaryClassTeacherAssignmentId]);

CREATE INDEX [IX_TemporaryClassTeacherAssignments_AcademicYearId] ON [TemporaryClassTeacherAssignments] ([AcademicYearId]);

CREATE INDEX [IX_TemporaryClassTeacherAssignments_AssignedByStaffId] ON [TemporaryClassTeacherAssignments] ([AssignedByStaffId]);

CREATE INDEX [IX_TemporaryClassTeacherAssignments_RevokedByStaffId] ON [TemporaryClassTeacherAssignments] ([RevokedByStaffId]);

CREATE INDEX [IX_TemporaryClassTeacherAssignments_SchoolClassId] ON [TemporaryClassTeacherAssignments] ([SchoolClassId]);

CREATE INDEX [IX_TemporaryClassTeacherAssignments_StaffId] ON [TemporaryClassTeacherAssignments] ([StaffId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260917113500_AddClassTeacherAttendanceWorkflow', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StaffLeaveRequests] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [LeaveType] int NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [Status] int NOT NULL,
    [RequestedAt] datetime2 NOT NULL,
    [ReviewedByStaffId] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ReviewRemarks] nvarchar(1000) NULL,
    [CancelledAt] datetime2 NULL,
    CONSTRAINT [PK_StaffLeaveRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffLeaveRequests_Staff_ReviewedByStaffId] FOREIGN KEY ([ReviewedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StaffLeaveRequests_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
);

CREATE INDEX [IX_StaffLeaveRequests_ReviewedByStaffId] ON [StaffLeaveRequests] ([ReviewedByStaffId]);

CREATE INDEX [IX_StaffLeaveRequests_StaffId_FromDate_ToDate] ON [StaffLeaveRequests] ([StaffId], [FromDate], [ToDate]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260917140111_AddStaffLeaveWorkflow', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Notifications] (
    [Id] int NOT NULL IDENTITY,
    [RecipientStaffId] int NOT NULL,
    [Type] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ReadAt] datetime2 NULL,
    [ReferenceType] nvarchar(100) NULL,
    [ReferenceId] int NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notifications_Staff_RecipientStaffId] FOREIGN KEY ([RecipientStaffId]) REFERENCES [Staff] ([Id])
);

CREATE INDEX [IX_Notifications_RecipientStaffId_IsRead_CreatedAt] ON [Notifications] ([RecipientStaffId], [IsRead], [CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260917215033_AddStaffNotifications', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StaffDeviceTokens] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [Token] nvarchar(1000) NOT NULL,
    [Platform] int NOT NULL,
    [DeviceName] nvarchar(200) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [LastUsedAt] datetime2 NULL,
    CONSTRAINT [PK_StaffDeviceTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffDeviceTokens_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_StaffDeviceTokens_StaffId_IsActive] ON [StaffDeviceTokens] ([StaffId], [IsActive]);

CREATE UNIQUE INDEX [IX_StaffDeviceTokens_Token] ON [StaffDeviceTokens] ([Token]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260919103504_AddStaffDeviceTokens', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SpecialClassSessions] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [AcademicTermId] int NULL,
    [SchoolClassId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [StaffId] int NOT NULL,
    [ClassDate] date NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [Room] nvarchar(100) NULL,
    [Reason] nvarchar(500) NULL,
    [Remarks] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [CreatedByStaffId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ReviewedByStaffId] int NULL,
    [ReviewedAt] datetime2 NULL,
    [ReviewRemarks] nvarchar(500) NULL,
    [CancelledAt] datetime2 NULL,
    [CancelledByStaffId] int NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_SpecialClassSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SpecialClassSessions_AcademicTerms_AcademicTermId] FOREIGN KEY ([AcademicTermId]) REFERENCES [AcademicTerms] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_Staff_CancelledByStaffId] FOREIGN KEY ([CancelledByStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_Staff_CreatedByStaffId] FOREIGN KEY ([CreatedByStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_Staff_ReviewedByStaffId] FOREIGN KEY ([ReviewedByStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SpecialClassSessions_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [TimetableEntries] (
    [Id] int NOT NULL IDENTITY,
    [AcademicYearId] int NOT NULL,
    [AcademicTermId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [StaffId] int NOT NULL,
    [Day] int NOT NULL,
    [StartTime] time NOT NULL,
    [EndTime] time NOT NULL,
    [Room] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedByStaffId] int NOT NULL,
    CONSTRAINT [PK_TimetableEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TimetableEntries_AcademicTerms_AcademicTermId] FOREIGN KEY ([AcademicTermId]) REFERENCES [AcademicTerms] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TimetableEntries_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TimetableEntries_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TimetableEntries_Staff_CreatedByStaffId] FOREIGN KEY ([CreatedByStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TimetableEntries_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TimetableEntries_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_SpecialClassSessions_AcademicTermId] ON [SpecialClassSessions] ([AcademicTermId]);

CREATE INDEX [IX_SpecialClassSessions_AcademicYearId] ON [SpecialClassSessions] ([AcademicYearId]);

CREATE INDEX [IX_SpecialClassSessions_CancelledByStaffId] ON [SpecialClassSessions] ([CancelledByStaffId]);

CREATE INDEX [IX_SpecialClassSessions_ClassDate_SchoolClassId_StartTime] ON [SpecialClassSessions] ([ClassDate], [SchoolClassId], [StartTime]);

CREATE INDEX [IX_SpecialClassSessions_CreatedByStaffId] ON [SpecialClassSessions] ([CreatedByStaffId]);

CREATE INDEX [IX_SpecialClassSessions_ReviewedByStaffId] ON [SpecialClassSessions] ([ReviewedByStaffId]);

CREATE INDEX [IX_SpecialClassSessions_SchoolClassId] ON [SpecialClassSessions] ([SchoolClassId]);

CREATE INDEX [IX_SpecialClassSessions_StaffId_ClassDate_StartTime] ON [SpecialClassSessions] ([StaffId], [ClassDate], [StartTime]);

CREATE INDEX [IX_SpecialClassSessions_SubjectId] ON [SpecialClassSessions] ([SubjectId]);

CREATE INDEX [IX_TimetableEntries_AcademicTermId] ON [TimetableEntries] ([AcademicTermId]);

CREATE INDEX [IX_TimetableEntries_AcademicYearId_AcademicTermId_SchoolClassId_Day_StartTime] ON [TimetableEntries] ([AcademicYearId], [AcademicTermId], [SchoolClassId], [Day], [StartTime]);

CREATE INDEX [IX_TimetableEntries_CreatedByStaffId] ON [TimetableEntries] ([CreatedByStaffId]);

CREATE INDEX [IX_TimetableEntries_SchoolClassId] ON [TimetableEntries] ([SchoolClassId]);

CREATE INDEX [IX_TimetableEntries_StaffId_Day_StartTime] ON [TimetableEntries] ([StaffId], [Day], [StartTime]);

CREATE INDEX [IX_TimetableEntries_SubjectId] ON [TimetableEntries] ([SubjectId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260919134553_AddTimetableAndSpecialClasses', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Notifications]') AND [c].[name] = N'RecipientStaffId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Notifications] ALTER COLUMN [RecipientStaffId] int NULL;

ALTER TABLE [Notifications] ADD [RecipientStudentId] int NULL;

CREATE INDEX [IX_Notifications_RecipientStudentId_IsRead_CreatedAt] ON [Notifications] ([RecipientStudentId], [IsRead], [CreatedAt]);

ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_Recipient] CHECK (([RecipientStaffId] IS NOT NULL AND [RecipientStudentId] IS NULL) OR ([RecipientStaffId] IS NULL AND [RecipientStudentId] IS NOT NULL));

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Students_RecipientStudentId] FOREIGN KEY ([RecipientStudentId]) REFERENCES [Students] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260920104323_AddStudentNotificationRecipients', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StudentDeviceTokens] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [Token] nvarchar(1000) NOT NULL,
    [Platform] int NOT NULL,
    [DeviceName] nvarchar(200) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [LastUsedAt] datetime2 NULL,
    CONSTRAINT [PK_StudentDeviceTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentDeviceTokens_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_StudentDeviceTokens_StudentId_IsActive] ON [StudentDeviceTokens] ([StudentId], [IsActive]);

CREATE UNIQUE INDEX [IX_StudentDeviceTokens_Token] ON [StudentDeviceTokens] ([Token]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260920122043_AddStudentDeviceTokens', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StudentRegistrationCodes] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [CodeHash] nvarchar(500) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL,
    [UsedAt] datetime2 NULL,
    [FailedAttempts] int NOT NULL,
    [MaxAttempts] int NOT NULL,
    [CreatedByStaffId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_StudentRegistrationCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentRegistrationCodes_Staff_CreatedByStaffId] FOREIGN KEY ([CreatedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StudentRegistrationCodes_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id])
);

CREATE INDEX [IX_StudentRegistrationCodes_CreatedByStaffId] ON [StudentRegistrationCodes] ([CreatedByStaffId]);

CREATE INDEX [IX_StudentRegistrationCodes_ExpiresAt] ON [StudentRegistrationCodes] ([ExpiresAt]);

CREATE INDEX [IX_StudentRegistrationCodes_StudentId_IsActive_IsUsed] ON [StudentRegistrationCodes] ([StudentId], [IsActive], [IsUsed]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260920181650_AddStudentRegistrationCodes', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StaffPasswordResetCodes] (
    [Id] int NOT NULL IDENTITY,
    [StaffId] int NOT NULL,
    [CodeHash] nvarchar(500) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL,
    [UsedAt] datetime2 NULL,
    [FailedAttempts] int NOT NULL,
    [MaxAttempts] int NOT NULL,
    [CreatedByApplicationUserId] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_StaffPasswordResetCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffPasswordResetCodes_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
);

CREATE INDEX [IX_StaffPasswordResetCodes_ExpiresAt] ON [StaffPasswordResetCodes] ([ExpiresAt]);

CREATE INDEX [IX_StaffPasswordResetCodes_StaffId_IsActive_IsUsed] ON [StaffPasswordResetCodes] ([StaffId], [IsActive], [IsUsed]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260921093255_AddStaffPasswordResetCodes', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StudentAcademicEnrollments] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [AcademicYearId] int NOT NULL,
    [SchoolClassId] int NOT NULL,
    [EnrollmentDate] date NOT NULL,
    [IsCurrent] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedByStaffId] int NOT NULL,
    CONSTRAINT [PK_StudentAcademicEnrollments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentAcademicEnrollments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_StudentAcademicEnrollments_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_StudentAcademicEnrollments_Staff_CreatedByStaffId] FOREIGN KEY ([CreatedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StudentAcademicEnrollments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id])
);

CREATE INDEX [IX_StudentAcademicEnrollments_AcademicYearId] ON [StudentAcademicEnrollments] ([AcademicYearId]);

CREATE INDEX [IX_StudentAcademicEnrollments_CreatedByStaffId] ON [StudentAcademicEnrollments] ([CreatedByStaffId]);

CREATE INDEX [IX_StudentAcademicEnrollments_SchoolClassId] ON [StudentAcademicEnrollments] ([SchoolClassId]);

CREATE UNIQUE INDEX [IX_StudentAcademicEnrollments_StudentId_AcademicYearId] ON [StudentAcademicEnrollments] ([StudentId], [AcademicYearId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260921171739_AddStudentAcademicEnrollments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StudentPromotionHistories] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [FromAcademicYearId] int NOT NULL,
    [ToAcademicYearId] int NOT NULL,
    [FromSchoolClassId] int NOT NULL,
    [ToSchoolClassId] int NULL,
    [Action] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NULL,
    [ProcessedByStaffId] int NOT NULL,
    [ProcessedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_StudentPromotionHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentPromotionHistories_AcademicYears_FromAcademicYearId] FOREIGN KEY ([FromAcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_StudentPromotionHistories_AcademicYears_ToAcademicYearId] FOREIGN KEY ([ToAcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_StudentPromotionHistories_SchoolClasses_FromSchoolClassId] FOREIGN KEY ([FromSchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_StudentPromotionHistories_SchoolClasses_ToSchoolClassId] FOREIGN KEY ([ToSchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_StudentPromotionHistories_Staff_ProcessedByStaffId] FOREIGN KEY ([ProcessedByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StudentPromotionHistories_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id])
);

CREATE INDEX [IX_StudentPromotionHistories_FromAcademicYearId] ON [StudentPromotionHistories] ([FromAcademicYearId]);

CREATE INDEX [IX_StudentPromotionHistories_FromSchoolClassId] ON [StudentPromotionHistories] ([FromSchoolClassId]);

CREATE INDEX [IX_StudentPromotionHistories_ProcessedByStaffId] ON [StudentPromotionHistories] ([ProcessedByStaffId]);

CREATE INDEX [IX_StudentPromotionHistories_StudentId] ON [StudentPromotionHistories] ([StudentId]);

CREATE INDEX [IX_StudentPromotionHistories_ToAcademicYearId] ON [StudentPromotionHistories] ([ToAcademicYearId]);

CREATE INDEX [IX_StudentPromotionHistories_ToSchoolClassId] ON [StudentPromotionHistories] ([ToSchoolClassId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260921194645_AddStudentPromotionHistory', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Students] ADD [GraduationAcademicYearId] int NULL;

ALTER TABLE [Students] ADD [GraduationDate] date NULL;

ALTER TABLE [Students] ADD [IsGraduated] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE INDEX [IX_Students_GraduationAcademicYearId] ON [Students] ([GraduationAcademicYearId]);

ALTER TABLE [Students] ADD CONSTRAINT [FK_Students_AcademicYears_GraduationAcademicYearId] FOREIGN KEY ([GraduationAcademicYearId]) REFERENCES [AcademicYears] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260921200621_AddStudentGraduationFields', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [StudentSubjectEnrollments] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [AcademicYearId] int NOT NULL,
    [SubjectId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [EnrolledAt] datetime2 NOT NULL,
    [EnrolledByStaffId] int NOT NULL,
    CONSTRAINT [PK_StudentSubjectEnrollments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentSubjectEnrollments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
    CONSTRAINT [FK_StudentSubjectEnrollments_Staff_EnrolledByStaffId] FOREIGN KEY ([EnrolledByStaffId]) REFERENCES [Staff] ([Id]),
    CONSTRAINT [FK_StudentSubjectEnrollments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]),
    CONSTRAINT [FK_StudentSubjectEnrollments_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([Id])
);

CREATE INDEX [IX_StudentSubjectEnrollments_AcademicYearId] ON [StudentSubjectEnrollments] ([AcademicYearId]);

CREATE INDEX [IX_StudentSubjectEnrollments_EnrolledByStaffId] ON [StudentSubjectEnrollments] ([EnrolledByStaffId]);

CREATE UNIQUE INDEX [IX_StudentSubjectEnrollments_StudentId_AcademicYearId_SubjectId] ON [StudentSubjectEnrollments] ([StudentId], [AcademicYearId], [SubjectId]);

CREATE INDEX [IX_StudentSubjectEnrollments_SubjectId] ON [StudentSubjectEnrollments] ([SubjectId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260921201922_AddStudentSubjectEnrollments', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [SchoolAnnouncements] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(2000) NOT NULL,
    [AudienceType] nvarchar(50) NOT NULL,
    [SectionId] int NULL,
    [GradeId] int NULL,
    [SchoolClassId] int NULL,
    [RoleName] nvarchar(100) NULL,
    [PublishAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [CreatedByStaffId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SchoolAnnouncements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SchoolAnnouncements_Grades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [Grades] ([Id]),
    CONSTRAINT [FK_SchoolAnnouncements_SchoolClasses_SchoolClassId] FOREIGN KEY ([SchoolClassId]) REFERENCES [SchoolClasses] ([Id]),
    CONSTRAINT [FK_SchoolAnnouncements_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]),
    CONSTRAINT [FK_SchoolAnnouncements_Staff_CreatedByStaffId] FOREIGN KEY ([CreatedByStaffId]) REFERENCES [Staff] ([Id])
);

CREATE INDEX [IX_SchoolAnnouncements_CreatedByStaffId] ON [SchoolAnnouncements] ([CreatedByStaffId]);

CREATE INDEX [IX_SchoolAnnouncements_GradeId] ON [SchoolAnnouncements] ([GradeId]);

CREATE INDEX [IX_SchoolAnnouncements_SchoolClassId] ON [SchoolAnnouncements] ([SchoolClassId]);

CREATE INDEX [IX_SchoolAnnouncements_SectionId] ON [SchoolAnnouncements] ([SectionId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922105612_AddSchoolAnnouncements', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ParentGuardians] (
    [Id] int NOT NULL IDENTITY,
    [ParentNumber] nvarchar(50) NOT NULL,
    [FullName] nvarchar(200) NOT NULL,
    [Email] nvarchar(256) NULL,
    [PhoneNumber] nvarchar(50) NULL,
    [ApplicationUserId] nvarchar(450) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ParentGuardians] PRIMARY KEY ([Id])
);

CREATE TABLE [StudentParentGuardians] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] int NOT NULL,
    [ParentGuardianId] int NOT NULL,
    [Relationship] nvarchar(50) NOT NULL,
    [IsPrimaryGuardian] bit NOT NULL,
    [IsEmergencyContact] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_StudentParentGuardians] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StudentParentGuardians_ParentGuardians_ParentGuardianId] FOREIGN KEY ([ParentGuardianId]) REFERENCES [ParentGuardians] ([Id]),
    CONSTRAINT [FK_StudentParentGuardians_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id])
);

CREATE UNIQUE INDEX [IX_ParentGuardians_ApplicationUserId] ON [ParentGuardians] ([ApplicationUserId]) WHERE [ApplicationUserId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_ParentGuardians_ParentNumber] ON [ParentGuardians] ([ParentNumber]);

CREATE INDEX [IX_StudentParentGuardians_ParentGuardianId] ON [StudentParentGuardians] ([ParentGuardianId]);

CREATE UNIQUE INDEX [IX_StudentParentGuardians_StudentId_ParentGuardianId] ON [StudentParentGuardians] ([StudentId], [ParentGuardianId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922140129_AddParentGuardians', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Notifications] ADD [RecipientParentGuardianId] int NULL;

CREATE TABLE [ParentDeviceTokens] (
    [Id] int NOT NULL IDENTITY,
    [ParentGuardianId] int NOT NULL,
    [Token] nvarchar(1000) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [LastUsedAt] datetime2 NULL,
    CONSTRAINT [PK_ParentDeviceTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ParentDeviceTokens_ParentGuardians_ParentGuardianId] FOREIGN KEY ([ParentGuardianId]) REFERENCES [ParentGuardians] ([Id])
);

CREATE INDEX [IX_Notifications_RecipientParentGuardianId] ON [Notifications] ([RecipientParentGuardianId]);

CREATE INDEX [IX_ParentDeviceTokens_ParentGuardianId_IsActive] ON [ParentDeviceTokens] ([ParentGuardianId], [IsActive]);

CREATE UNIQUE INDEX [IX_ParentDeviceTokens_Token] ON [ParentDeviceTokens] ([Token]);

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_ParentGuardians_RecipientParentGuardianId] FOREIGN KEY ([RecipientParentGuardianId]) REFERENCES [ParentGuardians] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922161838_AddParentNotificationsAndDeviceTokens', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Notifications] DROP CONSTRAINT [CK_Notifications_Recipient];

ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_Recipient] CHECK ((
            ([RecipientStaffId] IS NOT NULL
                AND [RecipientStudentId] IS NULL
                AND [RecipientParentGuardianId] IS NULL)

            OR

            ([RecipientStaffId] IS NULL
                AND [RecipientStudentId] IS NOT NULL
                AND [RecipientParentGuardianId] IS NULL)

            OR

            ([RecipientStaffId] IS NULL
                AND [RecipientStudentId] IS NULL
                AND [RecipientParentGuardianId] IS NOT NULL)
        ));

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260922170636_UpdateNotificationRecipientConstraint', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AuditLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NULL,
    [StaffId] int NULL,
    [StudentId] int NULL,
    [ParentGuardianId] int NULL,
    [Action] nvarchar(100) NOT NULL,
    [EntityName] nvarchar(150) NOT NULL,
    [EntityId] nvarchar(100) NULL,
    [Description] nvarchar(1000) NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);

CREATE INDEX [IX_AuditLogs_EntityName] ON [AuditLogs] ([EntityName]);

CREATE INDEX [IX_AuditLogs_ParentGuardianId] ON [AuditLogs] ([ParentGuardianId]);

CREATE INDEX [IX_AuditLogs_StaffId] ON [AuditLogs] ([StaffId]);

CREATE INDEX [IX_AuditLogs_StudentId] ON [AuditLogs] ([StudentId]);

CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260923064446_AddAuditLogs', N'10.0.12');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Students] ADD [Email] nvarchar(max) NULL;

ALTER TABLE [Students] ADD [Mobile] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260924204356_AddStudentContactInformation', N'10.0.12');

COMMIT;
GO

