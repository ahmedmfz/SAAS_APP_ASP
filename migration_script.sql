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

CREATE TABLE [Organizations] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [OrganizationSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [PlanId] int NOT NULL,
    [StartAt] datetime2 NOT NULL,
    [EndAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OrganizationSubscriptions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SubscriptionPlans] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ApiCallsPerMonth] int NOT NULL,
    [StorageLimitMb] int NOT NULL,
    CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [Email] nvarchar(450) NOT NULL,
    [Password] nvarchar(max) NOT NULL,
    [Role] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ApiCallsPerMonth', N'Name', N'StorageLimitMb') AND [object_id] = OBJECT_ID(N'[SubscriptionPlans]'))
    SET IDENTITY_INSERT [SubscriptionPlans] ON;
INSERT INTO [SubscriptionPlans] ([Id], [ApiCallsPerMonth], [Name], [StorageLimitMb])
VALUES (1, 10000, N'Basic', 500),
(2, 100000, N'Pro', 5000);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ApiCallsPerMonth', N'Name', N'StorageLimitMb') AND [object_id] = OBJECT_ID(N'[SubscriptionPlans]'))
    SET IDENTITY_INSERT [SubscriptionPlans] OFF;
GO

CREATE INDEX [IX_OrganizationSubscriptions_OrganizationId_PlanId] ON [OrganizationSubscriptions] ([OrganizationId], [PlanId]);
GO

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260303101927_InitialCreate', N'8.0.12');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Name', N'Status') AND [object_id] = OBJECT_ID(N'[Organizations]'))
    SET IDENTITY_INSERT [Organizations] ON;
INSERT INTO [Organizations] ([Id], [CreatedAt], [Name], [Status])
VALUES ('11111111-1111-1111-1111-111111111111', '2025-01-01T00:00:00.0000000Z', N'Acme Corp', 1),
('22222222-2222-2222-2222-222222222222', '2025-01-01T00:00:00.0000000Z', N'Beta Inc', 1);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Name', N'Status') AND [object_id] = OBJECT_ID(N'[Organizations]'))
    SET IDENTITY_INSERT [Organizations] OFF;
GO

CREATE INDEX [IX_Users_OrganizationId] ON [Users] ([OrganizationId]);
GO

ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260303214638_SeedOrganizations', N'8.0.12');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DELETE FROM [Organizations]
WHERE [Id] = '11111111-1111-1111-1111-111111111111';
SELECT @@ROWCOUNT;

GO

DELETE FROM [Organizations]
WHERE [Id] = '22222222-2222-2222-2222-222222222222';
SELECT @@ROWCOUNT;

GO

DELETE FROM [SubscriptionPlans]
WHERE [Id] = 1;
SELECT @@ROWCOUNT;

GO

DELETE FROM [SubscriptionPlans]
WHERE [Id] = 2;
SELECT @@ROWCOUNT;

GO

EXEC sp_rename N'[Users].[PasswordHash]', N'Password', N'COLUMN';
GO

CREATE TABLE [ApiKeys] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Prefix] nvarchar(450) NOT NULL,
    [KeyHash] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NULL,
    [LastUsedAt] datetime2 NULL,
    CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApiKeys_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [OrganizationUsageMonthly] (
    [Id] uniqueidentifier NOT NULL,
    [OrganizationId] uniqueidentifier NOT NULL,
    [YearMonth] int NOT NULL,
    [ApiCallCount] bigint NOT NULL,
    [RowVersion] bigint NOT NULL,
    CONSTRAINT [PK_OrganizationUsageMonthly] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrganizationUsageMonthly_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UsageRecords] (
    [Id] bigint NOT NULL IDENTITY,
    [OrganizationId] uniqueidentifier NOT NULL,
    [ApiKeyId] uniqueidentifier NOT NULL,
    [OccurredAt] datetime2 NOT NULL,
    [Endpoint] nvarchar(max) NOT NULL,
    [StatusCode] int NOT NULL,
    CONSTRAINT [PK_UsageRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsageRecords_ApiKeys_ApiKeyId] FOREIGN KEY ([ApiKeyId]) REFERENCES [ApiKeys] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsageRecords_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ApiKeys_OrganizationId] ON [ApiKeys] ([OrganizationId]);
GO

CREATE INDEX [IX_ApiKeys_Prefix] ON [ApiKeys] ([Prefix]);
GO

CREATE UNIQUE INDEX [IX_OrganizationUsageMonthly_OrganizationId_YearMonth] ON [OrganizationUsageMonthly] ([OrganizationId], [YearMonth]);
GO

CREATE INDEX [IX_UsageRecords_ApiKeyId] ON [UsageRecords] ([ApiKeyId]);
GO

CREATE INDEX [IX_UsageRecords_OrganizationId_OccurredAt] ON [UsageRecords] ([OrganizationId], [OccurredAt]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260303225424_AddUsageAndApiKeyEntities', N'8.0.12');
GO

COMMIT;
GO

