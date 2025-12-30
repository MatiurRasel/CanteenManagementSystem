using CanteenManagementSystem.Data;
using CanteenManagementSystem.Models;
using CanteenManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public OrderController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // Place order with quantity validation
        [HttpPost]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (request.Items == null || !request.Items.Any())
                {
                    return Json(new OrderResponse
                    {
                        Success = false,
                        Message = "অর্ডারে কোনো আইটেম নেই"
                    });
                }

                // Get user balance
                UserBalance? balance = null;

                string userId = request.UserId;//StudentID & EmployeeID

                balance = await _context.UserBalances
                        .FirstOrDefaultAsync(b => b.UserId == userId);

                if (balance == null)
                {
                    return Json(new OrderResponse
                    {
                        Success = false,
                        Message = "ব্যবহারকারীর ব্যালেন্স খুঁজে পাওয়া যায়নি"
                    });
                }

                // Calculate total and validate quantities
                decimal totalAmount = 0;
                var orderItems = new List<OrderItem>();
                var menuUpdates = new List<DailyMenu>();

                foreach (var item in request.Items)
                {
                    var dailyMenu = await _context.DailyMenus
                        .Include(dm => dm.FoodItem)
                        .FirstOrDefaultAsync(dm => dm.DailyMenuID == item.DailyMenuId);

                    if (dailyMenu == null || !dailyMenu.IsAvailable)
                    {
                        await transaction.RollbackAsync();
                        return Json(new OrderResponse
                        {
                            Success = false,
                            Message = $"আইটেম উপলব্ধ নেই"
                        });
                    }

                    // Check quantity availability
                    if (dailyMenu.AvailableQuantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Json(new OrderResponse
                        {
                            Success = false,
                            Message = $"{dailyMenu.FoodItem.ItemName} - শুধুমাত্র {dailyMenu.AvailableQuantity} টি বাকি আছে"
                        });
                    }

                    var itemTotal = dailyMenu.FoodItem.Price * item.Quantity;
                    totalAmount += itemTotal;

                    orderItems.Add(new OrderItem
                    {
                        FoodItemID = item.FoodItemId,
                        Quantity = item.Quantity,
                        UnitPrice = dailyMenu.FoodItem.Price,
                        TotalPrice = itemTotal
                    });

                    // Update available quantity
                    dailyMenu.AvailableQuantity -= item.Quantity;
                    if (dailyMenu.AvailableQuantity <= 0)
                    {
                        dailyMenu.IsAvailable = false;
                    }
                    menuUpdates.Add(dailyMenu);
                }

                // Check balance
                if (balance.AvailableBalance < totalAmount)
                {
                    await transaction.RollbackAsync();
                    return Json(new OrderResponse
                    {
                        Success = false,
                        Message = $"অপর্যাপ্ত ব্যালেন্স। উপলব্ধ: ৳{balance.AvailableBalance}, প্রয়োজন: ৳{totalAmount}"
                    });
                }

                // Create order
                var order = new Order
                {
                    OrderNumber = GenerateOrderNumber(request.UserIdentifier),
                    UserId = userId,
                    UserType = request.UserType,
                    TotalAmount = totalAmount,
                    Status = CanteenOrderStatus.Pending,
                    OrderDate = DateTime.Now,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                // Update user balance
                balance.UsedBalance += totalAmount;
                balance.LastUpdated = DateTime.Now;

                // Update menu quantities
                _context.DailyMenus.UpdateRange(menuUpdates);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new OrderResponse
                {
                    Success = true,
                    Message = "অর্ডার সফলভাবে সম্পন্ন হয়েছে",
                    OrderId = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    TotalAmount = totalAmount,
                    RemainingBalance = balance.AvailableBalance
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new OrderResponse
                {
                    Success = false,
                    Message = "অর্ডার প্রক্রিয়াকরণে সমস্যা হয়েছে"
                });
            }
        }

        private string GenerateOrderNumber(string requestUserId)
        {
            return $"ORD-{DateTime.Now:yyMMdd}-{requestUserId}-{new Random().Next(100, 999)}";
        }
        // Get order success details
        public async Task<IActionResult> Success(int orderId)
        {
            var order = await _context.Orders
                //.Include(o => o.Student)
                //.Include(o => o.Employee)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
                return NotFound();

            return View(order);
        }
        // Kiosk screen for ordering
        public async Task<IActionResult> Kiosk(string userType)
        {
            if (string.IsNullOrEmpty(userType))
                return RedirectToAction("SelectUserType", "Home");

            HttpContext.Session.SetString("UserType", userType);

            var todayMenu = await _context.DailyMenus
                .Include(dm => dm.FoodItem)
                .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
                .OrderBy(dm => dm.DisplayOrder)
                .ToListAsync();

            ViewBag.UserType = userType;
            return View(todayMenu);
        }
    }

    // Request models
    public class OrderRequest
    {
        public string UserId { get; set; }
        public string UserType { get; set; } = string.Empty;
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public int FoodItemId { get; set; }
        public int Quantity { get; set; }
    }
}
