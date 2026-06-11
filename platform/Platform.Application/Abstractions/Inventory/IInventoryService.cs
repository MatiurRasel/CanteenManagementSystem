using Platform.Application.Results;

namespace Platform.Application.Abstractions.Inventory;

/// Reserve-on-order, consume-on-delivery stock operations. Backed by
/// <c>DailyMenu.ReservedQuantity</c> with optimistic concurrency via RowVersion.
public interface IInventoryService
{
    Task<Result> ReserveAsync(int dailyMenuId, int quantity, CancellationToken cancellationToken = default);
    Task<Result> ConsumeAsync(int dailyMenuId, int quantity, CancellationToken cancellationToken = default);
    Task<Result> ReleaseAsync(int dailyMenuId, int quantity, CancellationToken cancellationToken = default);
}
