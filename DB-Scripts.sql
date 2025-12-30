/*
============================================
CANTEEN MANAGEMENT SYSTEM - COMPLETE DATABASE MIGRATION
Chattogram Cantonment Public College
Database: ccpc_c217
============================================
*/

USE [ccpc_c217];
GO

--DROP VIEW vw_StudentInfo_Canteen
CREATE VIEW vw_StudentInfo_Canteen
AS
SELECT SM.StudentID,SM.StudentIDC,SourceID,SM.ProgramID,SubjectID,GroupID,ClassID,SM.SectionID,ShiftID,SM.SessionID,SM.VersionID,StudentCatID,HouseID,StudentRoll,StudentName,
ContactNo,SD.StudentSex,PhotoPathS,SV.VersionName,SP.ProgramName,SS.SessionName,SC.SectionName 
FROM STDStudentMaster SM INNER JOIN STDStudentDetails SD ON SM.StudentID = SD.StudentID
LEFT JOIN STDProgram SP ON SM.ProgramID = SP.ProgramID
LEFT JOIN STDSession SS ON SM.SessionID = SS.SessionID
LEFT JOIN STDVersionInfo SV ON SM.VersionID = SV.VersionID
LEFT JOIN STDSection SC ON SM.SectionID = SC.SectionID
WHERE SM.IsActive = 'Y' AND SM.IsArchive = 'N'
GO

--DROP VIEW vw_EmployeeInfo_Canteen
CREATE VIEW vw_EmployeeInfo_Canteen
AS
SELECT EM.EmployeeID,EM.EmployeeName,EM.MobileNo,EM.DesignationID,DI.DataName AS DesignationName,'/employees/'+EM.EmployeeID+'.jpg' AS EmployeePhotoPath ,ED.EmployeeTypeID,DI2.DataName AS EmployeeTypeName
,EM.GenderID,DI3.DataName AS EmployeeGender
FROM HRMEmployeeMaster EM INNER JOIN HRMEmployeeDetails ED ON EM.EmployeeID = ED.EmployeeID 
LEFT JOIN PMSDataInfo DI ON EM.DesignationID = DI.DataID
LEFT JOIN HRMDataInfo DI2 ON ED.EmployeeTypeID = DI2.DataID
LEFT JOIN PMSDataInfo DI3 ON EM.GenderID = DI3.DataID
WHERE EM.IsActive = 'Y'
GO

PRINT '============================================';
PRINT 'Starting Database Migration...';
PRINT '============================================';
GO

--DROp TABLE CanteenFoodItems
-- ============================================
-- 1. FoodItems Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CanteenFoodItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenFoodItems](
        [FoodItemID] INT IDENTITY(1,1) NOT NULL,
        [ItemName] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [Price] DECIMAL(10, 2) NOT NULL,
        [Category] INT NULL,
        [ImageUrl] NVARCHAR(500) NULL,
        [IsAvailable] BIT NOT NULL DEFAULT 1,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_FoodItems] PRIMARY KEY CLUSTERED ([FoodItemID] ASC)
    );
    PRINT '✓ CanteenFoodItems table created';
END
ELSE PRINT '- CanteenFoodItems table already exists';
GO
--DROp TABLE CanteenUserBalances
-- ============================================
-- 2. UserBalances Table (NEW)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CanteenUserBalances]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenUserBalances](
        [BalanceID] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(15) NOT NULL,
        [UserType] INT NOT NULL,
        [TotalBalance] DECIMAL(10, 2) NOT NULL,
        [UsedBalance] DECIMAL(10, 2) NOT NULL DEFAULT 0,
        [LastUpdated] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_UserBalances] PRIMARY KEY CLUSTERED ([BalanceID] ASC)
       
    );
    --DROP INDEX  [IX_UserBalances_User]
    --CREATE UNIQUE INDEX [IX_UserBalances_User] 
    --    ON [dbo].[CanteenUserBalances]([UserId]) WHERE [UserId] IS NOT NULL;

	CREATE UNIQUE INDEX [IX_UserBalances_User_Type] 
    ON [dbo].[CanteenUserBalances]([UserId], [UserType]);
    
    PRINT '✓ CanteenUserBalances table created';
END
ELSE PRINT '- CanteenUserBalances table already exists';
GO

--DROP TABLE CanteenOrders
-- ============================================
-- 3. Orders Table (ENHANCED)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CanteenOrders]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenOrders](
        [OrderID] INT IDENTITY(1,1) NOT NULL,
        [OrderNumber] NVARCHAR(50) NOT NULL,
        [UserId]  NVARCHAR(15) NOT NULL,
        [UserType] INT NOT NULL,
        [TotalAmount] DECIMAL(10, 2) NOT NULL,
        [Status] INT NOT NULL DEFAULT (0),
        [OrderDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [DeliveredDate] DATETIME NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([OrderID] ASC)
    );
    
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [dbo].[CanteenOrders]([OrderNumber]);
    CREATE INDEX [IX_Orders_Status_Date] ON [dbo].[CanteenOrders]([Status], [OrderDate]);
    
    PRINT '✓ CanteenOrders table created';
END
ELSE PRINT '- CanteenOrders table already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Orders_UserBalances')
BEGIN
    ALTER TABLE [dbo].[CanteenOrders]
    ADD CONSTRAINT [FK_Orders_UserBalances] 
    FOREIGN KEY ([UserId], [UserType]) 
    REFERENCES [dbo].[CanteenUserBalances]([UserId], [UserType])
    ON DELETE NO ACTION;
    
    PRINT '✓ Foreign key from Orders to UserBalances created';
END
ELSE PRINT '- Foreign key already exists';
GO

--DROP TABLE CanteenOrderItems
-- ============================================
-- 4. OrderItems Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CanteenOrderItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenOrderItems](
        [OrderItemID] INT IDENTITY(1,1) NOT NULL,
        [OrderID] INT NOT NULL,
        [FoodItemID] INT NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] DECIMAL(10, 2) NOT NULL,
        [TotalPrice] DECIMAL(10, 2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY CLUSTERED ([OrderItemID] ASC),
        CONSTRAINT [FK_OrderItems_Order] FOREIGN KEY([OrderID]) 
            REFERENCES [dbo].[CanteenOrders]([OrderID]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_FoodItem] FOREIGN KEY([FoodItemID]) 
            REFERENCES [dbo].[CanteenFoodItems]([FoodItemID])
    );
    
    CREATE INDEX [IX_OrderItems_OrderID] ON [dbo].[CanteenOrderItems]([OrderID]);
    
    PRINT '✓ OrderItems table created';
END
ELSE PRINT '- OrderItems table already exists';
GO

--DROP TABLE CanteenDailyMenus
-- ============================================
-- 5. DailyMenus Table (WITH QUANTITY)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CanteenDailyMenus]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenDailyMenus](
        [DailyMenuID] INT IDENTITY(1,1) NOT NULL,
        [FoodItemID] INT NOT NULL,
        [MenuDate] DATE NOT NULL,
        [MealType] INT NULL,
        [IsAvailable] BIT NOT NULL DEFAULT 1,
        [AvailableQuantity] INT NOT NULL DEFAULT 0,
        [InitialQuantity] INT NOT NULL DEFAULT 0,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_DailyMenus] PRIMARY KEY CLUSTERED ([DailyMenuID] ASC),
        CONSTRAINT [FK_DailyMenus_FoodItem] FOREIGN KEY([FoodItemID]) 
            REFERENCES [dbo].[CanteenFoodItems]([FoodItemID])
    );
    
    CREATE INDEX [IX_DailyMenus_MenuDate] ON [dbo].[CanteenDailyMenus]([MenuDate]);
    CREATE INDEX [IX_DailyMenus_Date_Available] ON [dbo].[CanteenDailyMenus]([MenuDate], [IsAvailable]);
    
    PRINT '✓ DailyMenus table created';
END
ELSE PRINT '- DailyMenus table already exists';
GO

--DROP TABLE CanteenOrderItems
-- ============================================
-- 6. WeeklyMenuTemplates Table (NEW)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CanteenWeeklyMenuTemplates]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CanteenWeeklyMenuTemplates](
        [TemplateID] INT IDENTITY(1,1) NOT NULL,
        [TemplateName] NVARCHAR(200) NOT NULL,
        [DayOfWeek] INT NOT NULL,
        [FoodItemID] INT NOT NULL,
        [MealType] INT NULL,
        [DefaultQuantity] INT NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_WeeklyMenuTemplates] PRIMARY KEY CLUSTERED ([TemplateID] ASC),
        CONSTRAINT [FK_WeeklyMenuTemplates_FoodItem] FOREIGN KEY([FoodItemID]) 
            REFERENCES [dbo].[CanteenFoodItems]([FoodItemID]),
        CONSTRAINT [CHK_DayOfWeek] CHECK ([DayOfWeek] >= 0 AND [DayOfWeek] <= 6)
    );
    
    CREATE INDEX [IX_WeeklyMenuTemplates_DayOfWeek] ON [dbo].[CanteenWeeklyMenuTemplates]([DayOfWeek], [IsActive]);
    
    PRINT '✓ WeeklyMenuTemplates table created';
END
ELSE PRINT '- WeeklyMenuTemplates table already exists';
GO

--DROP TABLE CanteenOrderItems
-- ============================================
-- 7. Insert Sample Food Items (30+ items)
-- ============================================
IF NOT EXISTS (SELECT * FROM [dbo].[CanteenFoodItems])
BEGIN
    SET IDENTITY_INSERT [dbo].[CanteenFoodItems] ON;
    
    INSERT INTO [dbo].[CanteenFoodItems] ([FoodItemID], [ItemName], [Description], [Price], [Category], [IsAvailable], [IsActive])
    VALUES 
        -- Breakfast Items
        (1, N'পরোটা ও সবজি', N'তাজা পরোটা সহ মিক্স সবজি তরকারি', 60.00, 1, 1, 1),
        (2, N'পরোটা ও ডিম', N'পরোটা সহ দুই পিস অমলেট', 70.00, 1, 1, 1),
        (3, N'ভেজিটেবল স্যান্ডউইচ', N'তাজা সবজি দিয়ে স্বাস্থ্যকর স্যান্ডউইচ', 80.00, 1, 1, 1),
        (4, N'ডিম স্যান্ডউইচ', N'অমলেট সহ স্যান্ডউইচ', 90.00, 1, 1, 1),
        (5, N'নাস্তা কম্বো', N'পরোটা, ডিম ও চা', 100.00, 1, 1, 1),
        
        -- Main Course
        (6, N'ভাত ও তরকারি', N'ভাত সহ মিক্স তরকারি', 90.00, 2, 1, 1),
        (7, N'পোলাও', N'বাসমতি চালের সুগন্ধি পোলাও', 100.00, 2, 1, 1),
        (8, N'খিচুড়ি', N'মসলাদার ডাল খিচুড়ি', 80.00, 2, 1, 1),
        (9, N'চিকেন কারি', N'মসলাদার চিকেন কারি', 120.00, 2, 1, 1),
        (10, N'বিফ কারি', N'ঝোল বিফ কারি', 140.00, 2, 1, 1),
        (11, N'ফিশ কারি', N'তাজা মাছের তরকারি', 110.00, 2, 1, 1),
        (12, N'চিকেন ফ্রাইড রাইস', N'চিকেন ও সবজি ফ্রাইড রাইস', 150.00, 2, 1, 1),
        (13, N'নুডলস', N'ভেজিটেবল নুডলস', 100.00, 2, 1, 1),
        (14, N'চিকেন নুডলস', N'চিকেন সহ নুডলস', 130.00, 2, 1, 1),
        
        -- Snacks
        (15, N'সিঙ্গারা', N'ঐতিহ্যবাহী বাংলা সিঙ্গারা', 15.00, 3, 1, 1),
        (16, N'সমুচা', N'মসলাদার আলু সমুচা', 15.00, 3, 1, 1),
        (17, N'চিকেন রোল', N'মসলাদার চিকেন রোল', 100.00, 3, 1, 1),
        (18, N'ভেজিটেবল রোল', N'তাজা সবজি রোল', 80.00, 3, 1, 1),
        (19, N'চিকেন বার্গার', N'চিকেন প্যাটি সহ বার্গার', 120.00, 3, 1, 1),
        (20, N'বিফ বার্গার', N'বিফ প্যাটি সহ বার্গার', 150.00, 3, 1, 1),
        (21, N'পিৎজা স্লাইস', N'চিজ পিৎজা স্লাইস', 120.00, 3, 1, 1),
        (22, N'স্প্রিং রোল', N'ক্রিস্পি স্প্রিং রোল', 60.00, 3, 1, 1),
        (23, N'ফ্রেঞ্চ ফ্রাই', N'ক্রিস্পি ফ্রেঞ্চ ফ্রাই', 70.00, 3, 1, 1),
        (24, N'চিকেন নাগেটস', N'৫ পিস চিকেন নাগেটস', 90.00, 3, 1, 1),
        
        -- Drinks
        (25, N'চা', N'গরম মসলা চা', 10.00, 4, 1, 1),
        (26, N'কফি', N'ব্ল্যাক কফি', 25.00, 4, 1, 1),
        (27, N'দুধ চা', N'দুধ দিয়ে চা', 20.00, 4, 1, 1),
        (28, N'কোল্ড ড্রিংকস', N'পেপসি / কোকা-কোলা', 30.00, 4, 1, 1),
        (29, N'মিনারেল ওয়াটার', N'বোতলজাত পানি ৫০০ মিলি', 15.00, 4, 1, 1),
        (30, N'ফ্রেশ জুস', N'তাজা ফলের জুস', 50.00, 4, 1, 1),
        (31, N'লেবুর শরবত', N'তাজা লেবুর শরবত', 35.00, 4, 1, 1),
        (32, N'মাঠা', N'ঠান্ডা দই মাঠা', 30.00, 4, 1, 1);
    
    SET IDENTITY_INSERT [dbo].[CanteenFoodItems] OFF;
    
    PRINT '✓ Sample food items inserted (32 items)';
END
ELSE PRINT '- Food items already exist';
GO

--DROP TABLE CanteenOrderItems
-- ============================================
-- 8. Insert Sample Weekly Templates
-- ============================================
IF NOT EXISTS (SELECT * FROM [dbo].[CanteenWeeklyMenuTemplates])
BEGIN
    -- Saturday (0) Template
    INSERT INTO [dbo].[CanteenWeeklyMenuTemplates] ([TemplateName], [DayOfWeek], [FoodItemID], [MealType], [DefaultQuantity], [DisplayOrder])
    VALUES 
        (N'Saturday Default', 0, 1, 1, 50, 1),
        (N'Saturday Default', 0, 6, 2, 100, 2),
        (N'Saturday Default', 0, 9, 2, 80, 3),
        (N'Saturday Default', 0, 15, 3, 200, 4),
        (N'Saturday Default', 0, 25, 4, 150, 5);
    
    -- Sunday (1) Template
    INSERT INTO [dbo].[CanteenWeeklyMenuTemplates] ([TemplateName], [DayOfWeek], [FoodItemID], [MealType], [DefaultQuantity], [DisplayOrder])
    VALUES 
        (N'Sunday Default', 1, 2, 1, 50, 1),
        (N'Sunday Default', 1, 7, 2, 100, 2),
        (N'Sunday Default', 1, 10, 2, 70, 3),
        (N'Sunday Default', 1, 17, 3, 100, 4),
        (N'Sunday Default', 1, 25, 4, 150, 5);
    
    -- Monday (2) Template
    INSERT INTO [dbo].[CanteenWeeklyMenuTemplates] ([TemplateName], [DayOfWeek], [FoodItemID], [MealType], [DefaultQuantity], [DisplayOrder])
    VALUES 
        (N'Monday Default', 2, 3, 1, 60, 1),
        (N'Monday Default', 2, 12, 2, 90, 2),
        (N'Monday Default', 2, 19, 3, 80, 3),
        (N'Monday Default', 2, 28, 4, 120, 4);
    
    PRINT '✓ Sample weekly templates created';
END
GO

--DROP TABLE CanteenOrderItems
-- ============================================
-- 9. Insert Today's Menu with Quantity
-- ============================================
DELETE FROM [dbo].[CanteenDailyMenus] WHERE MenuDate = CAST(GETDATE() AS DATE);

INSERT INTO [dbo].[CanteenDailyMenus] ([FoodItemID], [MenuDate], [MealType], [AvailableQuantity], [InitialQuantity], [IsAvailable], [DisplayOrder])
VALUES
    (1, CAST(GETDATE() AS DATE), 1, 50, 50, 1, 1),
    (2, CAST(GETDATE() AS DATE), 1, 50, 50, 1, 2),
    (6, CAST(GETDATE() AS DATE), 2, 100, 100, 1, 3),
    (7, CAST(GETDATE() AS DATE), 2, 80, 80, 1, 4),
    (9, CAST(GETDATE() AS DATE), 2, 60, 60, 1, 5),
    (12, CAST(GETDATE() AS DATE), 2, 70, 70, 1, 6),
    (15, CAST(GETDATE() AS DATE), 3, 200, 200, 1, 7),
    (16, CAST(GETDATE() AS DATE), 3, 200, 200, 1, 8),
    (17, CAST(GETDATE() AS DATE), 3, 100, 100, 1, 9),
    (19, CAST(GETDATE() AS DATE), 3, 80, 80, 1, 10),
    (25, CAST(GETDATE() AS DATE), 4, 150, 150, 1, 11),
    (28, CAST(GETDATE() AS DATE), 4, 100, 100, 1, 12),
    (29, CAST(GETDATE() AS DATE), 4, 200, 200, 1, 13),
    (30, CAST(GETDATE() AS DATE), 4, 80, 80, 1, 14);

PRINT '✓ Today''s menu inserted with quantities';
GO

-- ============================================
-- 10. Create Sample User Balances
-- ============================================
-- Add sample balances for testing
-- Note: Replace with actual student/employee IDs from your database

PRINT '============================================';
PRINT 'Database Migration Completed Successfully!';
PRINT '============================================';
PRINT '';
PRINT 'Summary:';
PRINT '✓ FoodItems: 32 items';
PRINT '✓ UserBalances: Ready for use';
PRINT '✓ Orders: Ready for tracking';
PRINT '✓ DailyMenus: Today''s menu loaded';
PRINT '✓ WeeklyTemplates: 3 templates created';
PRINT '';
PRINT 'Next Steps:';
PRINT '1. Update connection string in appsettings.json';
PRINT '2. Run the ASP.NET application';
PRINT '3. Test with actual student/employee IDs';
PRINT '4. Configure user balances as needed';
PRINT '';
PRINT '============================================';
GO

--SELECT * FROM CanteenFoodItems
--SELECT * FROM CanteenUserBalances
--SELECT * FROM CanteenOrders
--SELECT * FROM CanteenOrderItems
--SELECT * FROM CanteenDailyMenus
--SELECT * FROM CanteenWeeklyMenuTemplates

--DROP TABLE CanteenFoodItems
--DROP TABLE CanteenUserBalances
--DROP TABLE CanteenOrders
--DROP TABLE CanteenOrderItems
--DROP TABLE CanteenDailyMenus
--DROP TABLE CanteenWeeklyMenuTemplates