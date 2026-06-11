using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCanteenSaasSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppApiKeys",
                columns: table => new
                {
                    ApiKeyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AppKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AllowedIps = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UsageCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppApiKeys", x => x.ApiKeyId);
                });

            migrationBuilder.CreateTable(
                name: "AppPermissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPermissions", x => x.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "AppRoles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRoles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenAuditEntries",
                columns: table => new
                {
                    AuditID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PerformedByRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenAuditEntries", x => x.AuditID);
                });

            migrationBuilder.CreateTable(
                name: "CanteenFoodItems",
                columns: table => new
                {
                    FoodItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenFoodItems", x => x.FoodItemID);
                });

            migrationBuilder.CreateTable(
                name: "CanteenNfcCards",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardUid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssuedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenNfcCards", x => x.CardId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenNotificationLog",
                columns: table => new
                {
                    NotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenNotificationLog", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenPaymentGatewayConfigs",
                columns: table => new
                {
                    GatewayConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GatewayCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubTitle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    GatewayGroup = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsSandbox = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    LiveBaseUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LiveUsername = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LivePassword = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LiveAppKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LiveAppSecret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LiveMerchantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LiveMerchantNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LivePublicKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LivePrivateKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LiveCallbackUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LiveWebhookUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LiveIpnUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LiveFailCallbackUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SandboxBaseUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SandboxUsername = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SandboxPassword = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SandboxAppKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SandboxAppSecret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SandboxMerchantId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SandboxMerchantNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SandboxPublicKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SandboxPrivateKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SandboxCallbackUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SandboxWebhookUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SandboxIpnUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SandboxFailCallbackUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LastTestStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    LastTestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestMessage = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NotifyOnSuccess = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnFailure = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnRefund = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenPaymentGatewayConfigs", x => x.GatewayConfigId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenPaymentTransactions",
                columns: table => new
                {
                    PaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    UserType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    ChannelCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GatewayConfigId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    GatewayMessage = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CallbackPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RedirectUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenPaymentTransactions", x => x.PaymentId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenTenantSettings",
                columns: table => new
                {
                    SettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenTenantSettings", x => x.SettingId);
                });

            migrationBuilder.CreateTable(
                name: "CanteenUserBalances",
                columns: table => new
                {
                    BalanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UsedBalance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    BlockedAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    EmergencyEntitlement = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    EmergencyUsed = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenUserBalances", x => x.BalanceID);
                    table.UniqueConstraint("AK_CanteenUserBalances_UserId_UserType", x => new { x.UserId, x.UserType });
                });

            migrationBuilder.CreateTable(
                name: "CanteenWalletLedger",
                columns: table => new
                {
                    LedgerID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BalanceID = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    BlockedAfter = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    OrderID = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenWalletLedger", x => x.LedgerID);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    ClientId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.ClientId);
                });

            migrationBuilder.CreateTable(
                name: "DirectorySyncRuns",
                columns: table => new
                {
                    SyncRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StudentsAdded = table.Column<int>(type: "int", nullable: false),
                    StudentsUpdated = table.Column<int>(type: "int", nullable: false),
                    StudentsDisabled = table.Column<int>(type: "int", nullable: false),
                    EmployeesAdded = table.Column<int>(type: "int", nullable: false),
                    EmployeesUpdated = table.Column<int>(type: "int", nullable: false),
                    EmployeesDisabled = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HighWatermarkUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectorySyncRuns", x => x.SyncRunId);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CardIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmployeeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.EmployeeId);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CardIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Program = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Class = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Section = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Session = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.StudentId);
                });

            migrationBuilder.CreateTable(
                name: "AppRolePermissions",
                columns: table => new
                {
                    RolePermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRolePermissions", x => x.RolePermissionId);
                    table.ForeignKey(
                        name: "FK_AppRolePermissions_AppPermissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "AppPermissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppRolePermissions_AppRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AppRoles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanteenDailyMenus",
                columns: table => new
                {
                    DailyMenuID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FoodItemID = table.Column<int>(type: "int", nullable: false),
                    MenuDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MealType = table.Column<int>(type: "int", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: false),
                    InitialQuantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenDailyMenus", x => x.DailyMenuID);
                    table.ForeignKey(
                        name: "FK_CanteenDailyMenus_CanteenFoodItems_FoodItemID",
                        column: x => x.FoodItemID,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanteenWeeklyMenuTemplates",
                columns: table => new
                {
                    TemplateID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    FoodItemID = table.Column<int>(type: "int", nullable: false),
                    MealType = table.Column<int>(type: "int", nullable: true),
                    DefaultQuantity = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenWeeklyMenuTemplates", x => x.TemplateID);
                    table.ForeignKey(
                        name: "FK_CanteenWeeklyMenuTemplates_CanteenFoodItems_FoodItemID",
                        column: x => x.FoodItemID,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanteenCardEvents",
                columns: table => new
                {
                    CardEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CardId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    PreviousCardUid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NewCardUid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenCardEvents", x => x.CardEventId);
                    table.ForeignKey(
                        name: "FK_CanteenCardEvents_CanteenNfcCards_CardId",
                        column: x => x.CardId,
                        principalTable: "CanteenNfcCards",
                        principalColumn: "CardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanteenGatewayChannels",
                columns: table => new
                {
                    ChannelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GatewayConfigId = table.Column<int>(type: "int", nullable: false),
                    ChannelCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChannelName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ChargeType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    TotalCharge = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MinChargeAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MaxChargeAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    VatTaxPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    OurMarkup = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenGatewayChannels", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_CanteenGatewayChannels_CanteenPaymentGatewayConfigs_GatewayConfigId",
                        column: x => x.GatewayConfigId,
                        principalTable: "CanteenPaymentGatewayConfigs",
                        principalColumn: "GatewayConfigId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanteenOrders",
                columns: table => new
                {
                    OrderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    UserType = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputSequence = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenOrders", x => x.OrderID);
                    table.ForeignKey(
                        name: "FK_CanteenOrders_CanteenUserBalances_UserId_UserType",
                        columns: x => new { x.UserId, x.UserType },
                        principalTable: "CanteenUserBalances",
                        principalColumns: new[] { "UserId", "UserType" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LinkedPersonId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UserKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockedOutUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AppUsers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CanteenOrderItems",
                columns: table => new
                {
                    OrderItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderID = table.Column<int>(type: "int", nullable: false),
                    FoodItemID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanteenOrderItems", x => x.OrderItemID);
                    table.ForeignKey(
                        name: "FK_CanteenOrderItems_CanteenFoodItems_FoodItemID",
                        column: x => x.FoodItemID,
                        principalTable: "CanteenFoodItems",
                        principalColumn: "FoodItemID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CanteenOrderItems_CanteenOrders_OrderID",
                        column: x => x.OrderID,
                        principalTable: "CanteenOrders",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppRefreshTokens",
                columns: table => new
                {
                    RefreshTokenId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    RevokedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ReplacedByToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRefreshTokens", x => x.RefreshTokenId);
                    table.ForeignKey(
                        name: "FK_AppRefreshTokens_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserRoles",
                columns: table => new
                {
                    UserRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserRoles", x => x.UserRoleId);
                    table.ForeignKey(
                        name: "FK_AppUserRoles_AppRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AppRoles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppUserRoles_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppApiKeys_AppKey",
                table: "AppApiKeys",
                column: "AppKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppApiKeys_ClientId",
                table: "AppApiKeys",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPermissions_PermissionCode",
                table: "AppPermissions",
                column: "PermissionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRefreshTokens_TokenHash",
                table: "AppRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRefreshTokens_UserId",
                table: "AppRefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRolePermissions_PermissionId",
                table: "AppRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRolePermissions_RoleId_PermissionId",
                table: "AppRolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRoles_RoleCode",
                table: "AppRoles",
                column: "RoleCode");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserRoles_RoleId",
                table: "AppUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserRoles_UserId_RoleId",
                table: "AppUserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_ClientId",
                table: "AppUsers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_UserName",
                table: "AppUsers",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanteenAuditEntries_ClientId",
                table: "CanteenAuditEntries",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenAuditEntries_EntityType",
                table: "CanteenAuditEntries",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenAuditEntries_OccurredAtUtc",
                table: "CanteenAuditEntries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenAuditEntries_PerformedBy",
                table: "CanteenAuditEntries",
                column: "PerformedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenCardEvents_CardId",
                table: "CanteenCardEvents",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenCardEvents_ClientId",
                table: "CanteenCardEvents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenDailyMenus_ClientId",
                table: "CanteenDailyMenus",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenDailyMenus_FoodItemID",
                table: "CanteenDailyMenus",
                column: "FoodItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenDailyMenus_MenuDate",
                table: "CanteenDailyMenus",
                column: "MenuDate");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenFoodItems_ClientId",
                table: "CanteenFoodItems",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenGatewayChannels_ClientId",
                table: "CanteenGatewayChannels",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenGatewayChannels_GatewayConfigId",
                table: "CanteenGatewayChannels",
                column: "GatewayConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenGatewayChannels_GatewayConfigId_ChannelCode",
                table: "CanteenGatewayChannels",
                columns: new[] { "GatewayConfigId", "ChannelCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanteenNfcCards_CardUid",
                table: "CanteenNfcCards",
                column: "CardUid");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenNfcCards_ClientId",
                table: "CanteenNfcCards",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenNfcCards_UserId_UserType",
                table: "CanteenNfcCards",
                columns: new[] { "UserId", "UserType" });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenNotificationLog_Channel_Status",
                table: "CanteenNotificationLog",
                columns: new[] { "Channel", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenNotificationLog_ClientId",
                table: "CanteenNotificationLog",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenNotificationLog_OccurredAtUtc",
                table: "CanteenNotificationLog",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrderItems_ClientId",
                table: "CanteenOrderItems",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrderItems_FoodItemID",
                table: "CanteenOrderItems",
                column: "FoodItemID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrderItems_OrderID",
                table: "CanteenOrderItems",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_ClientId",
                table: "CanteenOrders",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_IdempotencyKey",
                table: "CanteenOrders",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_OrderDate_Status",
                table: "CanteenOrders",
                columns: new[] { "OrderDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_OrderNumber",
                table: "CanteenOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanteenOrders_UserId_UserType",
                table: "CanteenOrders",
                columns: new[] { "UserId", "UserType" });

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentGatewayConfigs_ClientId",
                table: "CanteenPaymentGatewayConfigs",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentGatewayConfigs_GatewayCode",
                table: "CanteenPaymentGatewayConfigs",
                column: "GatewayCode");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentGatewayConfigs_Method",
                table: "CanteenPaymentGatewayConfigs",
                column: "Method");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentTransactions_ClientId",
                table: "CanteenPaymentTransactions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentTransactions_Status",
                table: "CanteenPaymentTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentTransactions_TransactionRef",
                table: "CanteenPaymentTransactions",
                column: "TransactionRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanteenPaymentTransactions_UserId",
                table: "CanteenPaymentTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenTenantSettings_ClientId",
                table: "CanteenTenantSettings",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenTenantSettings_Key",
                table: "CanteenTenantSettings",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserBalances_ClientId",
                table: "CanteenUserBalances",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenUserBalances_UserId_UserType",
                table: "CanteenUserBalances",
                columns: new[] { "UserId", "UserType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWalletLedger_ClientId",
                table: "CanteenWalletLedger",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWalletLedger_IdempotencyKey",
                table: "CanteenWalletLedger",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWalletLedger_OrderID",
                table: "CanteenWalletLedger",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWalletLedger_UserId",
                table: "CanteenWalletLedger",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWeeklyMenuTemplates_ClientId",
                table: "CanteenWeeklyMenuTemplates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CanteenWeeklyMenuTemplates_FoodItemID",
                table: "CanteenWeeklyMenuTemplates",
                column: "FoodItemID");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ClientCode",
                table: "Clients",
                column: "ClientCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncRuns_ClientId",
                table: "DirectorySyncRuns",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncRuns_StartedAtUtc",
                table: "DirectorySyncRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DirectorySyncRuns_Status",
                table: "DirectorySyncRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CardIdentifier",
                table: "Employees",
                column: "CardIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ClientId",
                table: "Employees",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ClientId_ExternalId",
                table: "Employees",
                columns: new[] { "ClientId", "ExternalId" },
                unique: true,
                filter: "[ClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ExternalId",
                table: "Employees",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_CardIdentifier",
                table: "Students",
                column: "CardIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClientId",
                table: "Students",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClientId_ExternalId",
                table: "Students",
                columns: new[] { "ClientId", "ExternalId" },
                unique: true,
                filter: "[ClientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ExternalId",
                table: "Students",
                column: "ExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppApiKeys");

            migrationBuilder.DropTable(
                name: "AppRefreshTokens");

            migrationBuilder.DropTable(
                name: "AppRolePermissions");

            migrationBuilder.DropTable(
                name: "AppUserRoles");

            migrationBuilder.DropTable(
                name: "CanteenAuditEntries");

            migrationBuilder.DropTable(
                name: "CanteenCardEvents");

            migrationBuilder.DropTable(
                name: "CanteenDailyMenus");

            migrationBuilder.DropTable(
                name: "CanteenGatewayChannels");

            migrationBuilder.DropTable(
                name: "CanteenNotificationLog");

            migrationBuilder.DropTable(
                name: "CanteenOrderItems");

            migrationBuilder.DropTable(
                name: "CanteenPaymentTransactions");

            migrationBuilder.DropTable(
                name: "CanteenTenantSettings");

            migrationBuilder.DropTable(
                name: "CanteenWalletLedger");

            migrationBuilder.DropTable(
                name: "CanteenWeeklyMenuTemplates");

            migrationBuilder.DropTable(
                name: "DirectorySyncRuns");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "AppPermissions");

            migrationBuilder.DropTable(
                name: "AppRoles");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "CanteenNfcCards");

            migrationBuilder.DropTable(
                name: "CanteenPaymentGatewayConfigs");

            migrationBuilder.DropTable(
                name: "CanteenOrders");

            migrationBuilder.DropTable(
                name: "CanteenFoodItems");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "CanteenUserBalances");
        }
    }
}
