BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604093326_RemoveLegacyBridgeEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604093326_RemoveLegacyBridgeEntities', N'10.0.8');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604104113_AddKioskDevices'
)
BEGIN
    CREATE TABLE [AppKioskDevices] (
        [KioskDeviceId] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [Salt] nvarchar(64) NOT NULL,
        [TokenPrefix] nvarchar(8) NOT NULL,
        [IsActive] bit NOT NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [RedeemedAtUtc] datetime2 NULL,
        [LastSeenAtUtc] datetime2 NULL,
        [RevokedAtUtc] datetime2 NULL,
        [IssuedByUserId] int NULL,
        [LastSeenIp] nvarchar(45) NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_AppKioskDevices] PRIMARY KEY ([KioskDeviceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604104113_AddKioskDevices'
)
BEGIN
    CREATE INDEX [IX_AppKioskDevices_ClientId] ON [AppKioskDevices] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604104113_AddKioskDevices'
)
BEGIN
    CREATE INDEX [IX_AppKioskDevices_IsActive] ON [AppKioskDevices] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604104113_AddKioskDevices'
)
BEGIN
    CREATE INDEX [IX_AppKioskDevices_TokenHash] ON [AppKioskDevices] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604104113_AddKioskDevices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604104113_AddKioskDevices', N'10.0.8');
END;

COMMIT;
GO

