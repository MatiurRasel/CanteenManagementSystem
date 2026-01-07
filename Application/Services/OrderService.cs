using CanteenManagementSystem.Application.DTOs;
using CanteenManagementSystem.Application.Interfaces;
using CanteenManagementSystem.Domain.Entities;
using CanteenManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWalletService _walletService;
        private readonly IInventoryService _inventoryService;
        private readonly IMenuService _menuService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            ApplicationDbContext context,
            IWalletService walletService,
            IInventoryService inventoryService,
            IMenuService menuService,
            ILogger<OrderService> logger)
        {
            _context = context;
            _walletService = walletService;
            _inventoryService = inventoryService;
            _menuService = menuService;
            _logger = logger;
        }

        public async Task<OrderDto> PlaceOrderAsync(PlaceOrderRequest request, Guid? userId = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get user if provided
                User? user = null;
                if (userId.HasValue)
                {
                    user = await _context.Users.FindAsync(userId.Value);
                    if (user == null)
                        throw new InvalidOperationException("User not found");
                }

                // Validate items and calculate totals
                decimal subtotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var itemRequest in request.Items)
                {
                    var menuItem = await _context.MenuItems
                        .Include(m => m.Inventory)
                        .FirstOrDefaultAsync(m => m.ItemId == itemRequest.ItemId);

                    if (menuItem == null || !menuItem.IsAvailable)
                        throw new InvalidOperationException($"Item {itemRequest.ItemId} not available");

                    // Check stock
                    var availableStock = menuItem.Inventory?.TotalStock - menuItem.Inventory?.ReservedStock ?? 0;
                    if (availableStock < itemRequest.Quantity)
                        throw new InvalidOperationException($"Insufficient stock for {menuItem.ItemName}");

                    // Get role-based price
                    var unitPrice = GetRoleBasedPrice(menuItem, user?.Role ?? "CUSTOMER");
                    var itemSubtotal = unitPrice * itemRequest.Quantity;
                    subtotal += itemSubtotal;

                    orderItems.Add(new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        ItemId = itemRequest.ItemId,
                        ItemName = menuItem.ItemName,
                        ItemCode = menuItem.ItemCode,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = unitPrice,
                        Subtotal = itemSubtotal,
                        Total = itemSubtotal,
                        CustomizationsJson = System.Text.Json.JsonSerializer.Serialize(itemRequest.Customizations),
                        SpecialInstructions = itemRequest.SpecialInstructions,
                        StockReserved = false,
                        StockDeducted = false
                    });
                }

                // Calculate tax and total
                var taxAmount = subtotal * 0.05m; // 5% tax (configurable)
                var totalAmount = subtotal + taxAmount - request.Items.Sum(i => 0); // Discount can be added

                // Check wallet balance if user is authenticated
                if (user != null)
                {
                    var wallet = await _walletService.GetWalletAsync(user.UserId);
                    if (wallet.SpendableBalance < totalAmount)
                        throw new InvalidOperationException("Insufficient balance");

                    // Block amount
                    await _walletService.BlockAmountAsync(user.UserId, totalAmount);
                }

                // Reserve stock
                foreach (var itemRequest in request.Items)
                {
                    await _inventoryService.ReserveStockAsync(itemRequest.ItemId, itemRequest.Quantity);
                }

                // Generate order number and token
                var orderNumber = GenerateOrderNumber();
                var tokenNumber = GenerateTokenNumber();

                // Create order
                var order = new Order
                {
                    OrderId = Guid.NewGuid(),
                    ClientId = user?.ClientId ?? Guid.Empty, // Should be set from context
                    UserId = userId,
                    OrderNumber = orderNumber,
                    TokenNumber = tokenNumber,
                    OrderType = request.OrderType,
                    OrderStatus = "CONFIRMED",
                    PaymentStatus = user != null ? "BLOCKED" : "PENDING",
                    Subtotal = subtotal,
                    TaxAmount = taxAmount,
                    TotalAmount = totalAmount,
                    AmountBlocked = user != null,
                    DeliveryTimeSlot = request.DeliveryTimeSlot,
                    TableNumber = request.TableNumber,
                    SpecialInstructions = request.SpecialInstructions,
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow,
                    AutoCancelAt = request.DeliveryTimeSlot?.AddMinutes(30) ?? DateTime.UtcNow.AddMinutes(30),
                    SyncStatus = "SYNCED"
                };

                _context.Orders.Add(order);

                // Add order items
                foreach (var item in orderItems)
                {
                    item.OrderId = order.OrderId;
                    item.StockReserved = true;
                    _context.OrderItems.Add(item);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Map to DTO
                return MapToDto(order, orderItems);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<OrderDto> GetOrderAsync(Guid orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                throw new InvalidOperationException("Order not found");

            return MapToDto(order, order.OrderItems.ToList());
        }

        public async Task<List<OrderDto>> GetUserOrdersAsync(Guid userId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return orders.Select(o => MapToDto(o, o.OrderItems.ToList())).ToList();
        }

        public async Task<bool> CancelOrderAsync(Guid orderId, Guid userId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null || order.OrderStatus == "PREPARING" || order.OrderStatus == "READY")
                return false;

            // Unblock amount
            if (order.AmountBlocked && userId == order.UserId)
            {
                await _walletService.UnblockAmountAsync(userId, order.TotalAmount);
            }

            // Release stock
            foreach (var item in order.OrderItems)
            {
                await _inventoryService.ReleaseStockAsync(item.ItemId, item.Quantity);
            }

            order.OrderStatus = "CANCELLED";
            order.CancelledAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ConfirmDeliveryAsync(Guid orderId, string nfcCardNumber)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.User)
                .ThenInclude(u => u.NfcCards)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.OrderStatus != "READY")
                return false;

            // Verify NFC card
            if (order.User == null || !order.User.NfcCards.Any(c => c.CardNumber == nfcCardNumber && c.CardStatus == "ACTIVE"))
                return false;

            // Deduct amount
            if (order.UserId.HasValue)
            {
                await _walletService.DeductAmountAsync(order.UserId.Value, order.TotalAmount, orderId);
            }

            // Deduct stock
            foreach (var item in order.OrderItems)
            {
                await _inventoryService.DeductStockAsync(item.ItemId, item.Quantity, orderId);
                item.StockDeducted = true;
            }

            order.OrderStatus = "DELIVERED";
            order.PaymentStatus = "COMPLETED";
            order.DeliveredAt = DateTime.UtcNow;
            order.NfcCardUsed = nfcCardNumber;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkOrderReadyAsync(Guid orderId, Guid operatorId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null || order.OrderStatus != "PREPARING")
                return false;

            order.OrderStatus = "READY";
            order.ReadyAt = DateTime.UtcNow;
            order.PreparedBy = operatorId;
            order.AutoCancelAt = DateTime.UtcNow.AddMinutes(30);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<OrderDto>> GetPendingOrdersAsync(Guid clientId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.ClientId == clientId && 
                           (o.OrderStatus == "CONFIRMED" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY"))
                .OrderBy(o => o.DeliveryTimeSlot ?? o.CreatedAt)
                .ToListAsync();

            return orders.Select(o => MapToDto(o, o.OrderItems.ToList())).ToList();
        }

        private decimal GetRoleBasedPrice(MenuItem menuItem, string role)
        {
            if (string.IsNullOrEmpty(menuItem.RoleBasedPricingJson) || menuItem.RoleBasedPricingJson == "{}")
                return menuItem.BasePrice;

            try
            {
                var pricing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(menuItem.RoleBasedPricingJson);
                if (pricing != null && pricing.ContainsKey(role))
                    return pricing[role];
            }
            catch { }

            return menuItem.BasePrice;
        }

        private string GenerateOrderNumber()
        {
            var date = DateTime.Now;
            var random = new Random().Next(1000, 9999);
            return $"ORD-{date:yyyyMMdd}-{random}";
        }

        private string GenerateTokenNumber()
        {
            var today = DateTime.Today;
            var lastToken = _context.Orders
                .Where(o => o.CreatedAt.Date == today)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => o.TokenNumber)
                .FirstOrDefault();

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastToken) && lastToken.StartsWith("T-"))
            {
                if (int.TryParse(lastToken.Substring(2), out int current))
                {
                    nextNumber = current + 1;
                }
            }

            return $"T-{nextNumber}";
        }

        private OrderDto MapToDto(Order order, List<OrderItem> items)
        {
            return new OrderDto
            {
                OrderId = order.OrderId,
                OrderNumber = order.OrderNumber,
                TokenNumber = order.TokenNumber,
                OrderType = order.OrderType,
                OrderStatus = order.OrderStatus,
                PaymentStatus = order.PaymentStatus,
                Subtotal = order.Subtotal,
                TaxAmount = order.TaxAmount,
                DiscountAmount = order.DiscountAmount,
                TotalAmount = order.TotalAmount,
                DeliveryTimeSlot = order.DeliveryTimeSlot,
                CreatedAt = order.CreatedAt,
                Items = items.Select(i => new OrderItemDto
                {
                    ItemId = i.ItemId,
                    ItemName = i.ItemName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal,
                    Customizations = string.IsNullOrEmpty(i.CustomizationsJson) 
                        ? new List<CustomizationDto>() 
                        : System.Text.Json.JsonSerializer.Deserialize<List<CustomizationDto>>(i.CustomizationsJson) ?? new()
                }).ToList()
            };
        }
    }
}

