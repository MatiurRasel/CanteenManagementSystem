using CanteenManagementSystem.Application.Interfaces;
using CanteenManagementSystem.Domain.Entities;
using CanteenManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(ApplicationDbContext context, ILogger<InventoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ReserveStockAsync(Guid itemId, decimal quantity)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (inventory == null)
            {
                // Create inventory if doesn't exist
                var menuItem = await _context.MenuItems.FindAsync(itemId);
                if (menuItem == null)
                    return false;

                inventory = new Inventory
                {
                    InventoryId = Guid.NewGuid(),
                    ClientId = menuItem.ClientId,
                    ItemId = itemId,
                    TotalStock = 0,
                    ReservedStock = 0
                };

                _context.Inventory.Add(inventory);
            }

            var availableStock = inventory.TotalStock - inventory.ReservedStock;
            if (availableStock < quantity)
                return false;

            inventory.ReservedStock += quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            // Log transaction
            var transaction = new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventory.InventoryId,
                ClientId = inventory.ClientId,
                TransactionType = "RESERVE",
                Quantity = quantity,
                StockBefore = inventory.ReservedStock - quantity,
                StockAfter = inventory.ReservedStock,
                CreatedAt = DateTime.UtcNow
            };

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeductStockAsync(Guid itemId, decimal quantity, Guid? orderId = null)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (inventory == null || inventory.ReservedStock < quantity)
                return false;

            inventory.TotalStock -= quantity;
            inventory.ReservedStock -= quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            // Log transaction
            var transaction = new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventory.InventoryId,
                ClientId = inventory.ClientId,
                TransactionType = "SALE",
                Quantity = quantity,
                StockBefore = inventory.TotalStock + quantity,
                StockAfter = inventory.TotalStock,
                RelatedOrderId = orderId,
                CreatedAt = DateTime.UtcNow
            };

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReleaseStockAsync(Guid itemId, decimal quantity)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (inventory == null || inventory.ReservedStock < quantity)
                return false;

            inventory.ReservedStock -= quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            // Log transaction
            var transaction = new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventory.InventoryId,
                ClientId = inventory.ClientId,
                TransactionType = "RELEASE",
                Quantity = quantity,
                StockBefore = inventory.ReservedStock + quantity,
                StockAfter = inventory.ReservedStock,
                CreatedAt = DateTime.UtcNow
            };

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RestockAsync(Guid itemId, decimal quantity, decimal costPerUnit, Guid operatorId)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (inventory == null)
            {
                var menuItem = await _context.MenuItems.FindAsync(itemId);
                if (menuItem == null)
                    return false;

                inventory = new Inventory
                {
                    InventoryId = Guid.NewGuid(),
                    ClientId = menuItem.ClientId,
                    ItemId = itemId,
                    TotalStock = 0,
                    ReservedStock = 0
                };

                _context.Inventory.Add(inventory);
            }

            var stockBefore = inventory.TotalStock;
            inventory.TotalStock += quantity;
            inventory.CostPerUnit = costPerUnit;
            inventory.TotalValue = inventory.TotalStock * costPerUnit;
            inventory.LastPurchasePrice = costPerUnit;
            inventory.LastRestockedAt = DateTime.UtcNow;
            inventory.LastRestockedBy = operatorId;
            inventory.UpdatedAt = DateTime.UtcNow;

            // Log transaction
            var transaction = new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventory.InventoryId,
                ClientId = inventory.ClientId,
                TransactionType = "RESTOCK",
                Quantity = quantity,
                StockBefore = stockBefore,
                StockAfter = inventory.TotalStock,
                CostPerUnit = costPerUnit,
                TotalCost = quantity * costPerUnit,
                CreatedBy = operatorId,
                CreatedAt = DateTime.UtcNow
            };

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<decimal> GetAvailableStockAsync(Guid itemId)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (inventory == null)
                return 0;

            return inventory.TotalStock - inventory.ReservedStock;
        }

        public async Task<bool> LogWastageAsync(Guid itemId, decimal quantity, string reason, Guid? orderId = null)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (inventory == null)
                return false;

            var wastage = new WastageLog
            {
                WastageId = Guid.NewGuid(),
                ClientId = inventory.ClientId,
                ItemId = itemId,
                QuantityWasted = quantity,
                UnitOfMeasurement = inventory.UnitOfMeasurement,
                Reason = reason,
                CostPerUnit = inventory.CostPerUnit,
                TotalLoss = quantity * (inventory.CostPerUnit ?? 0),
                RelatedOrderId = orderId,
                LoggedAt = DateTime.UtcNow
            };

            _context.WastageLog.Add(wastage);

            // Deduct from inventory
            inventory.TotalStock -= quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            // Log inventory transaction
            var transaction = new InventoryTransaction
            {
                TransactionId = Guid.NewGuid(),
                InventoryId = inventory.InventoryId,
                ClientId = inventory.ClientId,
                TransactionType = "WASTAGE",
                Quantity = quantity,
                StockBefore = inventory.TotalStock + quantity,
                StockAfter = inventory.TotalStock,
                RelatedOrderId = orderId,
                Notes = $"Wastage: {reason}",
                CreatedAt = DateTime.UtcNow
            };

            _context.InventoryTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

