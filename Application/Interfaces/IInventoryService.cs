namespace CanteenManagementSystem.Application.Interfaces
{
    public interface IInventoryService
    {
        Task<bool> ReserveStockAsync(Guid itemId, decimal quantity);
        Task<bool> DeductStockAsync(Guid itemId, decimal quantity, Guid? orderId = null);
        Task<bool> ReleaseStockAsync(Guid itemId, decimal quantity);
        Task<bool> RestockAsync(Guid itemId, decimal quantity, decimal costPerUnit, Guid operatorId);
        Task<decimal> GetAvailableStockAsync(Guid itemId);
        Task<bool> LogWastageAsync(Guid itemId, decimal quantity, string reason, Guid? orderId = null);
    }
}

