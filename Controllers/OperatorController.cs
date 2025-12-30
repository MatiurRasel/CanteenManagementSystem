using Microsoft.AspNetCore.Mvc;
using CanteenManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using CanteenManagementSystem.Models.ViewModels;
using CanteenManagementSystem.Models;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CanteenManagementSystem.Controllers
{
    public class OperatorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        public OperatorController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // Main operator dashboard
        public async Task<IActionResult> Dashboard(DateTime? selectedDate)
        {
            var targetDate = selectedDate ?? DateTime.Today;

            var stats = new
            {
                TodayOrders = await _context.Orders.CountAsync(o => o.OrderDate.Date == targetDate.Date),
                PendingOrders = await _context.Orders.CountAsync(o => o.Status == CanteenOrderStatus.Pending && o.OrderDate.Date == targetDate.Date),
                DeliveredOrders = await _context.Orders.CountAsync(o => o.Status == CanteenOrderStatus.Delivered && o.OrderDate.Date == targetDate.Date),
                TodayRevenue = await _context.Orders
                    .Where(o => o.OrderDate.Date == targetDate.Date)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0
            };

            ViewBag.Stats = stats;
            ViewBag.SelectedDate = targetDate;
            return View();
        }

        // Get pending orders with user details
        public async Task<IActionResult> GetPendingOrders(DateTime? selectedDate, int page = 1, int pageSize = 50)
        {
            var targetDate = selectedDate ?? DateTime.Today;
            var isToday = targetDate.Date == DateTime.Today.Date;

            var query = _context.Orders
                .Where(o => o.Status == CanteenOrderStatus.Pending && o.OrderDate.Date == targetDate.Date)
                .OrderByDescending(o => o.OrderDate) // Latest orders first
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem);

            var totalOrders = await query.CountAsync();

            // Apply pagination
            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = await GetOrdersWithUserDetails(orders, isToday);

            return Json(new
            {
                success = true,
                orders = result,
                isToday = isToday,
                currentPage = page,
                totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize),
                totalOrders = totalOrders
            });
        }



        // Mark order as delivered
        [HttpPost]
        public async Task<IActionResult> MarkAsDelivered([FromBody] DeliverOrderRequest request)
        {
            try
            {
                var order = await _context.Orders.FindAsync(request.OrderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "অর্ডার খুঁজে পাওয়া যায়নি" });
                }

                order.Status = CanteenOrderStatus.Delivered;
                order.DeliveredDate = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "অর্ডার ডেলিভার করা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে" });
            }
        }


        // Search orders with date filtering and all statuses
        [HttpGet]
        public async Task<IActionResult> SearchAllOrders(string search, DateTime? selectedDate, int page = 1, int pageSize = 12)
        {
            var targetDate = selectedDate ?? DateTime.Today;

            var query = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .AsQueryable();

            // Filter by date if selected
            if (selectedDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date == targetDate.Date);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(o =>
                    o.OrderNumber.ToLower().Contains(search) ||
                    o.UserId.ToLower().Contains(search)
                );
            }

            var totalOrders = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate) // Latest orders first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = await GetOrdersWithUserDetails(orders, true);

            return Json(new
            {
                success = true,
                orders = result,
                currentPage = page,
                totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize),
                totalOrders = totalOrders
            });
        }

        // Search orders by user identifier
        [HttpGet]
        public async Task<IActionResult> SearchOrders(string search, DateTime? selectedDate, int page = 1, int pageSize = 50)
        {
            var targetDate = selectedDate ?? DateTime.Today;

            var query = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .Where(o => o.OrderDate.Date == targetDate.Date)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(o =>
                    o.OrderNumber.ToLower().Contains(search) ||
                    o.UserId.ToLower().Contains(search)
                );
            }

            var totalOrders = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate) // Latest orders first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = await GetOrdersWithUserDetails(orders, true);

            return Json(new
            {
                success = true,
                orders = result,
                currentPage = page,
                totalPages = (int)Math.Ceiling(totalOrders / (double)pageSize),
                totalOrders = totalOrders
            });
        }

        private async Task<List<OperatorOrderViewModel>> GetOrdersWithUserDetails(List<Order> orders, bool includePhoto = true)
        {
            var result = new List<OperatorOrderViewModel>();
            var clientSettings = await _context.UTClientSettings
                    .FirstOrDefaultAsync();
            var clientVal = clientSettings?.UserVal;
            var azureStorageUrl = _configuration.GetValue<string>("AzureStorageUrl")?.TrimEnd('/') ?? "";

            foreach (var order in orders)
            {
                var viewModel = new OperatorOrderViewModel
                {
                    OrderID = order.OrderID,
                    OrderNumber = order.OrderNumber,
                    UserType = order.UserType,
                    UserId = order.UserId,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status,
                    OrderTime = order.OrderDate.ToString("hh:mm tt", CultureInfo.CreateSpecificCulture("bn-BD")),
                    OrderDateTime = order.OrderDate,
                    Items = order.OrderItems.Select(oi => new OrderItemDetail
                    {
                        ItemName = oi.FoodItem?.ItemName ?? "Unknown Item",
                        Quantity = oi.Quantity,
                        Price = oi.TotalPrice,
                        FoodItemPrice = oi.FoodItem?.Price ?? 0
                    }).ToList()
                };

                if (order.UserType == CanteenUserType.Student)
                {
                    var student = await _context.StudentInfo_Canteens
                        .FirstOrDefaultAsync(s => s.StudentID == order.UserId);

                    if (student != null)
                    {
                        viewModel.UserName = student.StudentName ?? "";
                        viewModel.UserIdentifier = student.StudentIDC ?? student.StudentID;
                        viewModel.UserMobileNo = student.ContactNo ?? "";
                        viewModel.UserGender = student.StudentGender;
                        viewModel.AcademicInformation = $"Program: {student.ProgramName ?? "N/A"}; " +
                            $"Version: {student.VersionName ?? "N/A"}; " +
                            $"Session: {student.SessionName ?? "N/A"}; " +
                            $"Section: {student.SectionName ?? "N/A"}";

                        if (includePhoto && !string.IsNullOrWhiteSpace(student.PhotoPathS) &&
                            student.PhotoPathS != "--" && student.PhotoPathS != "-")
                        {
                            viewModel.UserPhotoUrl = $"{azureStorageUrl}/{clientVal}/{student.PhotoPathS.TrimStart('/')}";
                        }
                    }
                }
                else if (order.UserType == CanteenUserType.Employee)
                {
                    var employee = await _context.EmployeeInfo_Canteens
                        .FirstOrDefaultAsync(e => e.EmployeeID == order.UserId);

                    if (employee != null)
                    {
                        viewModel.UserName = employee.EmployeeName ?? "";
                        viewModel.UserIdentifier = employee.EmployeeID;
                        viewModel.UserMobileNo = employee.MobileNo ?? "";
                        viewModel.UserGender = employee.EmployeeGender;
                        viewModel.EmployeeTypeName = employee.EmployeeTypeName ?? "";
                        viewModel.AcademicInformation = $"Designation: {employee.DesignationName ?? "N/A"}; " +
                            $"Type: {employee.EmployeeTypeName ?? "N/A"}";

                        if (includePhoto && !string.IsNullOrWhiteSpace(employee.EmployeePhotoPath) &&
                            employee.EmployeePhotoPath != "--" && employee.EmployeePhotoPath != "-")
                        {
                            viewModel.UserPhotoUrl = $"{azureStorageUrl}/{clientVal}/{employee.EmployeePhotoPath.TrimStart('/')}";
                        }
                    }
                }

                result.Add(viewModel);
            }

            return result;
        }

        // Get order history
        public async Task<IActionResult> History(DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;

            var orders = await _context.Orders
                //.Include(o => o.Student)
                //.Include(o => o.Employee)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .Where(o => o.OrderDate.Date == targetDate.Date)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.TargetDate = targetDate;
            return View(orders);
        }

        // Update the GetStatistics method to accept date parameter
        [HttpGet]
        public async Task<IActionResult> GetStatistics(DateTime? selectedDate)
        {
            var targetDate = selectedDate ?? DateTime.Today;
            var isToday = targetDate.Date == DateTime.Today.Date;

            var stats = new
            {
                todayOrders = await _context.Orders.CountAsync(o => o.OrderDate.Date == targetDate.Date),
                pendingOrders = await _context.Orders.CountAsync(o => o.Status == CanteenOrderStatus.Pending && o.OrderDate.Date == targetDate.Date),
                deliveredOrders = await _context.Orders.CountAsync(o => o.Status == CanteenOrderStatus.Delivered && o.OrderDate.Date == targetDate.Date),
                todayRevenue = await _context.Orders
                    .Where(o => o.OrderDate.Date == targetDate.Date)
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0,
                isToday = isToday
            };

            return Json(new { success = true, stats });
        }

        public async Task<IActionResult> Orders(int status = -1)
        {
            var query = _context.Orders
                //.Include(o => o.Student)
                //.Include(o => o.Employee)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.FoodItem)
                .AsQueryable();

            if (status != -1)
                query = query.Where(o => o.Status == (CanteenOrderStatus)status);

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Take(50)
                .ToListAsync();

            ViewBag.Status = status;
            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, int status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                return Json(new { success = false, message = "Order not found" });

            order.Status = (CanteenOrderStatus)status;
            if (status == (int)CanteenOrderStatus.Delivered)
            {
                order.DeliveredDate = DateTime.Now;
            }
                

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Get live orders with user details
        public async Task<IActionResult> GetLiveOrders()
        {
            try
            {
                var today = DateTime.Today;

                var orders = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.FoodItem)
                    .Where(o => o.Status == CanteenOrderStatus.Pending && o.OrderDate.Date == today)
                    .OrderBy(o => o.OrderDate)
                    .Take(100)
                    .ToListAsync();

                var result = await GetOrdersWithUserDetails(orders, true);

                return Json(new { success = true, orders = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error retrieving orders" });
            }
        }

        private string GetUserName(CanteenUserType userType, string userId,
            Dictionary<string, string> studentNames, Dictionary<string, string> employeeNames)
        {
            if (string.IsNullOrWhiteSpace(userId)) return "Unknown User";

            return userType switch
            {
                CanteenUserType.Student when studentNames.TryGetValue(userId, out var name) => name,
                CanteenUserType.Employee when employeeNames.TryGetValue(userId, out var name) => name,
                _ => $"User #{userId}"
            };
        }
    }

    public class DeliverOrderRequest
    {
        public int OrderId { get; set; }
    }

}
