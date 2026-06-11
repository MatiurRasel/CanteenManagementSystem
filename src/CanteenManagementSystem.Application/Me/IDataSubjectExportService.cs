// =============================================================================
// IDataSubjectExportService  (CanteenManagementSystem.Application.Me)
// -----------------------------------------------------------------------------
// GDPR Article 20 — Right to data portability. Produces a ZIP archive containing
// every row this user owns, one JSON file per data class.
//
// SHAPE
//   me-{LinkedPersonId}-{yyyy-MM-dd}.zip
//     ├── manifest.json        (export timestamp, user id, entity counts)
//     ├── profile.json         (the AppUsers row + linked student/employee)
//     ├── orders.json          (full history)
//     ├── wallet-balance.json  (current balance)
//     ├── wallet-ledger.json   (every ledger entry for the user)
//     ├── favorites.json       (saved favourites)
//     ├── notifications.json   (every NotificationLog row addressed to the user)
//     └── card-events.json     (NFC card lifecycle events bound to the user)
//
// Scoping: every query is tenant-scoped through the EF global filter; the user
// can only export their own data. Sysadmin can use the existing tenant-level
// export at GET /admin/tenancy/export for the whole tenant.
// =============================================================================

namespace CanteenManagementSystem.Application.Me;

public sealed record DataSubjectArchive(byte[] Bytes, string FileName, string ContentType = "application/zip");

public interface IDataSubjectExportService
{
    /// <summary>Build the ZIP for the currently-signed-in user (caller passes AppUsers.UserId).</summary>
    Task<DataSubjectArchive> ExportAsync(int appUserId, CancellationToken ct = default);
}
