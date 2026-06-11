using Platform.Application.Persistence;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Abstractions.Directory;
using Platform.Infrastructure.Directory;
using CanteenManagementSystem.Infrastructure.Directory;
using CanteenManagementSystem.Infrastructure.Seeding;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Inventory;
using Platform.Application.Abstractions.Receipts;
using Platform.Application.Abstractions.Time;
using Platform.Application.Abstractions.Users;
using Platform.Application.Abstractions.Wallets;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Audit;
using Platform.Infrastructure.BackgroundJobs;
using CanteenManagementSystem.Infrastructure.BackgroundJobs;
using Platform.Infrastructure.Caching;
using Platform.Infrastructure.Common.Time;
using Platform.Infrastructure.Configuration;
using CanteenManagementSystem.Infrastructure.Inventory;
using CanteenManagementSystem.Infrastructure.Persistence;
using CanteenManagementSystem.Infrastructure.Receipts;
using Platform.Infrastructure.Tenancy;
using CanteenManagementSystem.Infrastructure.Users;
using CanteenManagementSystem.Infrastructure.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenManagementSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCanteenInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped(typeof(IRepository<>),         typeof(GenericRepository<>));
        services.AddScoped(typeof(IReadOnlyRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<Platform.Application.Abstractions.Audit.IAuditQueryService,
                           Platform.Infrastructure.Audit.AuditQueryService>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        services.AddScoped<IDistributedCacheService, TenantAwareDistributedCacheService>();
        services.AddScoped<CanteenManagementSystem.Application.Sessions.IOrderSessionStore,
                           CanteenManagementSystem.Infrastructure.Sessions.DistributedCacheOrderSessionStore>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<ITenantSettings, TenantSettingsService>();
        services.AddScoped<Platform.Application.Abstractions.Branding.IBrandingResolver,
                           Platform.Infrastructure.Branding.BrandingResolver>();
        services.AddScoped<IReceiptService, QuestPdfReceiptService>();
        services.AddScoped<Platform.Application.Abstractions.Receipts.INetworkReceiptPrinter,
                           CanteenManagementSystem.Infrastructure.Receipts.EscPosNetworkPrinter>();
        // NBR e-receipt:
        //   * StubNbrReceiptService     — default; no-ops unless Nbr.Enabled, no real gateway call.
        //   * NbrReceiptServiceProduction — flip this once tenant has NBR sandbox creds.
        services.AddScoped<Platform.Application.Abstractions.Receipts.INbrReceiptService,
                           CanteenManagementSystem.Infrastructure.Receipts.StubNbrReceiptService>();
        services.AddHttpClient("nbr.gateway", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<Platform.Application.Abstractions.Loyalty.ILoyaltyService,
                           CanteenManagementSystem.Infrastructure.Loyalty.LoyaltyService>();
        services.AddScoped<Platform.Application.Abstractions.Pricing.IDiscountEngine,
                           Platform.Infrastructure.Pricing.DiscountEngine>();
        services.AddScoped<Platform.Application.Abstractions.Pricing.IDiscountRuleAdminService,
                           Platform.Infrastructure.Pricing.DiscountRuleAdminService>();
        services.AddScoped<CanteenManagementSystem.Application.Cards.ICardAdminService,
                           CanteenManagementSystem.Infrastructure.Cards.CardAdminService>();
        services.AddScoped<CanteenManagementSystem.Application.Menus.IPublicMenuService,
                           CanteenManagementSystem.Infrastructure.Menus.PublicMenuService>();
        services.AddScoped<CanteenManagementSystem.Application.Me.IMeService,
                           CanteenManagementSystem.Infrastructure.Me.MeService>();
        services.AddScoped<CanteenManagementSystem.Application.Orders.ITableOrderService,
                           CanteenManagementSystem.Infrastructure.Orders.TableOrderService>();
        services.AddScoped<CanteenManagementSystem.Application.Orders.IPreOrderService,
                           CanteenManagementSystem.Infrastructure.Orders.PreOrderService>();
        services.AddScoped<CanteenManagementSystem.Application.Parents.IParentPortalService,
                           CanteenManagementSystem.Infrastructure.Parents.ParentPortalService>();
        services.AddScoped<CanteenManagementSystem.Application.Verifications.IVerificationSessionService,
                           CanteenManagementSystem.Infrastructure.Verifications.VerificationSessionService>();
        services.AddScoped<CanteenManagementSystem.Application.Directory.IDirectoryAdminQueryService,
                           CanteenManagementSystem.Infrastructure.Directory.DirectoryAdminQueryService>();

        // ─── Webhook outbound ─────────────────────────────────────────────
        services.AddScoped<Platform.Application.Abstractions.Webhooks.IWebhookPublisher,
                           Platform.Infrastructure.Webhooks.WebhookPublisher>();
        services.AddScoped<Platform.Application.Abstractions.Webhooks.IWebhookAdminService,
                           Platform.Infrastructure.Webhooks.WebhookAdminService>();
        services.AddHttpClient("webhook.dispatcher", c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddHostedService<Platform.Infrastructure.Webhooks.WebhookDispatcherBackgroundService>();

        // ─── Audit log retention (daily tick, per-tenant) ─────────────────
        services.AddHostedService<Platform.Infrastructure.BackgroundJobs.AuditRetentionService>();

        // ─── Payment callback retry (catches stuck Pending payments) ──────
        services.AddHostedService<Platform.Infrastructure.BackgroundJobs.PaymentCallbackRetryService>();

        // ─── Tenant hard-delete sweeper (runs once an hour) ───────────────
        services.AddHostedService<CanteenManagementSystem.Infrastructure.BackgroundJobs.TenantHardDeleteService>();

        // ─── Combos, inventory module, tenant deletion, billing, web push ─
        services.AddScoped<CanteenManagementSystem.Application.Menus.IComboButtonService,
                           CanteenManagementSystem.Infrastructure.Menus.ComboButtonService>();
        services.AddScoped<CanteenManagementSystem.Application.Inventory.IInventoryAdminService,
                           CanteenManagementSystem.Infrastructure.Inventory.InventoryAdminService>();
        services.AddScoped<CanteenManagementSystem.Application.Tenancy.ITenantDeletionService,
                           CanteenManagementSystem.Infrastructure.Tenancy.TenantDeletionService>();
        services.AddScoped<Platform.Application.Abstractions.Billing.IBillingService,
                           Platform.Infrastructure.Billing.StubBillingService>();
        services.AddScoped<Platform.Application.Abstractions.Notifications.IWebPushService,
                           Platform.Infrastructure.Notifications.WebPushService>();
        services.AddHttpClient("webpush.dispatcher", c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<Platform.Application.Abstractions.Reports.IReportingService,
                           CanteenManagementSystem.Infrastructure.Reports.ReportingService>();

        // ─── Report catalog (IReport implementations) ──────────────────────
        // Each registered IReport shows up automatically on /admin/reports-catalog.
        // Sister products on the platform register their own reports the same way.
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.DailyCollectionReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.PopularItemsReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.AuditTrailReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.ShiftSummaryReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.WalletBalanceDistributionReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.RefundVoidLogReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.TopSpendersReport>();

        // Redis distributed cache: opts in when a connection string is configured.
        // Falls back to in-memory distributed cache otherwise — keeps dev simple.
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "canteen:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // -------------------- Payments --------------------------------------
        services.AddHttpClient<Platform.Infrastructure.Payments.Bkash.BkashPaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<Platform.Infrastructure.Payments.Nagad.NagadPaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<Platform.Infrastructure.Payments.SslCommerz.SslCommerzPaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<Platform.Infrastructure.Payments.Stripe.StripePaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<Platform.Application.Abstractions.Payments.IPaymentGateway>(sp => sp.GetRequiredService<Platform.Infrastructure.Payments.Bkash.BkashPaymentGateway>());
        services.AddScoped<Platform.Application.Abstractions.Payments.IPaymentGateway>(sp => sp.GetRequiredService<Platform.Infrastructure.Payments.Nagad.NagadPaymentGateway>());
        services.AddScoped<Platform.Application.Abstractions.Payments.IPaymentGateway>(sp => sp.GetRequiredService<Platform.Infrastructure.Payments.SslCommerz.SslCommerzPaymentGateway>());
        services.AddScoped<Platform.Application.Abstractions.Payments.IPaymentGateway>(sp => sp.GetRequiredService<Platform.Infrastructure.Payments.Stripe.StripePaymentGateway>());
        services.AddScoped<Platform.Application.Abstractions.Payments.IPaymentOrchestrator,
                           Platform.Infrastructure.Payments.PaymentOrchestrator>();
        services.AddScoped<Platform.Application.Abstractions.Payments.IGatewayConfigService,
                           Platform.Infrastructure.Payments.GatewayConfigService>();
        services.AddScoped<Platform.Application.Abstractions.Payments.IGatewayAdminService,
                           Platform.Infrastructure.Payments.GatewayAdminService>();

        // -------------------- Notifications ---------------------------------
        services.AddScoped<Platform.Application.Abstractions.Notifications.INotificationService,
                           Platform.Infrastructure.Notifications.NotificationService>();
        services.AddHttpClient<Platform.Infrastructure.Notifications.Channels.TwilioSmsChannel>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<Platform.Infrastructure.Notifications.Channels.TwilioWhatsAppChannel>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<Platform.Application.Abstractions.Notifications.INotificationChannel>(sp => sp.GetRequiredService<Platform.Infrastructure.Notifications.Channels.TwilioSmsChannel>());
        services.AddScoped<Platform.Application.Abstractions.Notifications.INotificationChannel>(sp => sp.GetRequiredService<Platform.Infrastructure.Notifications.Channels.TwilioWhatsAppChannel>());
        services.AddScoped<Platform.Application.Abstractions.Notifications.INotificationChannel,
                           Platform.Infrastructure.Notifications.Channels.SmtpEmailChannel>();

        services.AddHostedService<ReadyOrderAutoCancelService>();
        services.AddHostedService<NotificationDispatcherService>();
        services.AddHostedService<CanteenManagementSystem.Infrastructure.BackgroundJobs.LowStockAlertService>();
        services.AddHostedService<CanteenManagementSystem.Infrastructure.BackgroundJobs.ParentWeeklySpendService>();

        // ─── Feature flags (per-tenant TenantSetting-backed gates) ────────
        services.AddScoped<Platform.Application.Abstractions.Configuration.IFeatureFlagService,
                           Platform.Infrastructure.Configuration.FeatureFlagService>();

        // ─── Allergy warning + favourites + GDPR tenant export ────────────
        services.AddScoped<CanteenManagementSystem.Application.Allergies.IAllergyWarningService,
                           CanteenManagementSystem.Infrastructure.Allergies.AllergyWarningService>();
        services.AddScoped<CanteenManagementSystem.Application.Favorites.IUserFavoritesService,
                           CanteenManagementSystem.Infrastructure.Favorites.UserFavoritesService>();
        services.AddScoped<CanteenManagementSystem.Application.Tenancy.ITenantDataExportService,
                           CanteenManagementSystem.Infrastructure.Tenancy.TenantDataExportService>();

        // ─── New reports: TopSpenders + Reconciliation + NBR VAT ──────────
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.ReconciliationReport>();
        services.AddScoped<Platform.Application.Abstractions.Reporting.IReport,
                           CanteenManagementSystem.Infrastructure.Reports.Reports.NbrVatRegisterReport>();

        // ─── Admin landing dashboard (KPI strip + recent activity) ────────
        services.AddScoped<CanteenManagementSystem.Application.Dashboards.IAdminDashboardService,
                           CanteenManagementSystem.Infrastructure.Dashboards.AdminDashboardService>();

        // ─── Platform dashboard (SystemAdmin cross-tenant overview) ───────
        services.AddScoped<CanteenManagementSystem.Application.Sysadmin.IPlatformDashboardService,
                           CanteenManagementSystem.Infrastructure.Sysadmin.PlatformDashboardService>();

        // ─── GDPR Article 20 data-subject export (per-user ZIP archive) ───
        services.AddScoped<CanteenManagementSystem.Application.Me.IDataSubjectExportService,
                           CanteenManagementSystem.Infrastructure.Me.DataSubjectExportService>();

        // ─── /admin/exports/* CSV aggregator (orders + wallet) ────────────
        services.AddScoped<CanteenManagementSystem.Application.Exports.IAdminExportsService,
                           CanteenManagementSystem.Infrastructure.Exports.AdminExportsService>();

        // ─── Sandbox impls (override the stub when Sandbox.Enabled) ───────
        // Swap StubBillingService → SandboxBillingService so the admin UI
        // shows realistic plan + invoice data without a Stripe account.
        services.AddScoped<Platform.Application.Abstractions.Billing.IBillingService,
                           Platform.Infrastructure.Billing.SandboxBillingService>();

        // Local NBR gateway simulator (binds 127.0.0.1:5099, disable in prod).
        services.AddHostedService<CanteenManagementSystem.Infrastructure.BackgroundJobs.LocalNbrGatewaySimulator>();

        // Demo fixtures contributor — only seeds when Sandbox.Enabled=true.
        services.AddScoped<Platform.Application.Abstractions.Seeding.ISeedContributor,
                           CanteenManagementSystem.Infrastructure.Seeding.SandboxSeedContributor>();

        // ─── Canteen-specific seed contributor (Order=100; runs after platform 10..40) ──
        services.AddScoped<ISeedContributor, CanteenSeed>();

        // ─── Directory sync (Phase 2A manual + Phase 3 pull + Phase 5 API) ──
        //   IDirectoryWriter      → canteen-specific (Students/Employees writer)
        //   IDirectorySyncService → platform-generic orchestrator
        //   IDirectorySourceFactory → resolves Source per-tenant from settings
        //   DirectorySyncBackgroundService → ticks every minute, runs due tenants
        services.AddScoped<IDirectoryWriter, CanteenDirectoryWriter>();
        services.AddScoped<IDirectorySyncService, DirectorySyncService>();
        services.AddScoped<IDirectorySourceFactory, DirectorySourceFactory>();
        services.AddHttpClient("directory.api", c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHostedService<DirectorySyncBackgroundService>();

        // Fallback tenant context — for background jobs, design-time, tests.
        // Presentation replaces this with RequestTenantContext per-request.
        services.AddScoped<ITenantContext, NullTenantContext>();

        return services;
    }
}
