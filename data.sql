CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [TCKN] nvarchar(max) NULL,
    [BirthDate] datetime2 NULL,
    [Height] float NULL,
    [Weight] float NULL,
    [Level] int NOT NULL,
    [DominantHand] int NOT NULL,
    [BackhandStyle] int NOT NULL,
    [EmergencyContactName] nvarchar(max) NULL,
    [EmergencyContactPhone] nvarchar(max) NULL,
    [MedicalNotes] nvarchar(max) NULL,
    [ProfileImageUrl] nvarchar(max) NULL,
    [LessonCredits] int NOT NULL,
    [GroupCredits] int NOT NULL,
    [RegistrationDate] datetime2 NOT NULL,
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
GO


CREATE TABLE [LessonPackets] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [CreditAmount] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Type] int NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_LessonPackets] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Tournaments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Category] nvarchar(max) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_Tournaments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Coaches] (
    [Id] int NOT NULL IDENTITY,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [Specialty] nvarchar(max) NOT NULL,
    [Bio] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(max) NOT NULL,
    [AppUserId] nvarchar(450) NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_Coaches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Coaches_AspNetUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Matches] (
    [Id] int NOT NULL IDENTITY,
    [TournamentId] int NOT NULL,
    [Player1Id] nvarchar(450) NOT NULL,
    [Player2Id] nvarchar(450) NULL,
    [Player2ExternalName] nvarchar(max) NOT NULL,
    [ScoreSet1] nvarchar(max) NOT NULL,
    [ScoreSet2] nvarchar(max) NULL,
    [ScoreSet3] nvarchar(max) NULL,
    [WinnerId] nvarchar(max) NULL,
    [MatchDate] datetime2 NOT NULL,
    [RefereeCoachId] int NOT NULL,
    [CoachNotes] nvarchar(max) NULL,
    [AppUserId] nvarchar(450) NULL,
    [AppUserId1] nvarchar(450) NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_Matches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Matches_AspNetUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Matches_AspNetUsers_AppUserId1] FOREIGN KEY ([AppUserId1]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Matches_AspNetUsers_Player1Id] FOREIGN KEY ([Player1Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Matches_AspNetUsers_Player2Id] FOREIGN KEY ([Player2Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Matches_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [CoachUnavailabilities] (
    [Id] int NOT NULL IDENTITY,
    [CoachId] int NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NOT NULL,
    [Reason] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_CoachUnavailabilities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CoachUnavailabilities_Coaches_CoachId] FOREIGN KEY ([CoachId]) REFERENCES [Coaches] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [GroupLessons] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [CoachId] int NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NOT NULL,
    [Capacity] int NOT NULL,
    [RegisteredCount] int NOT NULL,
    [MinLevel] int NOT NULL,
    [CreditCost] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_GroupLessons] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GroupLessons_Coaches_CoachId] FOREIGN KEY ([CoachId]) REFERENCES [Coaches] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [LessonBookings] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] nvarchar(450) NOT NULL,
    [CoachId] int NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_LessonBookings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LessonBookings_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_LessonBookings_Coaches_CoachId] FOREIGN KEY ([CoachId]) REFERENCES [Coaches] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [GroupLessonRegistrations] (
    [Id] int NOT NULL IDENTITY,
    [GroupLessonId] int NOT NULL,
    [StudentId] nvarchar(450) NOT NULL,
    [RegistrationDate] datetime2 NOT NULL,
    [IsAttended] bit NOT NULL,
    [CreatedDate] datetime2 NOT NULL,
    CONSTRAINT [PK_GroupLessonRegistrations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GroupLessonRegistrations_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_GroupLessonRegistrations_GroupLessons_GroupLessonId] FOREIGN KEY ([GroupLessonId]) REFERENCES [GroupLessons] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE INDEX [IX_Coaches_AppUserId] ON [Coaches] ([AppUserId]);
GO


CREATE INDEX [IX_CoachUnavailabilities_CoachId] ON [CoachUnavailabilities] ([CoachId]);
GO


CREATE INDEX [IX_GroupLessonRegistrations_GroupLessonId] ON [GroupLessonRegistrations] ([GroupLessonId]);
GO


CREATE INDEX [IX_GroupLessonRegistrations_StudentId] ON [GroupLessonRegistrations] ([StudentId]);
GO


CREATE INDEX [IX_GroupLessons_CoachId] ON [GroupLessons] ([CoachId]);
GO


CREATE INDEX [IX_LessonBookings_CoachId] ON [LessonBookings] ([CoachId]);
GO


CREATE INDEX [IX_LessonBookings_StudentId] ON [LessonBookings] ([StudentId]);
GO


CREATE INDEX [IX_Matches_AppUserId] ON [Matches] ([AppUserId]);
GO


CREATE INDEX [IX_Matches_AppUserId1] ON [Matches] ([AppUserId1]);
GO


CREATE INDEX [IX_Matches_Player1Id] ON [Matches] ([Player1Id]);
GO


CREATE INDEX [IX_Matches_Player2Id] ON [Matches] ([Player2Id]);
GO


CREATE INDEX [IX_Matches_TournamentId] ON [Matches] ([TournamentId]);
GO


