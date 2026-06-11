// =============================================================================
// OperatorQueryService  (CanteenManagementSystem.Application.Operators)
// -----------------------------------------------------------------------------
// ADR 0004: IUnitOfWork + IReadOnlyRepository<T> / IRepository<T>. Order
// status updates write via the UoW Orders repo; everything else is read-only.
// =============================================================================

using System.Globalization;
using Platform.Application.Persistence;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.Users;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Operators;

public class OperatorQueryService : IOperatorQueryService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<WalletLedger> _ledger;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IUserDirectory _userDirectory;
    private readonly INotificationService _notifications;
    private readonly ILogger<OperatorQueryService>? _logger;

    public OperatorQueryService(
        IUnitOfWork uow,
        IReadOnlyRepository<WalletLedger> ledger,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IUserDirectory userDirectory,
        INotificationService notifications,
        ILogger<OperatorQueryService>? logger = null)
    {
        _uow = uow;
        _ledger = ledger;
        _students = students;
        _employees = employees;
        _userDirectory = userDirectory;
        _notifications = notifications;
        _logger = logger;
    }

    private IRepository<Order> Orders => _uow.Repository<Order>();

    // ─── End-of-shift summary ──────────────────────────────────────────────

    public async Task<ShiftSummaryDto> GetShiftSummaryAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var d = date.Date;
        var next = d.AddDays(1);

        var ordersForDay = Orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= d && o.OrderDate < next);

        var totals = await ordersForDay.GroupBy(_ => 1).Select(g => new
        {
            Placed     = g.Count(),
            Delivered  = g.Count(o => o.Status == CanteenOrderStatus.Delivered),
            Pending    = g.Count(o => o.Status == CanteenOrderStatus.Pending || o.Status == CanteenOrderStatus.Placed ||
                                       o.Status == CanteenOrderStatus.Confirmed || o.Status == CanteenOrderStatus.Preparing ||
                                       o.Status == CanteenOrderStatus.Ready),
            Cancelled  = g.Count(o => o.Status == CanteenOrderStatus.Cancelled),
            Gross      = g.Where(o => o.Status == CanteenOrderStatus.Delivered).Sum(o => (decimal?)o.TotalAmount) ?? 0m,
            FirstAt    = g.Min(o => (DateTime?)o.OrderDate),
            LastAt     = g.Max(o => (DateTime?)o.OrderDate),
            Customers  = g.Select(o => o.UserId).Distinct().Count()
        }).FirstOrDefaultAsync(cancellationToken);

        var refunded = await _ledger.NoTrackingQuery()
            .Where(l => l.CreatedAtUtc >= d.ToUniversalTime() && l.CreatedAtUtc < next.ToUniversalTime() && l.EntryType == WalletLedgerEntryType.Refund)
            .Select(l => (decimal?)l.Amount).SumAsync(cancellationToken) ?? 0m;

        var topItems = await ordersForDay
            .Where(o => o.Status == CanteenOrderStatus.Delivered)
            .SelectMany(o => o.OrderItems)
            .GroupBy(oi => oi.FoodItem.ItemName)
            .Select(g => new ShiftTopItemRow(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.TotalPrice)))
            .OrderByDescending(r => r.QuantitySold).Take(10).ToListAsync(cancellationToken);

        var cancelled = await ordersForDay
            .Where(o => o.Status == CanteenOrderStatus.Cancelled)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new ShiftCancelledRow(o.OrderID, o.OrderNumber, o.TotalAmount, o.OrderDate, null))
            .Take(50).ToListAsync(cancellationToken);

        return new ShiftSummaryDto
        {
            ShiftDate        = d,
            OrdersPlaced     = totals?.Placed ?? 0,
            OrdersDelivered  = totals?.Delivered ?? 0,
            OrdersPending    = totals?.Pending ?? 0,
            OrdersCancelled  = totals?.Cancelled ?? 0,
            GrossRevenue     = totals?.Gross ?? 0m,
            Refunded         = refunded,
            UniqueCustomers  = totals?.Customers ?? 0,
            TopItems         = topItems,
            Cancelled        = cancelled,
            FirstOrderAtUtc  = totals?.FirstAt,
            LastOrderAtUtc   = totals?.LastAt
        };
    }

    public async Task<OperatorDashboardStatsDto> GetDashboardStatsAsync(DateTime? selectedDate)
    {
        var targetDate = (selectedDate ?? DateTime.Today).Date;
        var ordersForDate = Orders.NoTrackingQuery().Where(o => o.OrderDate.Date == targetDate);

        // Single trip to the DB instead of four — group + project.
        var summary = await ordersForDate
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(o => o.Status == CanteenOrderStatus.Pending),
                Delivered = g.Count(o => o.Status == CanteenOrderStatus.Delivered),
                Revenue = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m
            })
            .FirstOrDefaultAsync();

        return new OperatorDashboardStatsDto
        {
            TodayOrders = summary?.Total ?? 0,
            PendingOrders = summary?.Pending ?? 0,
            DeliveredOrders = summary?.Delivered ?? 0,
            TodayRevenue = summary?.Revenue ?? 0
        };
    }

    public async Task<PagedOrdersResultDto> GetPendingOrdersAsync(DateTime? selectedDate, int page, int pageSize)
    {
        var targetDate = (selectedDate ?? DateTime.Today).Date;
        var isToday = targetDate == DateTime.Today.Date;
        var query = Orders.NoTrackingQuery()
            .Where(o => o.Status == CanteenOrderStatus.Pending && o.OrderDate.Date == targetDate)
            .OrderByDescending(o => o.OrderDate);

        var totalOrders = await query.CountAsync();
        var orders = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .ToListAsync();

        return new PagedOrdersResultDto
        {
            Orders = await GetOrdersWithUserDetailsAsync(orders, isToday),
            IsToday = isToday,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize),
            TotalOrders = totalOrders
        };
    }

    public async Task<PagedOrdersResultDto> SearchAllOrdersAsync(string search, DateTime? selectedDate, int page, int pageSize)
    {
        var targetDate = (selectedDate ?? DateTime.Today).Date;
        var query = Orders.NoTrackingQuery();

        if (selectedDate.HasValue)
        {
            query = query.Where(o => o.OrderDate.Date == targetDate);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(term) || o.UserId.ToLower().Contains(term));
        }

        var totalOrders = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .ToListAsync();

        return new PagedOrdersResultDto
        {
            Orders = await GetOrdersWithUserDetailsAsync(orders, true),
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize),
            TotalOrders = totalOrders
        };
    }

    public async Task<PagedOrdersResultDto> SearchOrdersAsync(string search, DateTime? selectedDate, int page, int pageSize)
    {
        var targetDate = (selectedDate ?? DateTime.Today).Date;
        var query = Orders.NoTrackingQuery().Where(o => o.OrderDate.Date == targetDate);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(term) || o.UserId.ToLower().Contains(term));
        }

        var totalOrders = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .ToListAsync();

        return new PagedOrdersResultDto
        {
            Orders = await GetOrdersWithUserDetailsAsync(orders, true),
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize),
            TotalOrders = totalOrders
        };
    }

    public async Task<List<OperatorOrderViewModel>> GetOrdersWithUserDetailsAsync(List<Order> orders, bool includePhoto = true)
    {
        if (orders.Count == 0) return new List<OperatorOrderViewModel>();

        // Single batched lookup against student + employee sources, replacing
        // the previous per-order N+1 query pattern.
        // Convert the canteen-typed UserType to its string representation so the
        // platform-level IUserDirectory contract (which knows nothing about the
        // CanteenUserType enum) can take the input.
        var keys = orders
            .Select(o => (o.UserId, UserType: o.UserType.ToString()))
            .Distinct()
            .ToList();
        var profiles = await _userDirectory.BatchLookupAsync(keys);

        var bnCulture = CultureInfo.CreateSpecificCulture("bn-BD");
        var result = new List<OperatorOrderViewModel>(orders.Count);

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
                OrderTime = order.OrderDate.ToString("hh:mm tt", bnCulture),
                OrderDateTime = order.OrderDate,
                InputSequence = order.InputSequence,
                Items = order.OrderItems.Select(oi => new OrderItemDetail
                {
                    ItemName = oi.FoodItem?.ItemName ?? "Unknown Item",
                    Quantity = oi.Quantity,
                    Price = oi.TotalPrice,
                    FoodItemPrice = oi.FoodItem?.Price ?? 0
                }).ToList()
            };

            if (profiles.TryGetValue((order.UserId, order.UserType.ToString()), out var profile))
            {
                viewModel.UserName = profile.UserName;
                viewModel.UserIdentifier = profile.UserIdentifier;
                viewModel.UserMobileNo = profile.MobileNo;
                viewModel.UserGender = profile.Gender;
                viewModel.AcademicInformation = profile.AcademicInformation;
                viewModel.EmployeeTypeName = profile.EmployeeTypeName;
                if (includePhoto) viewModel.UserPhotoUrl = profile.PhotoUrl;
            }

            result.Add(viewModel);
        }

        return result;
    }

    public Task<List<Order>> GetHistoryOrdersAsync(DateTime? date)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        return Orders.NoTrackingQuery()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .Where(o => o.OrderDate.Date == targetDate)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<object> GetStatisticsAsync(DateTime? selectedDate)
    {
        var stats = await GetDashboardStatsAsync(selectedDate);
        var targetDate = (selectedDate ?? DateTime.Today).Date;
        return new
        {
            success = true,
            stats = new
            {
                todayOrders = stats.TodayOrders,
                pendingOrders = stats.PendingOrders,
                deliveredOrders = stats.DeliveredOrders,
                todayRevenue = stats.TodayRevenue,
                isToday = targetDate == DateTime.Today.Date
            }
        };
    }

    public Task<List<Order>> GetOrdersAsync(int status = -1)
    {
        var query = Orders.NoTrackingQuery()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem);
        var filtered = status != -1
            ? query.Where(o => o.Status == (CanteenOrderStatus)status)
            : query.AsQueryable();
        return filtered.OrderByDescending(o => o.OrderDate).Take(50).ToListAsync();
    }

    public async Task<OrderStatusUpdateResultDto> UpdateOrderStatusAsync(int orderId, int status)
    {
        var order = await Orders.GetByIdAsync(orderId);
        if (order is null) return new OrderStatusUpdateResultDto { Success = false, Message = "Order not found" };
        var previous = order.Status;
        order.Status = (CanteenOrderStatus)status;
        if (status == (int)CanteenOrderStatus.Delivered)
        {
            order.DeliveredDate = DateTime.Now;
        }
        await _uow.SaveChangesAsync();

        // SMS on Ready / Delivered transitions — best-effort.
        if (previous != order.Status &&
            (order.Status == CanteenOrderStatus.Ready || order.Status == CanteenOrderStatus.Delivered))
        {
            await TrySendStatusSmsAsync(order);
        }
        return new OrderStatusUpdateResultDto { Success = true };
    }

    public async Task<object> GetLiveOrdersAsync()
    {
        var today = DateTime.Today;
        var orders = await Orders.NoTrackingQuery()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .Where(o => o.Status == CanteenOrderStatus.Pending && o.OrderDate.Date == today)
            .OrderBy(o => o.OrderDate).Take(100).ToListAsync();
        return new { success = true, orders = await GetOrdersWithUserDetailsAsync(orders, true) };
    }

    public async Task<OrderStatusUpdateResultDto> MarkAsDeliveredAsync(int orderId)
    {
        var order = await Orders.GetByIdAsync(orderId);
        if (order is null) return new OrderStatusUpdateResultDto { Success = false, Message = "অর্ডার খুঁজে পাওয়া যায়নি" };
        order.Status = CanteenOrderStatus.Delivered;
        order.DeliveredDate = DateTime.Now;
        await _uow.SaveChangesAsync();
        await TrySendStatusSmsAsync(order);
        return new OrderStatusUpdateResultDto { Success = true, Message = "অর্ডার সফলভাবে ডেলিভারি হয়েছে" };
    }

    // ─── SMS helper (Ready / Delivered transitions) ───────────────────────

    private async Task TrySendStatusSmsAsync(Order order, CancellationToken ct = default)
    {
        try
        {
            var phone = await ResolveContactPhoneAsync(order.UserId, order.UserType, ct);
            if (string.IsNullOrWhiteSpace(phone)) return;

            var body = order.Status == CanteenOrderStatus.Ready
                ? $"Order #{order.OrderNumber} is READY for pickup at the counter."
                : $"Order #{order.OrderNumber} delivered. Thank you!";
            await _notifications.SendRawAsync(NotificationChannel.Sms, phone!,
                subject: null, body: body, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Status-SMS failed for order {Order}", order.OrderID);
        }
    }

    private async Task<string?> ResolveContactPhoneAsync(string userId, CanteenUserType userType, CancellationToken ct)
    {
        if (userType == CanteenUserType.Student)
        {
            return await _students.NoTrackingQuery()
                .Where(s => s.ExternalId == userId)
                .Select(s => s.ContactNo).FirstOrDefaultAsync(ct);
        }
        return await _employees.NoTrackingQuery()
            .Where(e => e.ExternalId == userId)
            .Select(e => e.ContactNo).FirstOrDefaultAsync(ct);
    }
}
