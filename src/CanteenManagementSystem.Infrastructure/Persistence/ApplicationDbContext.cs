using Platform.Application.Persistence;
using Platform.Domain.Audit;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using Platform.Domain.Tenancy;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IAppDbContext
{
    private readonly ITenantContext? _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    private int CurrentClientId => _tenant?.ClientId ?? 0;

    public DbSet<Client> Clients => Set<Client>();

    // Directory (canteen-owned, populated by DirectorySyncService) ---------
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();
    public DbSet<Platform.Domain.Directory.DirectorySyncRun> DirectorySyncRuns => Set<Platform.Domain.Directory.DirectorySyncRun>();

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<UserBalance> UserBalances => Set<UserBalance>();
    public DbSet<DailyMenu> DailyMenus => Set<DailyMenu>();
    public DbSet<WeeklyMenuTemplate> WeeklyMenuTemplates => Set<WeeklyMenuTemplate>();
    public DbSet<WalletLedger> WalletLedger => Set<WalletLedger>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<CanteenManagementSystem.Domain.Cards.NfcCard> NfcCards => Set<CanteenManagementSystem.Domain.Cards.NfcCard>();
    public DbSet<CanteenManagementSystem.Domain.Cards.CardEvent> CardEvents => Set<CanteenManagementSystem.Domain.Cards.CardEvent>();
    public DbSet<Platform.Domain.Payments.PaymentTransaction> PaymentTransactions => Set<Platform.Domain.Payments.PaymentTransaction>();
    public DbSet<Platform.Domain.Payments.PaymentGatewayConfig> PaymentGatewayConfigs => Set<Platform.Domain.Payments.PaymentGatewayConfig>();
    public DbSet<Platform.Domain.Payments.GatewayChannel> GatewayChannels => Set<Platform.Domain.Payments.GatewayChannel>();
    public DbSet<Platform.Domain.Notifications.NotificationLog> NotificationLogs => Set<Platform.Domain.Notifications.NotificationLog>();
    public DbSet<Platform.Domain.Identity.User> AppUsers => Set<Platform.Domain.Identity.User>();
    public DbSet<Platform.Domain.Identity.Role> AppRoles => Set<Platform.Domain.Identity.Role>();
    public DbSet<Platform.Domain.Identity.Permission> AppPermissions => Set<Platform.Domain.Identity.Permission>();
    public DbSet<Platform.Domain.Identity.UserRole> AppUserRoles => Set<Platform.Domain.Identity.UserRole>();
    public DbSet<Platform.Domain.Identity.RolePermission> AppRolePermissions => Set<Platform.Domain.Identity.RolePermission>();
    public DbSet<Platform.Domain.Identity.RefreshToken> AppRefreshTokens => Set<Platform.Domain.Identity.RefreshToken>();
    public DbSet<Platform.Domain.Identity.ApiKey> AppApiKeys => Set<Platform.Domain.Identity.ApiKey>();
    public DbSet<Platform.Domain.Identity.MfaRecoveryCode> AppMfaRecoveryCodes => Set<Platform.Domain.Identity.MfaRecoveryCode>();
    public DbSet<Platform.Domain.Identity.KioskDevice> AppKioskDevices => Set<Platform.Domain.Identity.KioskDevice>();
    public DbSet<Platform.Domain.Identity.AuthToken> AppAuthTokens => Set<Platform.Domain.Identity.AuthToken>();

    public DbSet<Platform.Domain.Reporting.ReportSchedule> ReportSchedules => Set<Platform.Domain.Reporting.ReportSchedule>();

    // ─── Loyalty (P2) ─────────────────────────────────────────────────────
    public DbSet<Platform.Domain.Loyalty.LoyaltyAccount> LoyaltyAccounts => Set<Platform.Domain.Loyalty.LoyaltyAccount>();
    public DbSet<Platform.Domain.Loyalty.LoyaltyEntry>   LoyaltyEntries  => Set<Platform.Domain.Loyalty.LoyaltyEntry>();

    // ─── Pricing rules (P2) ───────────────────────────────────────────────
    public DbSet<Platform.Domain.Pricing.DiscountRule>   DiscountRules   => Set<Platform.Domain.Pricing.DiscountRule>();

    // ─── Webhooks (outbound) ──────────────────────────────────────────────
    public DbSet<Platform.Domain.Webhooks.WebhookSubscription> WebhookSubscriptions => Set<Platform.Domain.Webhooks.WebhookSubscription>();
    public DbSet<Platform.Domain.Webhooks.WebhookDelivery>     WebhookDeliveries    => Set<Platform.Domain.Webhooks.WebhookDelivery>();

    // ─── Counter combos + inventory operations ────────────────────────────
    public DbSet<ComboButton> ComboButtons => Set<ComboButton>();
    public DbSet<CanteenManagementSystem.Domain.Inventory.Supplier>          Suppliers          => Set<CanteenManagementSystem.Domain.Inventory.Supplier>();
    public DbSet<CanteenManagementSystem.Domain.Inventory.PurchaseOrder>     PurchaseOrders     => Set<CanteenManagementSystem.Domain.Inventory.PurchaseOrder>();
    public DbSet<CanteenManagementSystem.Domain.Inventory.PurchaseOrderLine> PurchaseOrderLines => Set<CanteenManagementSystem.Domain.Inventory.PurchaseOrderLine>();
    public DbSet<CanteenManagementSystem.Domain.Inventory.StockTake>         StockTakes         => Set<CanteenManagementSystem.Domain.Inventory.StockTake>();
    public DbSet<CanteenManagementSystem.Domain.Inventory.StockTakeLine>     StockTakeLines     => Set<CanteenManagementSystem.Domain.Inventory.StockTakeLine>();
    public DbSet<CanteenManagementSystem.Domain.Inventory.WasteLog>          WasteLogs          => Set<CanteenManagementSystem.Domain.Inventory.WasteLog>();

    // ─── Web push subscriptions ───────────────────────────────────────────
    public DbSet<Platform.Domain.Notifications.PushSubscription> PushSubscriptions => Set<Platform.Domain.Notifications.PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Client>().HasKey(c => c.ClientId);
        modelBuilder.Entity<Client>().HasIndex(c => c.ClientCode).IsUnique();

        // ─── Directory (synced tables) ────────────────────────────────────
        modelBuilder.Entity<Student>().HasKey(s => s.StudentId);
        modelBuilder.Entity<Student>().Property(s => s.RowVersion).IsRowVersion();
        modelBuilder.Entity<Student>().HasIndex(s => s.ExternalId);
        modelBuilder.Entity<Student>().HasIndex(s => s.CardIdentifier);

        modelBuilder.Entity<Employee>().HasKey(e => e.EmployeeId);
        modelBuilder.Entity<Employee>().Property(e => e.RowVersion).IsRowVersion();
        modelBuilder.Entity<Employee>().HasIndex(e => e.ExternalId);
        modelBuilder.Entity<Employee>().HasIndex(e => e.CardIdentifier);

        modelBuilder.Entity<Platform.Domain.Directory.DirectorySyncRun>().HasKey(r => r.SyncRunId);
        modelBuilder.Entity<Platform.Domain.Directory.DirectorySyncRun>().HasIndex(r => r.StartedAtUtc);
        modelBuilder.Entity<Platform.Domain.Directory.DirectorySyncRun>().HasIndex(r => r.Status);

        modelBuilder.Entity<UserBalance>().HasKey(ub => ub.BalanceID);
        modelBuilder.Entity<UserBalance>()
            .HasIndex(ub => new { ub.UserId, ub.UserType })
            .IsUnique();

        modelBuilder.Entity<Order>()
            .HasOne(o => o.UserBalance)
            .WithMany(ub => ub.Orders)
            .HasForeignKey(o => new { o.UserId, o.UserType })
            .HasPrincipalKey(ub => new { ub.UserId, ub.UserType })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.FoodItem)
            .WithMany()
            .HasForeignKey(oi => oi.FoodItemID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyMenu>()
            .HasOne(dm => dm.FoodItem)
            .WithMany()
            .HasForeignKey(dm => dm.FoodItemID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WeeklyMenuTemplate>()
            .HasOne(wmt => wmt.FoodItem)
            .WithMany()
            .HasForeignKey(wmt => wmt.FoodItemID)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();
        modelBuilder.Entity<Order>().HasIndex(o => new { o.OrderDate, o.Status });
        modelBuilder.Entity<DailyMenu>().HasIndex(dm => dm.MenuDate);

        modelBuilder.Entity<Order>().Property(o => o.TotalAmount).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().Property(oi => oi.UnitPrice).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().Property(oi => oi.TotalPrice).HasPrecision(10, 2);

        modelBuilder.Entity<UserBalance>().Property(ub => ub.TotalBalance).HasPrecision(10, 2);
        modelBuilder.Entity<UserBalance>().Property(ub => ub.UsedBalance).HasPrecision(10, 2);
        modelBuilder.Entity<UserBalance>().Property(ub => ub.BlockedAmount).HasPrecision(10, 2);
        modelBuilder.Entity<UserBalance>().Property(ub => ub.EmergencyEntitlement).HasPrecision(10, 2);
        modelBuilder.Entity<UserBalance>().Property(ub => ub.EmergencyUsed).HasPrecision(10, 2);

        modelBuilder.Entity<FoodItem>().Property(f => f.Price).HasPrecision(10, 2);

        modelBuilder.Entity<Order>().Property(o => o.RowVersion).IsRowVersion();
        modelBuilder.Entity<UserBalance>().Property(ub => ub.RowVersion).IsRowVersion();
        modelBuilder.Entity<DailyMenu>().Property(dm => dm.RowVersion).IsRowVersion();

        modelBuilder.Entity<WalletLedger>().HasKey(l => l.LedgerID);
        modelBuilder.Entity<WalletLedger>().HasIndex(l => l.UserId);
        modelBuilder.Entity<WalletLedger>().HasIndex(l => l.OrderID);
        modelBuilder.Entity<WalletLedger>().HasIndex(l => l.IdempotencyKey);
        modelBuilder.Entity<WalletLedger>().Property(l => l.Amount).HasPrecision(10, 2);
        modelBuilder.Entity<WalletLedger>().Property(l => l.BalanceAfter).HasPrecision(10, 2);
        modelBuilder.Entity<WalletLedger>().Property(l => l.BlockedAfter).HasPrecision(10, 2);

        modelBuilder.Entity<AuditEntry>().HasKey(a => a.AuditID);
        modelBuilder.Entity<AuditEntry>().HasIndex(a => a.OccurredAtUtc);
        modelBuilder.Entity<AuditEntry>().HasIndex(a => a.EntityType);
        modelBuilder.Entity<AuditEntry>().HasIndex(a => a.PerformedBy);

        modelBuilder.Entity<Order>().HasIndex(o => o.IdempotencyKey);

        modelBuilder.Entity<TenantSetting>().HasKey(s => s.SettingId);
        modelBuilder.Entity<TenantSetting>().HasIndex(s => s.Key);

        modelBuilder.Entity<CanteenManagementSystem.Domain.Cards.NfcCard>().HasKey(c => c.CardId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Cards.NfcCard>().HasIndex(c => c.CardUid);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Cards.NfcCard>().HasIndex(c => new { c.UserId, c.UserType });
        modelBuilder.Entity<CanteenManagementSystem.Domain.Cards.NfcCard>().Property(c => c.RowVersion).IsRowVersion();
        modelBuilder.Entity<CanteenManagementSystem.Domain.Cards.CardEvent>().HasKey(e => e.CardEventId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Cards.CardEvent>().HasIndex(e => e.CardId);

        modelBuilder.Entity<Platform.Domain.Payments.PaymentTransaction>().HasKey(p => p.PaymentId);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentTransaction>().HasIndex(p => p.TransactionRef).IsUnique();
        modelBuilder.Entity<Platform.Domain.Payments.PaymentTransaction>().HasIndex(p => p.UserId);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentTransaction>().HasIndex(p => p.Status);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentTransaction>().Property(p => p.Amount).HasPrecision(10, 2);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentTransaction>().Property(p => p.RowVersion).IsRowVersion();

        modelBuilder.Entity<Platform.Domain.Payments.PaymentGatewayConfig>().HasKey(c => c.GatewayConfigId);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentGatewayConfig>().HasIndex(c => c.GatewayCode);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentGatewayConfig>().HasIndex(c => c.Method);
        modelBuilder.Entity<Platform.Domain.Payments.PaymentGatewayConfig>().Property(c => c.RowVersion).IsRowVersion();

        modelBuilder.Entity<Platform.Domain.Payments.GatewayChannel>().HasKey(c => c.ChannelId);
        modelBuilder.Entity<Platform.Domain.Payments.GatewayChannel>().HasIndex(c => c.GatewayConfigId);
        modelBuilder.Entity<Platform.Domain.Payments.GatewayChannel>().HasIndex(c => new { c.GatewayConfigId, c.ChannelCode }).IsUnique();
        modelBuilder.Entity<Platform.Domain.Payments.GatewayChannel>()
            .HasOne(c => c.GatewayConfig)
            .WithMany(g => g.Channels)
            .HasForeignKey(c => c.GatewayConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Platform.Domain.Notifications.NotificationLog>().HasKey(n => n.NotificationId);
        modelBuilder.Entity<Platform.Domain.Notifications.NotificationLog>().HasIndex(n => n.OccurredAtUtc);
        modelBuilder.Entity<Platform.Domain.Notifications.NotificationLog>().HasIndex(n => new { n.Channel, n.Status });

        modelBuilder.Entity<Platform.Domain.Notifications.UserNotificationPreference>().HasKey(p => p.PreferenceId);
        modelBuilder.Entity<Platform.Domain.Notifications.UserNotificationPreference>()
            .HasIndex(p => new { p.UserId, p.TemplateKey, p.Channel });

        // ─── User favourites (re-order with one tap from /me/orders) ─────
        modelBuilder.Entity<UserFavorite>().HasKey(f => f.FavoriteId);
        modelBuilder.Entity<UserFavorite>()
            .HasOne(f => f.FoodItem)
            .WithMany()
            .HasForeignKey(f => f.FoodItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserFavorite>()
            .HasIndex(f => new { f.UserId, f.FoodItemId })
            .IsUnique();

        // ─── Combo quick buttons ─────────────────────────────────────────
        modelBuilder.Entity<ComboButton>().HasKey(c => c.ComboButtonId);
        modelBuilder.Entity<ComboButton>().HasIndex(c => c.Code);

        // ─── Inventory operations entities ───────────────────────────────
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.Supplier>().HasKey(s => s.SupplierId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.Supplier>().HasIndex(s => s.Name);

        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.PurchaseOrder>().HasKey(p => p.PurchaseOrderId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.PurchaseOrder>().HasIndex(p => p.PoNumber);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.PurchaseOrder>()
            .HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.PurchaseOrderLine>().HasKey(l => l.PurchaseOrderLineId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.PurchaseOrderLine>()
            .HasOne(l => l.PurchaseOrder).WithMany(p => p.Lines).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.PurchaseOrderLine>()
            .HasOne(l => l.FoodItem).WithMany().HasForeignKey(l => l.FoodItemID).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.StockTake>().HasKey(s => s.StockTakeId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.StockTakeLine>().HasKey(l => l.StockTakeLineId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.StockTakeLine>()
            .HasOne(l => l.StockTake).WithMany(s => s.Lines).HasForeignKey(l => l.StockTakeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.StockTakeLine>()
            .HasOne(l => l.FoodItem).WithMany().HasForeignKey(l => l.FoodItemID).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.StockTakeLine>().Ignore(l => l.VarianceQty);

        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.WasteLog>().HasKey(w => w.WasteLogId);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.WasteLog>().HasIndex(w => w.OccurredAtUtc);
        modelBuilder.Entity<CanteenManagementSystem.Domain.Inventory.WasteLog>()
            .HasOne(w => w.FoodItem).WithMany().HasForeignKey(w => w.FoodItemID).OnDelete(DeleteBehavior.Restrict);

        // ─── Web push subscriptions ──────────────────────────────────────
        modelBuilder.Entity<Platform.Domain.Notifications.PushSubscription>().HasKey(p => p.PushSubscriptionId);
        modelBuilder.Entity<Platform.Domain.Notifications.PushSubscription>()
            .HasIndex(p => new { p.UserId, p.Endpoint }).IsUnique();

        // -------------------- Identity -----------------------------------
        modelBuilder.Entity<Platform.Domain.Identity.User>().HasKey(u => u.UserId);
        modelBuilder.Entity<Platform.Domain.Identity.User>().HasIndex(u => u.UserName).IsUnique();
        modelBuilder.Entity<Platform.Domain.Identity.User>().HasIndex(u => u.ClientId);
        modelBuilder.Entity<Platform.Domain.Identity.User>().Property(u => u.RowVersion).IsRowVersion();
        modelBuilder.Entity<Platform.Domain.Identity.User>()
            .HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Platform.Domain.Identity.Role>().HasKey(r => r.RoleId);
        modelBuilder.Entity<Platform.Domain.Identity.Role>().HasIndex(r => r.RoleCode);

        modelBuilder.Entity<Platform.Domain.Identity.Permission>().HasKey(p => p.PermissionId);
        modelBuilder.Entity<Platform.Domain.Identity.Permission>().HasIndex(p => p.PermissionCode).IsUnique();

        modelBuilder.Entity<Platform.Domain.Identity.UserRole>().HasKey(ur => ur.UserRoleId);
        modelBuilder.Entity<Platform.Domain.Identity.UserRole>().HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();

        modelBuilder.Entity<Platform.Domain.Identity.RolePermission>().HasKey(rp => rp.RolePermissionId);
        modelBuilder.Entity<Platform.Domain.Identity.RolePermission>().HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

        modelBuilder.Entity<Platform.Domain.Identity.RefreshToken>().HasKey(t => t.RefreshTokenId);
        modelBuilder.Entity<Platform.Domain.Identity.RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();
        modelBuilder.Entity<Platform.Domain.Identity.RefreshToken>().HasIndex(t => t.UserId);

        modelBuilder.Entity<Platform.Domain.Identity.ApiKey>().HasKey(k => k.ApiKeyId);
        modelBuilder.Entity<Platform.Domain.Identity.ApiKey>().HasIndex(k => k.AppKey).IsUnique();

        // MFA recovery codes — stored as PBKDF2 hashes; one-shot consumption.
        modelBuilder.Entity<Platform.Domain.Identity.MfaRecoveryCode>().HasKey(c => c.RecoveryCodeId);
        modelBuilder.Entity<Platform.Domain.Identity.MfaRecoveryCode>().HasIndex(c => c.UserId);
        modelBuilder.Entity<Platform.Domain.Identity.MfaRecoveryCode>().HasIndex(c => c.CodeHash);
        modelBuilder.Entity<Platform.Domain.Identity.MfaRecoveryCode>()
            .HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Paired self-service kiosk tablets. Each row is one device; the raw
        // token is hashed in the same PBKDF2 scheme as MFA recovery codes.
        modelBuilder.Entity<Platform.Domain.Identity.KioskDevice>().HasKey(d => d.KioskDeviceId);
        modelBuilder.Entity<Platform.Domain.Identity.KioskDevice>().HasIndex(d => d.IsActive);
        modelBuilder.Entity<Platform.Domain.Identity.KioskDevice>().HasIndex(d => d.TokenHash);

        // Short-lived single-use auth tokens (password reset, magic link, email verify).
        modelBuilder.Entity<Platform.Domain.Identity.AuthToken>().HasKey(t => t.AuthTokenId);
        modelBuilder.Entity<Platform.Domain.Identity.AuthToken>().HasIndex(t => new { t.UserId, t.Purpose });
        modelBuilder.Entity<Platform.Domain.Identity.AuthToken>().HasIndex(t => t.TokenHash);
        modelBuilder.Entity<Platform.Domain.Identity.AuthToken>()
            .HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // -------------------- Report schedules ---------------------------
        modelBuilder.Entity<Platform.Domain.Reporting.ReportSchedule>().HasKey(s => s.ScheduleId);
        modelBuilder.Entity<Platform.Domain.Reporting.ReportSchedule>().HasIndex(s => s.ReportKey);
        modelBuilder.Entity<Platform.Domain.Reporting.ReportSchedule>().HasIndex(s => s.NextRunAtUtc);

        // -------------------- Loyalty ------------------------------------
        modelBuilder.Entity<Platform.Domain.Loyalty.LoyaltyAccount>().HasKey(a => a.LoyaltyAccountId);
        modelBuilder.Entity<Platform.Domain.Loyalty.LoyaltyAccount>().HasIndex(a => a.UserExternalId);
        modelBuilder.Entity<Platform.Domain.Loyalty.LoyaltyEntry>().HasKey(e => e.LoyaltyEntryId);
        modelBuilder.Entity<Platform.Domain.Loyalty.LoyaltyEntry>().HasIndex(e => e.LoyaltyAccountId);
        modelBuilder.Entity<Platform.Domain.Loyalty.LoyaltyEntry>().HasIndex(e => e.OrderId);

        // -------------------- Discount rules -----------------------------
        modelBuilder.Entity<Platform.Domain.Pricing.DiscountRule>().HasKey(r => r.RuleId);
        modelBuilder.Entity<Platform.Domain.Pricing.DiscountRule>().HasIndex(r => r.RuleCode);
        modelBuilder.Entity<Platform.Domain.Pricing.DiscountRule>().HasIndex(r => new { r.IsActive, r.Priority });

        // -------------------- Webhook outbound ---------------------------
        modelBuilder.Entity<Platform.Domain.Webhooks.WebhookSubscription>().HasKey(s => s.SubscriptionId);
        modelBuilder.Entity<Platform.Domain.Webhooks.WebhookSubscription>().HasIndex(s => s.IsActive);
        modelBuilder.Entity<Platform.Domain.Webhooks.WebhookDelivery>().HasKey(d => d.DeliveryId);
        modelBuilder.Entity<Platform.Domain.Webhooks.WebhookDelivery>().HasIndex(d => d.SubscriptionId);
        modelBuilder.Entity<Platform.Domain.Webhooks.WebhookDelivery>().HasIndex(d => new { d.Status, d.NextRetryAtUtc });
        modelBuilder.Entity<Platform.Domain.Webhooks.WebhookDelivery>()
            .HasOne(d => d.Subscription).WithMany().HasForeignKey(d => d.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        ApplyTenantScoping(modelBuilder);

        // Composite unique indexes that reference the ClientId shadow column.
        // Must run AFTER ApplyTenantScoping so the property exists.
        modelBuilder.Entity<Student>().HasIndex("ClientId", nameof(Student.ExternalId)).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex("ClientId", nameof(Employee.ExternalId)).IsUnique();
    }

    private void ApplyTenantScoping(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Nullable shadow column. NULL means "legacy / unscoped" so existing
            // rows survive the additive migration.
            modelBuilder.Entity(entityType.ClrType).Property<int?>("ClientId");
            modelBuilder.Entity(entityType.ClrType).HasIndex("ClientId");

            // Filter: pass through when the tenant isn't resolved (CurrentClientId == 0)
            // or when the row has no ClientId (legacy data).
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var clientIdAccess = System.Linq.Expressions.Expression.Call(
                typeof(EF), nameof(EF.Property), new[] { typeof(int?) },
                parameter, System.Linq.Expressions.Expression.Constant("ClientId"));

            var currentClientIdAccess = System.Linq.Expressions.Expression.Property(
                System.Linq.Expressions.Expression.Constant(this), nameof(CurrentClientId));

            var nullableCurrent = System.Linq.Expressions.Expression.Convert(currentClientIdAccess, typeof(int?));
            var zeroConstant = System.Linq.Expressions.Expression.Constant(0);
            var nullConstant = System.Linq.Expressions.Expression.Constant(null, typeof(int?));

            // (CurrentClientId == 0) || (ClientId == null) || (ClientId == CurrentClientId)
            var isUnresolved = System.Linq.Expressions.Expression.Equal(currentClientIdAccess, zeroConstant);
            var isLegacyRow = System.Linq.Expressions.Expression.Equal(clientIdAccess, nullConstant);
            var matchesTenant = System.Linq.Expressions.Expression.Equal(clientIdAccess, nullableCurrent);

            var body = System.Linq.Expressions.Expression.OrElse(
                System.Linq.Expressions.Expression.OrElse(isUnresolved, isLegacyRow),
                matchesTenant);

            var lambda = System.Linq.Expressions.Expression.Lambda(body, parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    public override int SaveChanges() => SaveChangesInternal(() => base.SaveChanges());

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesInternalAsync(() => base.SaveChangesAsync(cancellationToken));

    private int SaveChangesInternal(Func<int> save)
    {
        StampTenantOnAddedEntries();
        return save();
    }

    private Task<int> SaveChangesInternalAsync(Func<Task<int>> save)
    {
        StampTenantOnAddedEntries();
        return save();
    }

    private void StampTenantOnAddedEntries()
    {
        if (CurrentClientId == 0) return;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified) continue;
            if (entry.Entity is not ITenantOwned) continue;
            if (entry.Metadata.FindProperty("ClientId") is null) continue;

            var current = entry.Property("ClientId").CurrentValue;

            if (entry.State == EntityState.Added && current is null)
            {
                entry.Property("ClientId").CurrentValue = CurrentClientId;
                continue;
            }

            // Cross-tenant write safeguard: a tracked entity whose ClientId
            // disagrees with the current tenant context indicates either a
            // bug in repository code or a hostile attempt to write across
            // tenants. Hard fail — better to surface the bug than to let
            // data leak. SystemAdmins acting via impersonation still hit
            // this check because impersonation flips the tenant context to
            // the target tenant before the write.
            if (current is int existing && existing != 0 && existing != CurrentClientId)
            {
                throw new InvalidOperationException(
                    $"Tenant isolation violation: tried to write entity of type " +
                    $"'{entry.Metadata.ClrType.Name}' owned by tenant {existing} " +
                    $"while current tenant is {CurrentClientId}. " +
                    $"This is a critical bug — review the calling code.");
            }
        }
    }
}
