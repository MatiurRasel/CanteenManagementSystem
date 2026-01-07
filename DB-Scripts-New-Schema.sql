/*
============================================
DYNAMIC E-CANTEEN SYSTEM - COMPLETE DATABASE SCHEMA
Multi-Tenant Architecture with Offline Support
============================================
*/
Create Database ECanteenDB;
GO

USE [ECanteenDB]; -- Update with your database name
GO

PRINT '============================================';
PRINT 'Starting Database Migration...';
PRINT '============================================';
GO

-- ============================================
-- 1. CANTEEN CLIENTS TABLE (Multi-tenant)
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenClients]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenClients]
    (
        [ClientId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientName] NVARCHAR(255) NOT NULL,
        [ClientCode] NVARCHAR(20) NOT NULL,
        [ClientShortName] NVARCHAR(255) NOT NULL,
        [ClientType] NVARCHAR(20) NOT NULL DEFAULT 'EDUCATIONAL',
        [Subdomain] NVARCHAR(100) UNIQUE NOT NULL,

        -- Branding
        [LogoUrl] NVARCHAR(MAX),
        [PrimaryColor] NVARCHAR(7) DEFAULT '#FF5722',
        [SecondaryColor] NVARCHAR(7) DEFAULT '#FFC107',
        [AppName] NVARCHAR(100) DEFAULT 'E-Canteen',

        -- External API configuration (student/employee master)
        [ApiBaseUrl] NVARCHAR(255) NULL,
        -- e.g. http://localhost:36524
        [ApiMethodPath] NVARCHAR(255) NULL,
        -- e.g. /getStudentOrEmployee
        [ApiAuthMethod] NVARCHAR(50) NULL,
        -- e.g. 'app-secretkey', 'userIdPasswordWithToken'
        [ApiKeyHeaderName] NVARCHAR(100) NULL,
        -- e.g. 'X-App-Key'
        [ApiKeyHeaderValue] NVARCHAR(255) NULL,
        -- value for key header
        [ApiClientHeaderName] NVARCHAR(100) NULL,
        -- e.g. 'X-App-Client'
        [ApiClientHeaderValue] NVARCHAR(255) NULL,
        -- value for client header
        [ApiExtraHeadersJson] NVARCHAR(MAX) DEFAULT '{}',
        -- additional headers as JSON
        [ApiAuthConfigJson] NVARCHAR(MAX) DEFAULT '{}',
        -- userId/password/token config as JSON

        -- Operational / billing / notification configuration (JSON or simple fields)
        [OperationalMode] NVARCHAR(20) DEFAULT 'PREPAID',
        -- PREPAID, POSTPAID, HYBRID, CASH
        [AuthenticationMethods] NVARCHAR(MAX) NULL,
        -- JSON array: ['NFC','QR','MOBILE_APP','BIOMETRIC']
        [MenuType] NVARCHAR(20) DEFAULT 'PRECOOKED',
        -- PRECOOKED, MADE_TO_ORDER, HYBRID
        [EnablePreOrdering] BIT DEFAULT 1,
        [EnableAnonymousOrdering] BIT DEFAULT 0,
        [EnableEmergencyBalance] BIT DEFAULT 0,
        [EmergencyBalanceAmount] DECIMAL(10,2) DEFAULT 100.00,
        [MinRechargeAmount] DECIMAL(10,2) DEFAULT 100.00,
        [MaxRechargeAmount] DECIMAL(10,2) DEFAULT 5000.00,
        [FixedDeductionOnRecharge] DECIMAL(10,2) DEFAULT 0.00,
        [AutoCancelOrderMinutes] INT DEFAULT 30,

        [OperatingHoursJson] NVARCHAR(MAX) DEFAULT '{"breakfast":{"start":"07:00","end":"09:00"},"lunch":{"start":"12:00","end":"14:00"},"dinner":{"start":"19:00","end":"21:00"},"snacks":{"start":"16:00","end":"18:00"}}',
        [BillingCycle] NVARCHAR(20) DEFAULT 'MONTHLY',
        [TaxEnabled] BIT DEFAULT 1,
        [CgstPercentage] DECIMAL(5,2) DEFAULT 2.5,
        [SgstPercentage] DECIMAL(5,2) DEFAULT 2.5,
        [NotificationConfigJson] NVARCHAR(MAX) DEFAULT '{"sms_enabled":true,"email_enabled":true,"push_enabled":true,"whatsapp_enabled":false}',
        [PgwEnabled] BIT DEFAULT 1,
        [PgwMerchantId] NVARCHAR(100),
        [PgwApiKeyEncrypted] NVARCHAR(MAX),

        -- Additional free-form config bucket if needed
        [OperationalConfigJson] NVARCHAR(MAX) DEFAULT '{}',
        [BillingConfig] NVARCHAR(MAX) NULL,

        -- Status
        [IsActive] BIT DEFAULT 1,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX IX_CanteenClients_Subdomain ON [dbo].[CanteenClients]([Subdomain]);
    PRINT '✓ CanteenClients table created';
END
ELSE PRINT '- CanteenClients table already exists';
GO

-- ============================================
-- 2. CANTEEN USERS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenUsers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenUsers]
    (
        [UserId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [FullName] NVARCHAR(255) NOT NULL,
        [Email] NVARCHAR(255),
        [Phone] NVARCHAR(20),
        [Username] NVARCHAR(100),
        [PasswordHash] NVARCHAR(MAX),
        [Role] NVARCHAR(20) NOT NULL DEFAULT 'STUDENT',
        [StudentID] NVARCHAR(50),
        [StudentIDC] NVARCHAR(50),
        [RollNumber] NVARCHAR(50),
        [Class] NVARCHAR(50),
        [Session] NVARCHAR(50),
        [Section] NVARCHAR(10),
        [ParentUserId] UNIQUEIDENTIFIER,
        [EmployeeId] NVARCHAR(50),
        [Department] NVARCHAR(100),
        [Designation] NVARCHAR(100),
        [ProfilePhotoUrl] NVARCHAR(MAX),
        [DateOfBirth] DATE,
        [Gender] NVARCHAR(10),
        [DietaryPreferencesJson] NVARCHAR(MAX) DEFAULT '{}',
        [NotificationPreferencesJson] NVARCHAR(MAX) DEFAULT '{}',
        [DailySpendingLimit] DECIMAL(10,2),
        [AccountStatus] NVARCHAR(20) DEFAULT 'ACTIVE',
        [EmailVerified] BIT DEFAULT 0,
        [PhoneVerified] BIT DEFAULT 0,
        [LastLoginAt] DATETIME2,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_Users_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[CanteenClients]([ClientId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CanteenUsers_Client ON [dbo].[CanteenUsers]([ClientId]);
    CREATE INDEX IX_CanteenUsers_Role ON [dbo].[CanteenUsers]([Role]);
    CREATE INDEX IX_CanteenUsers_Email ON [dbo].[CanteenUsers]([Email]) WHERE [Email] IS NOT NULL;
    CREATE INDEX IX_CanteenUsers_RollNumber ON [dbo].[CanteenUsers]([RollNumber]) WHERE [RollNumber] IS NOT NULL;
    CREATE INDEX IX_CanteenUsers_StudentID ON [dbo].[CanteenUsers]([StudentID]) WHERE [StudentID] IS NOT NULL;
    CREATE INDEX IX_CanteenUsers_StudentIDC ON [dbo].[CanteenUsers]([StudentIDC]) WHERE [StudentIDC] IS NOT NULL;
    CREATE INDEX IX_CanteenUsers_EmployeeId ON [dbo].[CanteenUsers]([EmployeeId]) WHERE [EmployeeId] IS NOT NULL;
    PRINT '✓ Canteen Users table created';
END
ELSE PRINT '- Canteen Users table already exists';
GO

-- ============================================
-- 3. Canteen USERS WALLET TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenUsersWallet]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenUsersWallet]
    (
        [WalletId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER UNIQUE NOT NULL,
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [MainBalance] DECIMAL(10,2) DEFAULT 0.00,
        [EmergencyBalanceLimit] DECIMAL(10,2) DEFAULT 0.00,
        [EmergencyBalanceUsed] DECIMAL(10,2) DEFAULT 0.00,
        [BlockedAmount] DECIMAL(10,2) DEFAULT 0.00,
        [LifetimeRecharge] DECIMAL(10,2) DEFAULT 0.00,
        [LifetimeSpent] DECIMAL(10,2) DEFAULT 0.00,
        [LifetimeRefunded] DECIMAL(10,2) DEFAULT 0.00,
        [LastRechargeAt] DATETIME2,
        [LastTransactionAt] DATETIME2,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_Wallet_CanteenUser FOREIGN KEY ([UserId]) REFERENCES [dbo].[CanteenUsers]([UserId]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_Wallet_CanteenUser ON [dbo].[CanteenUsersWallet]([UserId]);
    PRINT '✓ CanteenUsersWallet table created';
END
ELSE PRINT '- CanteenUsersWallet table already exists';
GO

-- ============================================
-- 4. Canteen TRANSACTIONS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenTransactions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenTransactions]
    (
        [TransactionId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [UserId] UNIQUEIDENTIFIER NOT NULL,
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [WalletId] UNIQUEIDENTIFIER NOT NULL,
        [TransactionType] NVARCHAR(20) NOT NULL,
        [Amount] DECIMAL(10,2) NOT NULL,
        [BalanceBefore] DECIMAL(10,2) NOT NULL,
        [BalanceAfter] DECIMAL(10,2) NOT NULL,
        [EmergencyBalanceRecovered] DECIMAL(10,2) DEFAULT 0.00,
        [EmergencyBalanceUsedInTxn] DECIMAL(10,2) DEFAULT 0.00,
        [PaymentMethod] NVARCHAR(20),
        [PaymentReference] NVARCHAR(255),
        [PaymentGatewayTxnId] NVARCHAR(255),
        [OrderId] UNIQUEIDENTIFIER,
        [Description] NVARCHAR(MAX) NOT NULL,
        [Notes] NVARCHAR(MAX),
        [MetadataJson] NVARCHAR(MAX),
        [CreatedBy] UNIQUEIDENTIFIER,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenTransactions_Wallet FOREIGN KEY ([WalletId]) REFERENCES [dbo].[CanteenUsersWallet]([WalletId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CanteenTransactions_User ON [dbo].[CanteenTransactions]([UserId]);
    CREATE INDEX IX_CanteenTransactions_Type ON [dbo].[CanteenTransactions]([TransactionType]);
    CREATE INDEX IX_CanteenTransactions_Created ON [dbo].[CanteenTransactions]([CreatedAt]);
    PRINT '✓ CanteenTransactions table created';
END
ELSE PRINT '- CanteenTransactions table already exists';
GO

-- ============================================
-- 5. Canteen NFC CARDS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenNfcCards]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenNfcCards]
    (
        [CardId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [CardNumber] NVARCHAR(100) UNIQUE NOT NULL,
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER,
        [CardStatus] NVARCHAR(20) DEFAULT 'UNASSIGNED',
        [IsBackupCard] BIT DEFAULT 0,
        [AssignedAt] DATETIME2,
        [ExpiresAt] DATETIME2,
        [LastUsedAt] DATETIME2,
        [FailedAttempts] INT DEFAULT 0,
        [LockedUntil] DATETIME2,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenNfcCards_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[CanteenClients]([ClientId]) ON DELETE CASCADE,
        CONSTRAINT FK_CanteenNfcCards_User FOREIGN KEY ([UserId]) REFERENCES [dbo].[CanteenUsers]([UserId]) ON DELETE SET NULL
    );

    CREATE UNIQUE INDEX IX_CanteenNfcCards_Number ON [dbo].[CanteenNfcCards]([CardNumber]);
    CREATE INDEX IX_CanteenNfcCards_User ON [dbo].[CanteenNfcCards]([UserId]) WHERE [UserId] IS NOT NULL;
    PRINT '✓ CanteenNfcCards table created';
END
ELSE PRINT '- CanteenNfcCards table already exists';
GO

-- ============================================
-- 6. MENU CATEGORIES TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenMenuCategories]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenMenuCategories]
    (
        [CategoryId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [CategoryName] NVARCHAR(100) NOT NULL,
        [CategoryDescription] NVARCHAR(MAX),
        [CategoryType] NVARCHAR(20),
        [CategoryIconUrl] NVARCHAR(MAX),
        [DisplayOrder] INT DEFAULT 0,
        [IsActive] BIT DEFAULT 1,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenMenuCategories_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[CanteenClients]([ClientId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_MenuCategories_Client ON [dbo].[MenuCategories]([ClientId]);
    PRINT '✓ CanteenMenuCategories table created';
END
ELSE PRINT '- CanteenMenuCategories table already exists';
GO

-- ============================================
-- 7. MENU ITEMS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenMenuItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenMenuItems]
    (
        [ItemId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [CategoryId] UNIQUEIDENTIFIER NOT NULL,
        [ItemName] NVARCHAR(255) NOT NULL,
        [ItemDescription] NVARCHAR(MAX),
        [ItemCode] NVARCHAR(50),
        [ItemType] NVARCHAR(20) DEFAULT 'PRECOOKED',
        [PreparationTime] INT DEFAULT 0,
        [IsVeg] BIT DEFAULT 1,
        [IsNonVeg] BIT DEFAULT 0,
        [IsVegan] BIT DEFAULT 0,
        [IsJain] BIT DEFAULT 0,
        [IsGlutenFree] BIT DEFAULT 0,
        [AllergensJson] NVARCHAR(MAX) DEFAULT '[]',
        [SpiceLevel] NVARCHAR(10),
        [Calories] INT,
        [ProteinGrams] DECIMAL(5,2),
        [CarbsGrams] DECIMAL(5,2),
        [FatGrams] DECIMAL(5,2),
        [BasePrice] DECIMAL(10,2) NOT NULL,
        [TaxPercentage] DECIMAL(5,2) DEFAULT 5.00,
        [DiscountedPrice] DECIMAL(10,2),
        [RoleBasedPricingJson] NVARCHAR(MAX) DEFAULT '{}',
        [IsAvailable] BIT DEFAULT 1,
        [AvailableDaysJson] NVARCHAR(MAX) DEFAULT '["MON","TUE","WED","THU","FRI","SAT","SUN"]',
        [AvailableTimeSlotsJson] NVARCHAR(MAX) DEFAULT '[]',
        [DailyQuantityLimit] INT,
        [RemainingQuantity] INT,
        [ImagesJson] NVARCHAR(MAX) DEFAULT '[]',
        [PrimaryImageUrl] NVARCHAR(MAX),
        [DisplayOrder] INT DEFAULT 0,
        [IsFeatured] BIT DEFAULT 0,
        [IsBestseller] BIT DEFAULT 0,
        [IsNew] BIT DEFAULT 0,
        [IsActive] BIT DEFAULT 1,
        [OutOfStockReason] NVARCHAR(MAX),
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenMenuItems_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[CanteenClients]([ClientId]) ON DELETE CASCADE,
        CONSTRAINT FK_CanteenMenuItems_Category FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[CanteenMenuCategories]([CategoryId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CanteenMenuItems_Client ON [dbo].[CanteenMenuItems]([ClientId]);
    CREATE INDEX IX_CanteenMenuItems_Category ON [dbo].[CanteenMenuItems]([CategoryId]);
    CREATE INDEX IX_CanteenMenuItems_Available ON [dbo].[CanteenMenuItems]([IsAvailable]);
    PRINT '✓ CanteenMenuItems table created';
END
ELSE PRINT '- CanteenMenuItems table already exists';
GO

-- ============================================
-- 8. MENU ITEM VARIANTS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenMenuItemVariants]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenMenuItemVariants]
    (
        [VariantId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ItemId] UNIQUEIDENTIFIER NOT NULL,
        [VariantName] NVARCHAR(100) NOT NULL,
        [VariantType] NVARCHAR(20),
        [VariantOptionsJson] NVARCHAR(MAX) NOT NULL,
        [IsRequired] BIT DEFAULT 0,
        [DisplayOrder] INT DEFAULT 0,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenMenuItemVariants_Item FOREIGN KEY ([ItemId]) REFERENCES [dbo].[CanteenMenuItems]([ItemId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CanteenMenuItemVariants_Item ON [dbo].[CanteenMenuItemVariants]([ItemId]);
    PRINT '✓ CanteenMenuItemVariants table created';
END
ELSE PRINT '- CanteenMenuItemVariants table already exists';
GO

-- ============================================
-- 9. ORDERS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenOrders]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenOrders]
    (
        [OrderId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER,
        [OrderNumber] NVARCHAR(50) UNIQUE NOT NULL,
        [TokenNumber] NVARCHAR(20) NOT NULL,
        [OrderType] NVARCHAR(20) NOT NULL DEFAULT 'INSTANT',
        [OrderStatus] NVARCHAR(20) DEFAULT 'PLACED',
        [PaymentStatus] NVARCHAR(20) DEFAULT 'PENDING',
        [Subtotal] DECIMAL(10,2) NOT NULL,
        [TaxAmount] DECIMAL(10,2) DEFAULT 0.00,
        [DiscountAmount] DECIMAL(10,2) DEFAULT 0.00,
        [TotalAmount] DECIMAL(10,2) NOT NULL,
        [AmountBlocked] BIT DEFAULT 0,
        [DeductedFromMain] DECIMAL(10,2) DEFAULT 0.00,
        [DeductedFromEmergency] DECIMAL(10,2) DEFAULT 0.00,
        [PaymentMethod] NVARCHAR(20),
        [DeliveryTimeSlot] DATETIME2,
        [TableNumber] NVARCHAR(20),
        [RoomNumber] NVARCHAR(50),
        [SpecialInstructions] NVARCHAR(MAX),
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [ConfirmedAt] DATETIME2,
        [PreparingAt] DATETIME2,
        [ReadyAt] DATETIME2,
        [DeliveredAt] DATETIME2,
        [CancelledAt] DATETIME2,
        [AutoCancelAt] DATETIME2,
        [CancelledBy] UNIQUEIDENTIFIER,
        [CancellationReason] NVARCHAR(MAX),
        [IsAutoCancelled] BIT DEFAULT 0,
        [NfcCardUsed] NVARCHAR(100),
        [PreparedBy] UNIQUEIDENTIFIER,
        [DeliveredBy] UNIQUEIDENTIFIER,
        [Rating] INT,
        [Feedback] NVARCHAR(MAX),
        [MetadataJson] NVARCHAR(MAX),
        [LocalOrderId] UNIQUEIDENTIFIER,
        [SyncStatus] NVARCHAR(20) DEFAULT 'SYNCED',
        [SyncAttempts] INT DEFAULT 0,
        [LastSyncAttemptAt] DATETIME2,
        [SyncedAt] DATETIME2,
        [SyncError] NVARCHAR(MAX),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenOrders_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[CanteenClients]([ClientId]) ON DELETE CASCADE,
        CONSTRAINT FK_CanteenOrders_User FOREIGN KEY ([UserId]) REFERENCES [dbo].[CanteenUsers]([UserId]) ON DELETE SET NULL
    );

    CREATE UNIQUE INDEX IX_CanteenOrders_OrderNumber ON [dbo].[CanteenOrders]([OrderNumber]);
    CREATE INDEX IX_CanteenOrders_Status ON [dbo].[CanteenOrders]([OrderStatus], [CreatedAt]);
    CREATE INDEX IX_CanteenOrders_AutoCancel ON [dbo].[CanteenOrders]([AutoCancelAt]) WHERE [OrderStatus] = 'READY';
    PRINT '✓ CanteenOrders table created';
END
ELSE PRINT '- CanteenOrders table already exists';
GO

-- ============================================
-- 10. ORDER ITEMS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenOrderItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenOrderItems](
        [OrderItemId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [OrderId] UNIQUEIDENTIFIER NOT NULL,
        [ItemId] UNIQUEIDENTIFIER NOT NULL,
        [ItemName] NVARCHAR(255) NOT NULL,
        [ItemCode] NVARCHAR(50),
        [Quantity] INT NOT NULL,
        [UnitPrice] DECIMAL(10,2) NOT NULL,
        [CustomizationsJson] NVARCHAR(MAX) DEFAULT '[]',
        [Subtotal] DECIMAL(10,2) NOT NULL,
        [TaxAmount] DECIMAL(10,2) DEFAULT 0.00,
        [Total] DECIMAL(10,2) NOT NULL,
        [StockReserved] BIT DEFAULT 0,
        [StockDeducted] BIT DEFAULT 0,
        [ItemStatus] NVARCHAR(20) DEFAULT 'PENDING',
        [SpecialInstructions] NVARCHAR(MAX),
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenOrderItems_Order FOREIGN KEY ([OrderId]) REFERENCES [dbo].[CanteenOrders]([OrderId]) ON DELETE CASCADE,
        CONSTRAINT FK_CanteenOrderItems_Item FOREIGN KEY ([ItemId]) REFERENCES [dbo].[CanteenMenuItems]([ItemId]) ON DELETE RESTRICT
    );

    CREATE INDEX IX_CanteenOrderItems_Order ON [dbo].[CanteenOrderItems]([OrderId]);
    PRINT '✓ CanteenOrderItems table created';
END
ELSE PRINT '- CanteenOrderItems table already exists';
GO

-- ============================================
-- 11. INVENTORY TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenInventory]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenInventory]
    (
        [InventoryId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [ItemId] UNIQUEIDENTIFIER UNIQUE NOT NULL,
        [TotalStock] DECIMAL(10,2) DEFAULT 0.00,
        [ReservedStock] DECIMAL(10,2) DEFAULT 0.00,
        [MinimumStockAlert] DECIMAL(10,2) DEFAULT 10.00,
        [OptimalStockLevel] DECIMAL(10,2) DEFAULT 100.00,
        [UnitOfMeasurement] NVARCHAR(20) DEFAULT 'PIECE',
        [CostPerUnit] DECIMAL(10,2) DEFAULT 0.00,
        [TotalValue] DECIMAL(10,2) DEFAULT 0.00,
        [LastPurchasePrice] DECIMAL(10,2),
        [LastRestockedAt] DATETIME2,
        [LastRestockedBy] UNIQUEIDENTIFIER,
        [LastUpdatedAt] DATETIME2,
        [AlertsEnabled] BIT DEFAULT 1,
        [LowStockNotifiedAt] DATETIME2,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenInventory_Client FOREIGN KEY ([ClientId]) REFERENCES [dbo].[CanteenClients]([ClientId]) ON DELETE CASCADE,
        CONSTRAINT FK_CanteenInventory_Item FOREIGN KEY ([ItemId]) REFERENCES [dbo].[CanteenMenuItems]([ItemId]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_CanteenInventory_Item ON [dbo].[CanteenInventory]([ItemId]);
    CREATE INDEX IX_CanteenInventory_LowStock ON [dbo].[CanteenInventory]([TotalStock]) WHERE [TotalStock] <= [MinimumStockAlert];
    PRINT '✓ CanteenInventory table created';
END
ELSE PRINT '- CanteenInventory table already exists';
GO

-- ============================================
-- 12. INVENTORY TRANSACTIONS TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenInventoryTransactions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenInventoryTransactions]
    (
        [TransactionId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [InventoryId] UNIQUEIDENTIFIER NOT NULL,
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [TransactionType] NVARCHAR(20) NOT NULL,
        [Quantity] DECIMAL(10,2) NOT NULL,
        [StockBefore] DECIMAL(10,2) NOT NULL,
        [StockAfter] DECIMAL(10,2) NOT NULL,
        [RelatedOrderId] UNIQUEIDENTIFIER,
        [RelatedPurchaseId] UNIQUEIDENTIFIER,
        [CostPerUnit] DECIMAL(10,2),
        [TotalCost] DECIMAL(10,2),
        [Notes] NVARCHAR(MAX),
        [CreatedBy] UNIQUEIDENTIFIER,
        [CreatedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenInventoryTransactions_Inventory FOREIGN KEY ([InventoryId]) REFERENCES [dbo].[CanteenInventory]([InventoryId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CanteenInventoryTransactions_Inventory ON [dbo].[CanteenInventoryTransactions]([InventoryId]);
    CREATE INDEX IX_CanteenInventoryTransactions_Type ON [dbo].[CanteenInventoryTransactions]([TransactionType]);
    PRINT '✓ Canteen InventoryTransactions table created';
END
ELSE PRINT '- Canteen InventoryTransactions table already exists';
GO

-- ============================================
-- 13. WASTAGE LOG TABLE
-- ============================================
IF NOT EXISTS (SELECT *
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CanteenWastageLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenWastageLog]
    (
        [WastageId] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [ClientId] UNIQUEIDENTIFIER NOT NULL,
        [ItemId] UNIQUEIDENTIFIER NOT NULL,
        [QuantityWasted] DECIMAL(10,2) NOT NULL,
        [UnitOfMeasurement] NVARCHAR(20),
        [Reason] NVARCHAR(50),
        [DetailedReason] NVARCHAR(MAX),
        [CostPerUnit] DECIMAL(10,2),
        [TotalLoss] DECIMAL(10,2),
        [RelatedOrderId] UNIQUEIDENTIFIER,
        [LoggedBy] UNIQUEIDENTIFIER,
        [LoggedAt] DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_CanteenWastageLog_Item FOREIGN KEY ([ItemId]) REFERENCES [dbo].[CanteenMenuItems]([ItemId]) ON DELETE CASCADE
    );

    CREATE INDEX IX_CanteenWastageLog_Item ON [dbo].[CanteenWastageLog]([ItemId]);
    CREATE INDEX IX_CanteenWastageLog_Logged ON [dbo].[CanteenWastageLog]([LoggedAt]);
    PRINT '✓ CanteenWastageLog table created';
END
ELSE PRINT '- CanteenWastageLog table already exists';
GO

-- ============================================
-- 14. INSERT SAMPLE CLIENT
-- ============================================
IF NOT EXISTS (SELECT *
FROM [dbo].[CanteenClients])
BEGIN
    INSERT INTO [dbo].[CanteenClients]
        ([ClientId], [ClientName], [ClientCode], [ClientShortName], [ClientType], [Subdomain], [IsActive])
    VALUES
        (NEWID(), N'Chattogram Cantonment Public College', 'C217', 'CCPC', 'EDUCATIONAL', 'ccpc', 1);

    PRINT '✓ Sample client created';
END
ELSE PRINT '- Client already exists';
GO

PRINT '============================================';
PRINT 'Database Migration Completed Successfully!';
PRINT '============================================';
PRINT '';
PRINT 'Summary:';
PRINT '✓ Multi-tenant architecture ready';
PRINT '✓ Wallet system with emergency balance';
PRINT '✓ NFC card management';
PRINT '✓ Enhanced menu management';
PRINT '✓ Order management with offline support';
PRINT '✓ Inventory management';
PRINT '';
PRINT 'Next Steps:';
PRINT '1. Update connection string in appsettings.json';
PRINT '2. Run Entity Framework migrations (optional)';
PRINT '3. Configure client settings';
PRINT '4. Add users and assign NFC cards';
PRINT '5. Set up menu items and inventory';
PRINT '';
PRINT '============================================';
GO

