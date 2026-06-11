// =============================================================================
// DataSubjectExportService  (CanteenManagementSystem.Infrastructure.Me)
// -----------------------------------------------------------------------------
// Builds the GDPR "data subject" ZIP archive for a single user. Each query
// stays inside the EF global tenant filter; the user can only see their own
// data.
// =============================================================================

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanteenManagementSystem.Application.Me;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;
using Platform.Domain.Identity;
using Platform.Domain.Notifications;

namespace CanteenManagementSystem.Infrastructure.Me;

public sealed class DataSubjectExportService : IDataSubjectExportService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    private readonly IAppDbContext _db;
    public DataSubjectExportService(IAppDbContext db) => _db = db;

    public async Task<DataSubjectArchive> ExportAsync(int appUserId, CancellationToken ct = default)
    {
        var user = await _db.Set<User>()
            .AsNoTracking()
            .Where(u => u.UserId == appUserId)
            .Select(u => new
            {
                u.UserId, u.UserName, u.DisplayName, u.Email, u.PhoneNumber,
                u.LinkedPersonId, u.UserKind, u.IsActive, u.CreatedAtUtc, u.LastLoginAtUtc
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("user not found");

        var personId = user.LinkedPersonId ?? string.Empty;

        var orders = string.IsNullOrEmpty(personId)
            ? new List<Order>(0)
            : await _db.Set<Order>()
                .AsNoTracking()
                .Include(o => o.OrderItems).ThenInclude(i => i.FoodItem)
                .Where(o => o.UserId == personId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(ct);

        var balance = string.IsNullOrEmpty(personId)
            ? null
            : await _db.Set<UserBalance>()
                .AsNoTracking()
                .Where(b => b.UserId == personId)
                .FirstOrDefaultAsync(ct);

        var ledger = string.IsNullOrEmpty(personId)
            ? new List<WalletLedger>(0)
            : await _db.Set<WalletLedger>()
                .AsNoTracking()
                .Where(l => l.UserId == personId)
                .OrderByDescending(l => l.CreatedAtUtc)
                .ToListAsync(ct);

        // NotificationLog has no UserId column — match by recipient (phone OR email).
        var recipients = new[] { user.Email, user.PhoneNumber }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var notifications = recipients.Count == 0
            ? new List<NotificationLog>(0)
            : await _db.Set<NotificationLog>()
                .AsNoTracking()
                .Where(n => n.Recipient != null && recipients.Contains(n.Recipient))
                .OrderByDescending(n => n.OccurredAtUtc)
                .Take(1000)
                .ToListAsync(ct);

        var favorites = string.IsNullOrEmpty(personId)
            ? Array.Empty<object>()
            : (await _db.Set<CanteenManagementSystem.Domain.Users.UserFavorite>()
                .AsNoTracking()
                .Where(f => f.UserId == personId)
                .OrderByDescending(f => f.CreatedAtUtc)
                .Select(f => new
                {
                    f.FavoriteId, f.UserId, f.FoodItemId,
                    FoodItem = f.FoodItem != null ? f.FoodItem.ItemName : null,
                    f.CreatedAtUtc
                })
                .ToListAsync(ct))
                .Cast<object>()
                .ToArray();

        // CardEvent has no UserId — join through NfcCard.UserId.
        var cardEvents = string.IsNullOrEmpty(personId)
            ? Array.Empty<object>()
            : (await (
                from e in _db.Set<CanteenManagementSystem.Domain.Cards.CardEvent>().AsNoTracking()
                join c in _db.Set<CanteenManagementSystem.Domain.Cards.NfcCard>().AsNoTracking()
                    on e.CardId equals c.CardId
                where c.UserId == personId
                orderby e.OccurredAtUtc descending
                select new
                {
                    e.CardEventId,
                    e.CardId,
                    CardUid = c.CardUid,
                    e.EventType,
                    e.PreviousCardUid,
                    e.NewCardUid,
                    e.PerformedBy,
                    e.Reason,
                    e.OccurredAtUtc
                }).ToListAsync(ct))
                .Cast<object>()
                .ToArray();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntry(zip, "manifest.json", new
            {
                ExportedAtUtc      = DateTime.UtcNow,
                AppUserId          = appUserId,
                UserName           = user.UserName,
                LinkedPersonId     = personId,
                OrderCount         = orders.Count,
                LedgerEntryCount   = ledger.Count,
                NotificationCount  = notifications.Count,
                FavoriteCount      = favorites.Length,
                CardEventCount     = cardEvents.Length,
                Notice             = "GDPR Article 20 data-portability export. All rows scoped to your own user id within your tenant."
            }, ct);
            await WriteEntry(zip, "profile.json",       user,           ct);
            await WriteEntry(zip, "orders.json",        orders,         ct);
            await WriteEntry(zip, "wallet-balance.json", balance,       ct);
            await WriteEntry(zip, "wallet-ledger.json", ledger,         ct);
            await WriteEntry(zip, "notifications.json", notifications,  ct);
            await WriteEntry(zip, "favorites.json",     favorites,      ct);
            await WriteEntry(zip, "card-events.json",   cardEvents,     ct);
        }
        ms.Position = 0;

        var safePersonId = string.IsNullOrEmpty(personId)
            ? "user-" + appUserId
            : new string(personId.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var fileName = $"me-{safePersonId}-{DateTime.UtcNow:yyyy-MM-dd}.zip";
        return new DataSubjectArchive(ms.ToArray(), fileName);
    }

    private static async Task WriteEntry(ZipArchive zip, string name, object? payload, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));
        await stream.WriteAsync(bytes, ct);
    }
}
