# Architecture

## Layered overview

```
+----------------------------------------------------------------------+
|  Presentation  (CanteenManagementSystem.Presentation)                |
|   - MVC controllers, Razor views, SignalR hub                        |
|   - OpenAPI / Swashbuckle, ApiVersioning                             |
|   - Middleware: GlobalException, TenantContext                       |
|   - RealTime impl (SignalROrderBroadcaster), CurrentUser, Telemetry  |
+----------------------------------------------------------------------+
|  Application   (CanteenManagementSystem.Application)                 |
|   - CQRS contract: IRequest, ICommand, IQuery, IDispatcher           |
|   - Pipeline behaviors: Logging, Validation, Transaction, etc.       |
|   - Feature folders: Orders, Menus, Verifications, Cards, ...        |
|   - Common abstractions (no Microsoft.AspNetCore allowed!):          |
|       IPaymentGateway, IPaymentOrchestrator                          |
|       IReceiptService, INotificationService, INotificationChannel    |
|       ITenantSettings, ICacheService, IUserDirectory, IClock, ...    |
+----------------------------------------------------------------------+
|  Infrastructure (CanteenManagementSystem.Infrastructure)             |
|   - EF Core ApplicationDbContext + GenericRepository + UoW           |
|   - Caching (CacheService L1+L2, TenantAwareDistributedCacheService) |
|   - Tenancy: NullTenantContext (fallback)                            |
|   - Wallet, Inventory, Audit, UserDirectory                          |
|   - Payments: BkashGateway, NagadGateway, SslCommerzGateway, Stripe  |
|   - Notifications: SmtpEmailChannel, TwilioSms/WhatsApp              |
|   - Reports: ReportingService                                        |
|   - BackgroundJobs: ReadyOrderAutoCancelService, NotificationDispatch|
+----------------------------------------------------------------------+
|  Domain        (CanteenManagementSystem.Domain)                      |
|   - Pure entities, enums, lifecycle helpers (OrderLifecycle).        |
|   - ITenantContext interface lives here.                             |
|   - NO references to EF Core, ASP.NET Core, HttpClient.              |
+----------------------------------------------------------------------+
```

## Dependency rule

Inner layers never reference outer ones:

```
Presentation -> Application -> Domain
Infrastructure -> Application -> Domain
Presentation -> Infrastructure (composition root only)
```

## The CQRS dispatcher

Every write goes through `IDispatcher.SendAsync(...)`. The pipeline order is:

1. `UnhandledExceptionBehavior` — logs any non-validation exception with the
   request name so dashboards can group failures.
2. `LoggingBehavior` — start / stop log with elapsed ms.
3. `ValidationBehavior` — runs every registered `IValidator<TRequest>`.
4. `TransactionBehavior` — only for `ICommand<>`. Opens an EF transaction,
   commits on success, rolls back on any throw.
5. **Handler executes.**

Queries skip the transaction behavior.

## Multi-tenancy

* `ITenantContext` is resolved per-request by `TenantContextMiddleware`.
* The chain is `Header > Subdomain > Default`.
* The DbContext applies a global query filter scoped by tenant.
* `TenantSetting` rows are the highest-priority configuration source.

## Configuration priority

> **DB tenant setting → appsettings.json → code default**

See [Configuration.md](Configuration.md) for the contract and helpers.

## Real-time

SignalR `KitchenDisplayHub` lives at `/hubs/kitchen`. Command handlers can
push events without depending on ASP.NET Core via the `IOrderBroadcaster`
abstraction (implementation in Presentation).
