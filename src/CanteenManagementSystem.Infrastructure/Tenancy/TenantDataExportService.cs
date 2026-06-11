// =============================================================================
// TenantDataExportService  (CanteenManagementSystem.Infrastructure.Tenancy)
// -----------------------------------------------------------------------------
// Produces the GDPR-style ZIP archive described by ITenantDataExportService.
// ADR 0004: IReadOnlyRepository<T> only. Global query filter restricts every
// read to the current tenant — no cross-tenant leakage possible.
// =============================================================================

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanteenManagementSystem.Application.Tenancy;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;
using Platform.Domain.Audit;
using Platform.Domain.Notifications;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Tenancy;

internal sealed class TenantDataExportService : ITenantDataExportService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private readonly ITenantContext _tenant;
    private readonly IReadOnlyRepository<Client> _clients;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<OrderItem> _orderItems;
    private readonly IReadOnlyRepository<UserBalance> _balances;
    private readonly IReadOnlyRepository<WalletLedger> _ledger;
    private readonly IReadOnlyRepository<FoodItem> _foods;
    private readonly IReadOnlyRepository<AuditEntry> _audit;
    private readonly IReadOnlyRepository<NotificationLog> _notifications;

    public TenantDataExportService(
        ITenantContext tenant,
        IReadOnlyRepository<Client> clients,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<OrderItem> orderItems,
        IReadOnlyRepository<UserBalance> balances,
        IReadOnlyRepository<WalletLedger> ledger,
        IReadOnlyRepository<FoodItem> foods,
        IReadOnlyRepository<AuditEntry> audit,
        IReadOnlyRepository<NotificationLog> notifications)
    {
        _tenant = tenant; _clients = clients; _students = students; _employees = employees;
        _orders = orders; _orderItems = orderItems; _balances = balances; _ledger = ledger;
        _foods = foods; _audit = audit; _notifications = notifications;
    }

    public async Task<TenantExportArchive> ExportAsync(CancellationToken cancellationToken = default)
    {
        // Collect every entity in one round trip per type. The query filter
        // already restricts to the current tenant.
        var clients     = await _clients.NoTrackingQuery().ToListAsync(cancellationToken);
        var students    = await _students.ListAsync(cancellationToken);
        var employees   = await _employees.ListAsync(cancellationToken);
        var orders      = await _orders.ListAsync(cancellationToken);
        var orderItems  = await _orderItems.ListAsync(cancellationToken);
        var balances    = await _balances.ListAsync(cancellationToken);
        var ledger      = await _ledger.ListAsync(cancellationToken);
        var foods       = await _foods.ListAsync(cancellationToken);
        var audit       = await _audit.ListAsync(cancellationToken);
        var notifs      = await _notifications.ListAsync(cancellationToken);

        var manifest = new
        {
            tenantClientId   = _tenant.ClientId,
            tenantClientCode = _tenant.ClientCode,
            exportedAtUtc    = DateTime.UtcNow,
            counts = new
            {
                clients      = clients.Count,
                students     = students.Count,
                employees    = employees.Count,
                orders       = orders.Count,
                orderItems   = orderItems.Count,
                balances     = balances.Count,
                ledger       = ledger.Count,
                foods        = foods.Count,
                audit        = audit.Count,
                notifications = notifs.Count
            }
        };

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "manifest.json",        manifest);
            AddEntry(zip, "clients.json",         clients);
            AddEntry(zip, "students.json",        students);
            AddEntry(zip, "employees.json",       employees);
            AddEntry(zip, "food-items.json",      foods);
            AddEntry(zip, "orders.json",          orders);
            AddEntry(zip, "order-items.json",     orderItems);
            AddEntry(zip, "wallet-balances.json", balances);
            AddEntry(zip, "wallet-ledger.json",   ledger);
            AddEntry(zip, "audit-entries.json",   audit);
            AddEntry(zip, "notification-log.json", notifs);
        }

        var fileName = $"tenant-{_tenant.ClientCode ?? _tenant.ClientId.ToString()}-{DateTime.UtcNow:yyyy-MM-dd}.zip";
        return new TenantExportArchive(ms.ToArray(), fileName);
    }

    private static void AddEntry(ZipArchive zip, string entryName, object payload)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        stream.Write(bytes);
    }
}
