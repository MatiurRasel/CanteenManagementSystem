#nullable enable

using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Application.Sessions;
using CanteenManagementSystem.Application.Tenancy;
using CanteenManagementSystem.Application.Verifications;
using CanteenManagementSystem.Application.Verifications.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Platform.Application.Dispatch;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]
public class VerificationController : Controller
{
    private readonly IVerificationQueryService _verificationQueryService;
    private readonly IVerificationSessionService _sessionData;
    private readonly IClientCacheService _clientCache;
    private readonly IDispatcher _dispatcher;
    private readonly IOrderSessionStore _sessions;
    private readonly CanteenConfiguration _canteenConfig;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(
        IVerificationQueryService verificationQueryService,
        IVerificationSessionService sessionData,
        IClientCacheService clientCache,
        IDispatcher dispatcher,
        IOrderSessionStore sessions,
        IOptions<CanteenConfiguration> canteenConfig,
        ILogger<VerificationController> logger)
    {
        _verificationQueryService = verificationQueryService;
        _sessionData = sessionData;
        _clientCache = clientCache;
        _dispatcher = dispatcher;
        _sessions = sessions;
        _canteenConfig = canteenConfig.Value;
        _logger = logger;
    }

    private TimeSpan SessionTtl => TimeSpan.FromSeconds(Math.Max(15, _canteenConfig.OrderSessionTimeout + 30));

    public IActionResult Index() => View();
    public IActionResult KeypadOrder() => View();

    [HttpPost]
    public async Task<IActionResult> VerifyUser([FromBody] VerifyRequest request)
        => Json(await _verificationQueryService.VerifyUserAsync(request.Identifier));

    [HttpPost]
    public async Task<IActionResult> StartOrderSession([FromBody] VerifyRequest request, CancellationToken ct)
    {
        try
        {
            var identifier = request.Identifier?.Trim();
            if (string.IsNullOrEmpty(identifier))
                return Json(new { success = false, message = "অনুগ্রহ করে আপনার কার্ড স্ক্যান করুন" });

            _logger.LogInformation("Starting order session for identifier: {Identifier}", identifier);

            var scanned = await _sessionData.LookupByIdentifierAsync(identifier, ct);
            if (scanned is null) return Json(new { success = false, message = "ব্যবহারকারী খুঁজে পাওয়া যায়নি" });

            decimal monthlyLimit = _canteenConfig.DefaultMonthlyLimit;
            if (scanned.UserType == CanteenUserType.Employee && scanned.EmployeeTypeName is not null)
            {
                if (scanned.EmployeeTypeName.Equals("TEACHER", StringComparison.OrdinalIgnoreCase))
                    monthlyLimit = monthlyLimit * _canteenConfig.TeacherLimitPercentage / 100;
                else if (scanned.EmployeeTypeName.Equals("STAFF", StringComparison.OrdinalIgnoreCase))
                    monthlyLimit = monthlyLimit * _canteenConfig.StaffLimitPercentage / 100;
            }

            var balance = await _sessionData.EnsureMonthlyBalanceAsync(scanned.UserId, scanned.UserType, monthlyLimit, ct);

            var session = new OrderSession
            {
                SessionId       = Guid.NewGuid().ToString(),
                UserId          = scanned.UserId,
                UserName        = scanned.UserName,
                UserType        = scanned.UserType,
                UserPhoto       = _clientCache.GetPhotoUrl(scanned.PhotoPath),
                UserInfo        = scanned.UserInfo,
                AvailableBalance = balance.AvailableBalance,
                TimeoutSeconds  = _canteenConfig.OrderSessionTimeout,
                StartTime       = DateTime.Now,
                LastActivity    = DateTime.Now
            };
            await _sessions.SetAsync(session, SessionTtl, ct);

            var tiles = await _sessionData.GetTodayMenuTilesAsync(ct);
            var menuItems = tiles.Select((t, index) => new
            {
                dailyMenuId       = t.DailyMenuId,
                foodItemId        = t.FoodItemId,
                itemNumber        = index + 1,
                itemName          = t.ItemName,
                price             = t.Price,
                availableQuantity = t.AvailableQuantity,
                isAvailable       = t.IsAvailable,
                category          = t.Category
            }).ToList();

            return Json(new
            {
                success           = true,
                sessionId         = session.SessionId,
                userId            = scanned.UserId,
                userName          = scanned.UserName,
                userPhoto         = session.UserPhoto,
                userInfo          = scanned.UserInfo,
                userType          = scanned.UserType.ToString(),
                availableBalance  = balance.AvailableBalance,
                totalBalance      = balance.TotalBalance,
                usedBalance       = balance.UsedBalance,
                timeout           = session.TimeoutSeconds,
                menuItems,
                config = new
                {
                    itemsPerPage          = _canteenConfig.ItemsPerPage,
                    keypadNextPage        = _canteenConfig.KeypadNextPage,
                    keypadPrevPage        = _canteenConfig.KeypadPrevPage,
                    requireConfirmation   = _canteenConfig.RequireConfirmation,
                    successScreenDuration = _canteenConfig.SuccessScreenDuration
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting order session");
            return Json(new { success = false, message = "সেশন শুরু করতে সমস্যা হয়েছে" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ProcessKeypadInput([FromBody] KeypadInputRequest request, CancellationToken ct)
    {
        try
        {
            var session = await _sessions.GetAsync(request.SessionId, ct);
            if (session is null) return Json(new { success = false, message = "সেশন মেয়াদ শেষ হয়ে গেছে", expired = true });
            if (session.IsExpired)
            {
                await _sessions.RemoveAsync(request.SessionId, ct);
                return Json(new { success = false, message = "টাইম আউট! আবার শুরু করুন।", expired = true });
            }

            session.LastActivity = DateTime.Now;

            if (request.Key == _canteenConfig.KeypadNextPage)
            {
                session.CurrentPage++;
                await _sessions.SetAsync(session, SessionTtl, ct);
                return Json(new { success = true, action = "page_next", currentPage = session.CurrentPage, remainingSeconds = session.RemainingSeconds });
            }
            if (request.Key == _canteenConfig.KeypadPrevPage)
            {
                session.CurrentPage = Math.Max(1, session.CurrentPage - 1);
                await _sessions.SetAsync(session, SessionTtl, ct);
                return Json(new { success = true, action = "page_prev", currentPage = session.CurrentPage, remainingSeconds = session.RemainingSeconds });
            }
            if (!int.TryParse(request.Key, out var digit) || digit < 0 || digit > 9)
                return Json(new { success = false, message = "ভুল ইনপুট" });

            int itemNumber = digit == 0
                ? session.CurrentPage * _canteenConfig.ItemsPerPage
                : ((session.CurrentPage - 1) * _canteenConfig.ItemsPerPage) + digit;

            session.InputSequence += digit.ToString();
            var result = await TryAddItemToSession(session, itemNumber, ct);
            await _sessions.SetAsync(session, SessionTtl, ct);
            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing keypad input");
            return Json(new { success = false, message = "ইনপুট প্রসেস করতে সমস্যা হয়েছে" });
        }
    }

    private async Task<object> TryAddItemToSession(OrderSession session, int itemNumber, CancellationToken ct)
    {
        var menuItem = await _sessionData.GetDailyMenuByPositionAsync(itemNumber, ct);
        if (menuItem is null)
            return new { success = false, message = $"আইটেম নম্বর {itemNumber} উপলব্ধ নেই", inputSequence = session.InputSequence, remainingSeconds = session.RemainingSeconds };

        if (menuItem.AvailableQuantity <= 0)
            return new { success = false, message = $"{menuItem.FoodItem.ItemName} - স্টক শেষ", outOfStock = true, inputSequence = session.InputSequence, remainingSeconds = session.RemainingSeconds };

        var itemPrice = menuItem.FoodItem.Price;
        if (session.TotalAmount + itemPrice > session.AvailableBalance)
            return new { success = false, message = $"অপর্যাপ্ত ব্যালেন্স। আরও ৳{itemPrice:F2} প্রয়োজন।", balanceExceeded = true, inputSequence = session.InputSequence, remainingSeconds = session.RemainingSeconds };

        var existing = session.Items.FirstOrDefault(i => i.DailyMenuID == menuItem.DailyMenuID);
        if (existing is not null) { existing.Quantity++; }
        else
        {
            session.Items.Add(new OrderSessionItem
            {
                DailyMenuID = menuItem.DailyMenuID, FoodItemID = menuItem.FoodItemID,
                ItemName = menuItem.FoodItem.ItemName, Quantity = 1,
                UnitPrice = menuItem.FoodItem.Price, ItemNumber = itemNumber
            });
        }

        return new
        {
            success = true, action = "item_added",
            itemName = menuItem.FoodItem.ItemName, itemNumber, itemPrice = menuItem.FoodItem.Price,
            totalAmount = session.TotalAmount,
            remainingBalance = session.AvailableBalance - session.TotalAmount,
            inputSequence = session.InputSequence,
            items = session.Items.Select(i => new
            {
                itemNumber = i.ItemNumber, itemName = i.ItemName,
                quantity = i.Quantity, unitPrice = i.UnitPrice, totalPrice = i.TotalPrice
            }).ToList(),
            remainingSeconds = session.RemainingSeconds
        };
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmOrder([FromBody] ConfirmOrderRequest request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(request.SessionId, cancellationToken);
        if (session is null) return Json(new { success = false, message = "সেশন মেয়াদ শেষ" });
        if (session.Items.Count == 0) return Json(new { success = false, message = "কোনো আইটেম নেই" });

        var placeOrder = new PlaceOrderCommand(new PlaceOrderRequestDto
        {
            UserId         = session.UserId,
            UserIdentifier = session.UserId,
            UserType       = session.UserType,
            IdempotencyKey = request.SessionId,
            Items = session.Items.Select(i => new OrderItemRequestDto
            {
                FoodItemId  = i.FoodItemID,
                DailyMenuId = i.DailyMenuID,
                Quantity    = i.Quantity
            }).ToList()
        });

        var result = await _dispatcher.SendAsync(placeOrder, cancellationToken);
        if (!result.Success) return Json(new { success = false, message = result.Message });

        await _sessions.RemoveAsync(request.SessionId, cancellationToken);
        return Json(new
        {
            success = true, message = result.Message,
            orderId = result.OrderId, orderNumber = result.OrderNumber,
            inputSequence = session.InputSequence,
            totalAmount = result.TotalAmount, remainingBalance = result.RemainingBalance
        });
    }

    [HttpPost]
    public async Task<IActionResult> CancelSession([FromBody] ConfirmOrderRequest request, CancellationToken ct)
    {
        await _sessions.RemoveAsync(request.SessionId, ct);
        return Json(new { success = true, message = "অর্ডার বাতিল হয়েছে" });
    }
}

public class VerifyRequest { public string Identifier { get; set; } = string.Empty; }
public class KeypadInputRequest { public string SessionId { get; set; } = string.Empty; public string Key { get; set; } = string.Empty; }
public class ConfirmOrderRequest { public string SessionId { get; set; } = string.Empty; }
