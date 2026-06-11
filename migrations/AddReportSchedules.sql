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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppApiKeys] (
        [ApiKeyId] int NOT NULL IDENTITY,
        [DisplayName] nvarchar(128) NOT NULL,
        [AppKey] nvarchar(64) NOT NULL,
        [SecretHash] nvarchar(128) NOT NULL,
        [Scopes] nvarchar(1024) NULL,
        [AllowedIps] nvarchar(1024) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [LastUsedAtUtc] datetime2 NULL,
        [LastUsedIp] nvarchar(45) NULL,
        [UsageCount] bigint NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_AppApiKeys] PRIMARY KEY ([ApiKeyId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppPermissions] (
        [PermissionId] int NOT NULL IDENTITY,
        [PermissionCode] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NULL,
        [Description] nvarchar(500) NULL,
        CONSTRAINT [PK_AppPermissions] PRIMARY KEY ([PermissionId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppRoles] (
        [RoleId] int NOT NULL IDENTITY,
        [RoleCode] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsSystem] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AppRoles] PRIMARY KEY ([RoleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenAuditEntries] (
        [AuditID] bigint NOT NULL IDENTITY,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NULL,
        [EntityId] nvarchar(50) NULL,
        [PerformedBy] nvarchar(100) NULL,
        [PerformedByRole] nvarchar(50) NULL,
        [PayloadJson] nvarchar(4000) NULL,
        [IpAddress] nvarchar(45) NULL,
        [CorrelationId] nvarchar(64) NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenAuditEntries] PRIMARY KEY ([AuditID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenFoodItems] (
        [FoodItemID] int NOT NULL IDENTITY,
        [ItemName] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Price] decimal(10,2) NOT NULL,
        [Category] int NULL,
        [ImageUrl] nvarchar(500) NULL,
        [IsAvailable] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenFoodItems] PRIMARY KEY ([FoodItemID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenNfcCards] (
        [CardId] int NOT NULL IDENTITY,
        [CardUid] nvarchar(64) NOT NULL,
        [UserId] nvarchar(15) NOT NULL,
        [UserType] int NOT NULL,
        [Status] int NOT NULL,
        [IssuedBy] nvarchar(50) NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [ActivatedAtUtc] datetime2 NULL,
        [BlockedAtUtc] datetime2 NULL,
        [RetiredAtUtc] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenNfcCards] PRIMARY KEY ([CardId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenNotificationLog] (
        [NotificationId] bigint NOT NULL IDENTITY,
        [TemplateKey] nvarchar(50) NOT NULL,
        [Channel] int NOT NULL,
        [Status] int NOT NULL,
        [Recipient] nvarchar(200) NULL,
        [Subject] nvarchar(200) NULL,
        [Body] nvarchar(4000) NULL,
        [ProviderId] nvarchar(100) NULL,
        [FailureReason] nvarchar(500) NULL,
        [AttemptCount] int NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [DeliveredAtUtc] datetime2 NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenNotificationLog] PRIMARY KEY ([NotificationId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenPaymentGatewayConfigs] (
        [GatewayConfigId] int NOT NULL IDENTITY,
        [GatewayCode] nvarchar(32) NOT NULL,
        [Name] nvarchar(64) NOT NULL,
        [SubTitle] nvarchar(128) NULL,
        [Method] int NOT NULL,
        [GatewayGroup] nvarchar(16) NOT NULL,
        [LogoUrl] nvarchar(256) NULL,
        [SortOrder] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [IsSandbox] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [Currency] nvarchar(8) NOT NULL,
        [LiveBaseUrl] nvarchar(512) NULL,
        [LiveUsername] nvarchar(128) NULL,
        [LivePassword] nvarchar(256) NULL,
        [LiveAppKey] nvarchar(256) NULL,
        [LiveAppSecret] nvarchar(512) NULL,
        [LiveMerchantId] nvarchar(128) NULL,
        [LiveMerchantNumber] nvarchar(128) NULL,
        [LivePublicKey] nvarchar(max) NULL,
        [LivePrivateKey] nvarchar(max) NULL,
        [LiveCallbackUrl] nvarchar(512) NULL,
        [LiveWebhookUrl] nvarchar(512) NULL,
        [LiveIpnUrl] nvarchar(512) NULL,
        [LiveFailCallbackUrl] nvarchar(512) NULL,
        [SandboxBaseUrl] nvarchar(512) NULL,
        [SandboxUsername] nvarchar(128) NULL,
        [SandboxPassword] nvarchar(256) NULL,
        [SandboxAppKey] nvarchar(256) NULL,
        [SandboxAppSecret] nvarchar(512) NULL,
        [SandboxMerchantId] nvarchar(128) NULL,
        [SandboxMerchantNumber] nvarchar(128) NULL,
        [SandboxPublicKey] nvarchar(max) NULL,
        [SandboxPrivateKey] nvarchar(max) NULL,
        [SandboxCallbackUrl] nvarchar(512) NULL,
        [SandboxWebhookUrl] nvarchar(512) NULL,
        [SandboxIpnUrl] nvarchar(512) NULL,
        [SandboxFailCallbackUrl] nvarchar(512) NULL,
        [LastTestStatus] tinyint NULL,
        [LastTestedAtUtc] datetime2 NULL,
        [LastTestMessage] nvarchar(512) NULL,
        [NotifyOnSuccess] bit NOT NULL,
        [NotifyOnFailure] bit NOT NULL,
        [NotifyOnRefund] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenPaymentGatewayConfigs] PRIMARY KEY ([GatewayConfigId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenPaymentTransactions] (
        [PaymentId] bigint NOT NULL IDENTITY,
        [TransactionRef] nvarchar(64) NOT NULL,
        [UserId] nvarchar(15) NOT NULL,
        [UserType] nvarchar(max) NOT NULL,
        [Amount] decimal(10,2) NOT NULL,
        [Currency] nvarchar(8) NOT NULL,
        [Method] int NOT NULL,
        [ChannelCode] nvarchar(64) NULL,
        [GatewayConfigId] int NULL,
        [Status] int NOT NULL,
        [GatewayPaymentId] nvarchar(128) NULL,
        [GatewayMessage] nvarchar(256) NULL,
        [CallbackPayload] nvarchar(4000) NULL,
        [RedirectUrl] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [InitiatedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenPaymentTransactions] PRIMARY KEY ([PaymentId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenTenantSettings] (
        [SettingId] bigint NOT NULL IDENTITY,
        [Key] nvarchar(200) NOT NULL,
        [Value] nvarchar(4000) NULL,
        [Description] nvarchar(500) NULL,
        [IsSecret] bit NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenTenantSettings] PRIMARY KEY ([SettingId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenUserBalances] (
        [BalanceID] int NOT NULL IDENTITY,
        [UserId] nvarchar(15) NOT NULL,
        [UserType] int NOT NULL,
        [TotalBalance] decimal(10,2) NOT NULL,
        [UsedBalance] decimal(10,2) NOT NULL,
        [BlockedAmount] decimal(10,2) NOT NULL,
        [EmergencyEntitlement] decimal(10,2) NOT NULL,
        [EmergencyUsed] decimal(10,2) NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenUserBalances] PRIMARY KEY ([BalanceID]),
        CONSTRAINT [AK_CanteenUserBalances_UserId_UserType] UNIQUE ([UserId], [UserType])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenWalletLedger] (
        [LedgerID] bigint NOT NULL IDENTITY,
        [BalanceID] int NOT NULL,
        [UserId] nvarchar(15) NOT NULL,
        [EntryType] int NOT NULL,
        [Amount] decimal(10,2) NOT NULL,
        [BalanceAfter] decimal(10,2) NOT NULL,
        [BlockedAfter] decimal(10,2) NOT NULL,
        [OrderID] int NULL,
        [Reason] nvarchar(500) NULL,
        [IdempotencyKey] nvarchar(64) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenWalletLedger] PRIMARY KEY ([LedgerID])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [Clients] (
        [ClientId] int NOT NULL IDENTITY,
        [ClientCode] nvarchar(50) NOT NULL,
        [ClientName] nvarchar(200) NOT NULL,
        [ShortName] nvarchar(100) NULL,
        [Address] nvarchar(500) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [Email] nvarchar(200) NULL,
        [WebsiteUrl] nvarchar(200) NULL,
        [LogoUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([ClientId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [DirectorySyncRuns] (
        [SyncRunId] bigint NOT NULL IDENTITY,
        [StartedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [Status] nvarchar(20) NOT NULL,
        [Source] nvarchar(20) NOT NULL,
        [StudentsAdded] int NOT NULL,
        [StudentsUpdated] int NOT NULL,
        [StudentsDisabled] int NOT NULL,
        [EmployeesAdded] int NOT NULL,
        [EmployeesUpdated] int NOT NULL,
        [EmployeesDisabled] int NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [HighWatermarkUtc] datetime2 NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_DirectorySyncRuns] PRIMARY KEY ([SyncRunId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [Employees] (
        [EmployeeId] uniqueidentifier NOT NULL,
        [ExternalId] nvarchar(50) NOT NULL,
        [CardIdentifier] nvarchar(50) NULL,
        [Name] nvarchar(200) NOT NULL,
        [Gender] nvarchar(20) NULL,
        [ContactNo] nvarchar(30) NULL,
        [PhotoPath] nvarchar(500) NULL,
        [Designation] nvarchar(100) NULL,
        [EmployeeType] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [SyncedAtUtc] datetime2 NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([EmployeeId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [Students] (
        [StudentId] uniqueidentifier NOT NULL,
        [ExternalId] nvarchar(50) NOT NULL,
        [CardIdentifier] nvarchar(50) NULL,
        [Name] nvarchar(200) NOT NULL,
        [Gender] nvarchar(20) NULL,
        [ContactNo] nvarchar(30) NULL,
        [PhotoPath] nvarchar(500) NULL,
        [Program] nvarchar(100) NULL,
        [Class] nvarchar(50) NULL,
        [Section] nvarchar(50) NULL,
        [Session] nvarchar(50) NULL,
        [Version] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [SyncedAtUtc] datetime2 NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_Students] PRIMARY KEY ([StudentId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppRolePermissions] (
        [RolePermissionId] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        CONSTRAINT [PK_AppRolePermissions] PRIMARY KEY ([RolePermissionId]),
        CONSTRAINT [FK_AppRolePermissions_AppPermissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [AppPermissions] ([PermissionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_AppRolePermissions_AppRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([RoleId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenDailyMenus] (
        [DailyMenuID] int NOT NULL IDENTITY,
        [FoodItemID] int NOT NULL,
        [MenuDate] datetime2 NOT NULL,
        [MealType] int NULL,
        [IsAvailable] bit NOT NULL,
        [AvailableQuantity] int NOT NULL,
        [InitialQuantity] int NOT NULL,
        [ReservedQuantity] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenDailyMenus] PRIMARY KEY ([DailyMenuID]),
        CONSTRAINT [FK_CanteenDailyMenus_CanteenFoodItems_FoodItemID] FOREIGN KEY ([FoodItemID]) REFERENCES [CanteenFoodItems] ([FoodItemID]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenWeeklyMenuTemplates] (
        [TemplateID] int NOT NULL IDENTITY,
        [TemplateName] nvarchar(200) NOT NULL,
        [DayOfWeek] int NOT NULL,
        [FoodItemID] int NOT NULL,
        [MealType] int NULL,
        [DefaultQuantity] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenWeeklyMenuTemplates] PRIMARY KEY ([TemplateID]),
        CONSTRAINT [FK_CanteenWeeklyMenuTemplates_CanteenFoodItems_FoodItemID] FOREIGN KEY ([FoodItemID]) REFERENCES [CanteenFoodItems] ([FoodItemID]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenCardEvents] (
        [CardEventId] bigint NOT NULL IDENTITY,
        [CardId] int NOT NULL,
        [EventType] int NOT NULL,
        [PreviousCardUid] nvarchar(64) NULL,
        [NewCardUid] nvarchar(64) NULL,
        [PerformedBy] nvarchar(100) NULL,
        [Reason] nvarchar(500) NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenCardEvents] PRIMARY KEY ([CardEventId]),
        CONSTRAINT [FK_CanteenCardEvents_CanteenNfcCards_CardId] FOREIGN KEY ([CardId]) REFERENCES [CanteenNfcCards] ([CardId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenGatewayChannels] (
        [ChannelId] int NOT NULL IDENTITY,
        [GatewayConfigId] int NOT NULL,
        [ChannelCode] nvarchar(64) NOT NULL,
        [ChannelName] nvarchar(128) NOT NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [ChargeType] nvarchar(1) NOT NULL,
        [TotalCharge] decimal(10,4) NOT NULL,
        [MinChargeAmount] decimal(10,2) NOT NULL,
        [MaxChargeAmount] decimal(10,2) NOT NULL,
        [VatTaxPercent] decimal(10,4) NOT NULL,
        [OurMarkup] decimal(10,4) NOT NULL,
        [LogoUrl] nvarchar(256) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenGatewayChannels] PRIMARY KEY ([ChannelId]),
        CONSTRAINT [FK_CanteenGatewayChannels_CanteenPaymentGatewayConfigs_GatewayConfigId] FOREIGN KEY ([GatewayConfigId]) REFERENCES [CanteenPaymentGatewayConfigs] ([GatewayConfigId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenOrders] (
        [OrderID] int NOT NULL IDENTITY,
        [OrderNumber] nvarchar(50) NOT NULL,
        [UserId] nvarchar(15) NOT NULL,
        [UserType] int NOT NULL,
        [TotalAmount] decimal(10,2) NOT NULL,
        [Status] int NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [DeliveredDate] datetime2 NULL,
        [InputSequence] nvarchar(100) NULL,
        [IdempotencyKey] nvarchar(64) NULL,
        [RowVersion] rowversion NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenOrders] PRIMARY KEY ([OrderID]),
        CONSTRAINT [FK_CanteenOrders_CanteenUserBalances_UserId_UserType] FOREIGN KEY ([UserId], [UserType]) REFERENCES [CanteenUserBalances] ([UserId], [UserType]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppUsers] (
        [UserId] int NOT NULL IDENTITY,
        [ClientId] int NULL,
        [UserName] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Email] nvarchar(256) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [LinkedPersonId] nvarchar(32) NULL,
        [UserKind] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockedOutUntilUtc] datetime2 NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [LastLoginIp] nvarchar(45) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_AppUsers] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_AppUsers_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([ClientId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [CanteenOrderItems] (
        [OrderItemID] int NOT NULL IDENTITY,
        [OrderID] int NOT NULL,
        [FoodItemID] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(10,2) NOT NULL,
        [TotalPrice] decimal(10,2) NOT NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_CanteenOrderItems] PRIMARY KEY ([OrderItemID]),
        CONSTRAINT [FK_CanteenOrderItems_CanteenFoodItems_FoodItemID] FOREIGN KEY ([FoodItemID]) REFERENCES [CanteenFoodItems] ([FoodItemID]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CanteenOrderItems_CanteenOrders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [CanteenOrders] ([OrderID]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppRefreshTokens] (
        [RefreshTokenId] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [CreatedIp] nvarchar(45) NULL,
        [RevokedIp] nvarchar(45) NULL,
        [ReplacedByToken] nvarchar(500) NULL,
        [RevocationReason] nvarchar(200) NULL,
        CONSTRAINT [PK_AppRefreshTokens] PRIMARY KEY ([RefreshTokenId]),
        CONSTRAINT [FK_AppRefreshTokens_AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([UserId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE TABLE [AppUserRoles] (
        [UserRoleId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        [AssignedAtUtc] datetime2 NOT NULL,
        [AssignedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_AppUserRoles] PRIMARY KEY ([UserRoleId]),
        CONSTRAINT [FK_AppUserRoles_AppRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([RoleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_AppUserRoles_AppUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AppUsers] ([UserId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppApiKeys_AppKey] ON [AppApiKeys] ([AppKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_AppApiKeys_ClientId] ON [AppApiKeys] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppPermissions_PermissionCode] ON [AppPermissions] ([PermissionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppRefreshTokens_TokenHash] ON [AppRefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_AppRefreshTokens_UserId] ON [AppRefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_AppRolePermissions_PermissionId] ON [AppRolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppRolePermissions_RoleId_PermissionId] ON [AppRolePermissions] ([RoleId], [PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_AppRoles_RoleCode] ON [AppRoles] ([RoleCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_AppUserRoles_RoleId] ON [AppUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUserRoles_UserId_RoleId] ON [AppUserRoles] ([UserId], [RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_AppUsers_ClientId] ON [AppUsers] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUsers_UserName] ON [AppUsers] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenAuditEntries_ClientId] ON [CanteenAuditEntries] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenAuditEntries_EntityType] ON [CanteenAuditEntries] ([EntityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenAuditEntries_OccurredAtUtc] ON [CanteenAuditEntries] ([OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenAuditEntries_PerformedBy] ON [CanteenAuditEntries] ([PerformedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenCardEvents_CardId] ON [CanteenCardEvents] ([CardId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenCardEvents_ClientId] ON [CanteenCardEvents] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenDailyMenus_ClientId] ON [CanteenDailyMenus] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenDailyMenus_FoodItemID] ON [CanteenDailyMenus] ([FoodItemID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenDailyMenus_MenuDate] ON [CanteenDailyMenus] ([MenuDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenFoodItems_ClientId] ON [CanteenFoodItems] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenGatewayChannels_ClientId] ON [CanteenGatewayChannels] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenGatewayChannels_GatewayConfigId] ON [CanteenGatewayChannels] ([GatewayConfigId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CanteenGatewayChannels_GatewayConfigId_ChannelCode] ON [CanteenGatewayChannels] ([GatewayConfigId], [ChannelCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenNfcCards_CardUid] ON [CanteenNfcCards] ([CardUid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenNfcCards_ClientId] ON [CanteenNfcCards] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenNfcCards_UserId_UserType] ON [CanteenNfcCards] ([UserId], [UserType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenNotificationLog_Channel_Status] ON [CanteenNotificationLog] ([Channel], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenNotificationLog_ClientId] ON [CanteenNotificationLog] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenNotificationLog_OccurredAtUtc] ON [CanteenNotificationLog] ([OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrderItems_ClientId] ON [CanteenOrderItems] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrderItems_FoodItemID] ON [CanteenOrderItems] ([FoodItemID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrderItems_OrderID] ON [CanteenOrderItems] ([OrderID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrders_ClientId] ON [CanteenOrders] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrders_IdempotencyKey] ON [CanteenOrders] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrders_OrderDate_Status] ON [CanteenOrders] ([OrderDate], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CanteenOrders_OrderNumber] ON [CanteenOrders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenOrders_UserId_UserType] ON [CanteenOrders] ([UserId], [UserType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenPaymentGatewayConfigs_ClientId] ON [CanteenPaymentGatewayConfigs] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenPaymentGatewayConfigs_GatewayCode] ON [CanteenPaymentGatewayConfigs] ([GatewayCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenPaymentGatewayConfigs_Method] ON [CanteenPaymentGatewayConfigs] ([Method]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenPaymentTransactions_ClientId] ON [CanteenPaymentTransactions] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenPaymentTransactions_Status] ON [CanteenPaymentTransactions] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CanteenPaymentTransactions_TransactionRef] ON [CanteenPaymentTransactions] ([TransactionRef]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenPaymentTransactions_UserId] ON [CanteenPaymentTransactions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenTenantSettings_ClientId] ON [CanteenTenantSettings] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenTenantSettings_Key] ON [CanteenTenantSettings] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenUserBalances_ClientId] ON [CanteenUserBalances] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CanteenUserBalances_UserId_UserType] ON [CanteenUserBalances] ([UserId], [UserType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenWalletLedger_ClientId] ON [CanteenWalletLedger] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenWalletLedger_IdempotencyKey] ON [CanteenWalletLedger] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenWalletLedger_OrderID] ON [CanteenWalletLedger] ([OrderID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenWalletLedger_UserId] ON [CanteenWalletLedger] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenWeeklyMenuTemplates_ClientId] ON [CanteenWeeklyMenuTemplates] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_CanteenWeeklyMenuTemplates_FoodItemID] ON [CanteenWeeklyMenuTemplates] ([FoodItemID]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Clients_ClientCode] ON [Clients] ([ClientCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_DirectorySyncRuns_ClientId] ON [DirectorySyncRuns] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_DirectorySyncRuns_StartedAtUtc] ON [DirectorySyncRuns] ([StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_DirectorySyncRuns_Status] ON [DirectorySyncRuns] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_Employees_CardIdentifier] ON [Employees] ([CardIdentifier]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_Employees_ClientId] ON [Employees] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Employees_ClientId_ExternalId] ON [Employees] ([ClientId], [ExternalId]) WHERE [ClientId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_Employees_ExternalId] ON [Employees] ([ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_Students_CardIdentifier] ON [Students] ([CardIdentifier]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_Students_ClientId] ON [Students] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Students_ClientId_ExternalId] ON [Students] ([ClientId], [ExternalId]) WHERE [ClientId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    CREATE INDEX [IX_Students_ExternalId] ON [Students] ([ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603070515_InitialCanteenSaasSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603070515_InitialCanteenSaasSchema', N'10.0.8');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603114357_AddReportSchedules'
)
BEGIN
    CREATE TABLE [ReportSchedules] (
        [ScheduleId] int NOT NULL IDENTITY,
        [ReportKey] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Recurrence] nvarchar(16) NOT NULL,
        [HourOfDay] int NULL,
        [Minute] int NULL,
        [DayOfWeek] int NULL,
        [DayOfMonth] int NULL,
        [IntervalHours] int NULL,
        [Format] nvarchar(16) NOT NULL,
        [ParametersJson] nvarchar(2000) NULL,
        [Recipients] nvarchar(1000) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [LastRunAtUtc] datetime2 NULL,
        [NextRunAtUtc] datetime2 NULL,
        [LastRunStatus] nvarchar(16) NULL,
        [LastError] nvarchar(2000) NULL,
        [CreatedBy] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [ClientId] int NULL,
        CONSTRAINT [PK_ReportSchedules] PRIMARY KEY ([ScheduleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603114357_AddReportSchedules'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_ClientId] ON [ReportSchedules] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603114357_AddReportSchedules'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_NextRunAtUtc] ON [ReportSchedules] ([NextRunAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603114357_AddReportSchedules'
)
BEGIN
    CREATE INDEX [IX_ReportSchedules_ReportKey] ON [ReportSchedules] ([ReportKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603114357_AddReportSchedules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603114357_AddReportSchedules', N'10.0.8');
END;

COMMIT;
GO

