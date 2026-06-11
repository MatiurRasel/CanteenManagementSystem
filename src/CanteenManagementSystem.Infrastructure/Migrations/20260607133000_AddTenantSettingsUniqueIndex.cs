// =============================================================================
// AddTenantSettingsUniqueIndex  (20260607-133000)
// -----------------------------------------------------------------------------
// Closes a startup-crash race on `CanteenTenantSettings`. Two background
// tasks racing to write `Directory.ConsecutiveFailures` (or any other
// per-tenant key) could each INSERT a row, leaving a duplicate
// (ClientId, Key) pair. `TenantSettingsService.GetBagAsync` then crashed in
// ToDictionary with "An item with the same key has already been added".
//
// This migration:
//   1. Dedupes existing duplicate rows — keeps the most-recently-updated row
//      (ORDER BY UpdatedAtUtc DESC, SettingId DESC) and DELETEs the rest.
//   2. Adds a UNIQUE filtered index on (ClientId, [Key]) where ClientId IS NOT NULL,
//      so SQL Server rejects the second INSERT and forces callers through
//      SetAsync's idempotent UPSERT path.
//
// The filter clause leaves NULL-ClientId rows alone — those are the SystemAdmin
// "cross-tenant" settings (today there are none, but the schema permits them).
//
// The application-side fix shipped in the same commit (`GetBagAsync` now
// GroupBy+latest-wins) is the runtime safety net — this migration is the
// schema-level guard for new deployments.
// =============================================================================
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanteenManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettingsUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Dedupe — keep the latest row per (ClientId, Key); drop the rest.
            migrationBuilder.Sql(@"
                ;WITH dups AS (
                    SELECT SettingId,
                           ROW_NUMBER() OVER (
                               PARTITION BY ClientId, [Key]
                               ORDER BY UpdatedAtUtc DESC, SettingId DESC) AS rn
                    FROM CanteenTenantSettings)
                DELETE FROM CanteenTenantSettings
                 WHERE SettingId IN (SELECT SettingId FROM dups WHERE rn > 1);");

            // 2. Unique filtered index so duplicates can never come back.
            //    (Idempotent — IF NOT EXISTS pattern via INFORMATION_SCHEMA.)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                     WHERE name = 'UX_CanteenTenantSettings_Client_Key'
                       AND object_id = OBJECT_ID(N'dbo.CanteenTenantSettings'))
                BEGIN
                    CREATE UNIQUE INDEX UX_CanteenTenantSettings_Client_Key
                        ON CanteenTenantSettings (ClientId, [Key])
                        WHERE ClientId IS NOT NULL;
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                     WHERE name = 'UX_CanteenTenantSettings_Client_Key'
                       AND object_id = OBJECT_ID(N'dbo.CanteenTenantSettings'))
                BEGIN
                    DROP INDEX UX_CanteenTenantSettings_Client_Key
                              ON CanteenTenantSettings;
                END");
            // Down does NOT restore deleted duplicates — they were stale rows
            // and revising the historical Up() to keep them would defeat the fix.
        }
    }
}
