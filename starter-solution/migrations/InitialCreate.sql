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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [AvatarUrl] nvarchar(500) NULL,
        [FriendsCount] int NOT NULL DEFAULT 0,
        [ActivityMinutesThisWeek] int NOT NULL DEFAULT 0,
        [LastActiveAtUtc] datetime2 NULL,
        [DefaultCourseId] uniqueidentifier NULL,
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(50) NOT NULL,
        [ColorHex] nvarchar(7) NOT NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [Enrollments] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [EnrolledAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Enrollments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Rating] float(2) NOT NULL,
        [Comment] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] uniqueidentifier NOT NULL,
        [Slug] nvarchar(80) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [InstructorId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [AverageRating] float(2) NOT NULL DEFAULT 0.0E0,
        [ReviewCount] int NOT NULL DEFAULT 0,
        [EnrollmentCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Courses_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseProgress] (
        [Id] uniqueidentifier NOT NULL,
        [EnrollmentId] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NOT NULL,
        [MinutesWatched] int NOT NULL DEFAULT 0,
        [IsCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] varbinary(max) NOT NULL,
        CONSTRAINT [PK_CourseProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseProgress_Enrollments_EnrollmentId] FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE TABLE [Lessons] (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Order] int NOT NULL,
        [ContentMarkdown] nvarchar(max) NOT NULL,
        [EstimatedMinutes] int NOT NULL DEFAULT 0,
        [IsPreview] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Lessons] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Lessons_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_FullName] ON [AspNetUsers] ([FullName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Categories_Slug] ON [Categories] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_CourseProgress_Enrollment_Lesson] ON [CourseProgress] ([EnrollmentId], [LessonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_CategoryId_Status] ON [Courses] ([CategoryId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_InstructorId] ON [Courses] ([InstructorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Courses_Popular] ON [Courses] ([EnrollmentCount]) WHERE [Status] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_CourseId_Status] ON [Enrollments] ([CourseId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_StudentId_Status] ON [Enrollments] ([StudentId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UQ_Enrollments_Active_Course_Student] ON [Enrollments] ([CourseId], [StudentId]) WHERE [Status] IN (1, 2)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Lessons_CourseId_Order] ON [Lessons] ([CourseId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_CourseId_CreatedAt_Desc] ON [Reviews] ([CourseId], [CreatedAt] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Reviews_CourseId_StudentId] ON [Reviews] ([CourseId], [StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261121121404_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261121121404_InitialCreate', N'8.0.2');
END;
GO

COMMIT;
GO

