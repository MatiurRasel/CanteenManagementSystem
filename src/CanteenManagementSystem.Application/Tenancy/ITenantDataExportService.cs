// =============================================================================
// ITenantDataExportService  (CanteenManagementSystem.Application.Tenancy)
// -----------------------------------------------------------------------------
// GDPR / off-boarding: produces a ZIP archive containing every row this tenant
// owns, one JSON file per entity. Used to satisfy a "data subject" / "right to
// portability" request OR to hand a tenant their data when they leave.
//
// SHAPE
//   tenant-{ClientCode}-{yyyy-MM-dd}.zip
//     ├── manifest.json        (export timestamp + entity counts)
//     ├── clients.json         (the tenant row itself)
//     ├── students.json
//     ├── employees.json
//     ├── orders.json
//     ├── order-items.json
//     ├── wallet-balances.json
//     ├── wallet-ledger.json
//     ├── audit-entries.json
//     ├── notification-log.json
//     └── …
//
// The service uses NoTracking reads scoped by the global query filter, so it
// can never leak rows across tenants.
// =============================================================================

namespace CanteenManagementSystem.Application.Tenancy;

public sealed record TenantExportArchive(byte[] Bytes, string FileName, string ContentType = "application/zip");

public interface ITenantDataExportService
{
    Task<TenantExportArchive> ExportAsync(CancellationToken cancellationToken = default);
}
