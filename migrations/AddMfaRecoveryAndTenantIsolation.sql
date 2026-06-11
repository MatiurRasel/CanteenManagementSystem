BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604074718_AddMfaRecoveryAndTenantIsolation'
)
BEGIN
    ALTER TABLE [Clients] ADD [IsolationMode] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604074718_AddMfaRecoveryAndTenantIsolation'
)
BEGIN
    CREATE TABLE [AppMfaRecoveryCodes] (
        [RecoveryCodeId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [Salt] nvarchar(64) NOT NULL,
        [CodePrefix] nvarchar(2) NOT NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [ConsumedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_AppMfaRecoveryCodes] PRIMARY KEY ([RecoveryCodeId]),
        CONSTRAINT [FK_AppMfaRecoveryCodes_AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([UserId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604074718_AddMfaRecoveryAndTenantIsolation'
)
BEGIN
    CREATE INDEX [IX_AppMfaRecoveryCodes_CodeHash] ON [AppMfaRecoveryCodes] ([CodeHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604074718_AddMfaRecoveryAndTenantIsolation'
)
BEGIN
    CREATE INDEX [IX_AppMfaRecoveryCodes_UserId] ON [AppMfaRecoveryCodes] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604074718_AddMfaRecoveryAndTenantIsolation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604074718_AddMfaRecoveryAndTenantIsolation', N'10.0.8');
END;

COMMIT;
GO

