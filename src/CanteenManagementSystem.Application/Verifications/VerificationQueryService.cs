// =============================================================================
// VerificationQueryService  (CanteenManagementSystem.Application.Verifications)
// -----------------------------------------------------------------------------
// ADR 0004: IUnitOfWork + IReadOnlyRepository<T> / IRepository<T>. The only
// write here is the initial UserBalance bootstrap (and the monthly cap reset)
// — UoW commits it explicitly.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Configuration;
using CanteenManagementSystem.Application.Tenancy;
using CanteenManagementSystem.Application.Verifications.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanteenManagementSystem.Application.Verifications;

public class VerificationQueryService : IVerificationQueryService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IReadOnlyRepository<DailyMenu> _menus;
    private readonly IConfiguration _configuration;
    private readonly IClientCacheService _clientCache;
    private readonly CanteenConfiguration _canteenConfig;
    private readonly ILogger<VerificationQueryService> _logger;

    public VerificationQueryService(
        IUnitOfWork uow,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IReadOnlyRepository<DailyMenu> menus,
        IConfiguration configuration,
        IClientCacheService clientCache,
        IOptions<CanteenConfiguration> canteenConfig,
        ILogger<VerificationQueryService> logger)
    {
        _uow = uow;
        _students = students;
        _employees = employees;
        _menus = menus;
        _configuration = configuration;
        _clientCache = clientCache;
        _canteenConfig = canteenConfig.Value;
        _logger = logger;
    }

    private IRepository<UserBalance> Balances => _uow.Repository<UserBalance>();

    public async Task<VerificationViewModel> VerifyUserAsync(string identifier)
    {
        identifier = identifier?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(identifier))
        {
            return new VerificationViewModel { Success = false, Message = "অনুগ্রহ করে কার্ড স্ক্যান করুন বা ID লিখুন" };
        }

        _logger.LogInformation("Verifying user with identifier: {Identifier}", identifier);

        var student = await _students.NoTrackingQuery()
            .FirstOrDefaultAsync(s => s.CardIdentifier == identifier || s.ExternalId == identifier);
        if (student is not null)
        {
            _logger.LogInformation("Found student: {Name}", student.Name);
            return await GetUserVerificationDataAsync(student.ExternalId, CanteenUserType.Student);
        }

        var employee = await _employees.NoTrackingQuery()
            .FirstOrDefaultAsync(e => e.CardIdentifier == identifier || e.ExternalId == identifier);
        if (employee is not null)
        {
            _logger.LogInformation("Found employee: {Name}", employee.Name);
            return await GetUserVerificationDataAsync(employee.ExternalId, CanteenUserType.Employee, employee.EmployeeType);
        }

        _logger.LogWarning("User not found with identifier: {Identifier}", identifier);
        return new VerificationViewModel { Success = false, Message = "ব্যবহারকারী খুঁজে পাওয়া যায়নি" };
    }

    private async Task<VerificationViewModel> GetUserVerificationDataAsync(string userId, CanteenUserType userType, string? employeeTypeName = "")
    {
        try
        {
            // Photo URLs go through IClientCacheService.GetPhotoUrl which reads the
            // primed "Branding.PhotoContainer" tenant setting from memory cache.
            // Calling GetClientInfoAsync once primes both entries for this request.
            _ = await _clientCache.GetClientInfoAsync();

            var balance = await Balances.FirstOrDefaultAsync(b => b.UserId == userId && b.UserType == userType);

            decimal monthlyLimit = _canteenConfig.DefaultMonthlyLimit;
            if (userType == CanteenUserType.Employee)
            {
                if (string.Equals(employeeTypeName, "TEACHER", StringComparison.OrdinalIgnoreCase))
                {
                    monthlyLimit = monthlyLimit * _canteenConfig.TeacherLimitPercentage / 100;
                }
                else if (string.Equals(employeeTypeName, "STAFF", StringComparison.OrdinalIgnoreCase))
                {
                    monthlyLimit = monthlyLimit * _canteenConfig.StaffLimitPercentage / 100;
                }
            }

            if (balance is null)
            {
                balance = new UserBalance
                {
                    UserId = userId,
                    UserType = userType,
                    TotalBalance = monthlyLimit,
                    UsedBalance = 0,
                    LastUpdated = DateTime.Now
                };
                await Balances.AddAsync(balance);
                await _uow.SaveChangesAsync();
            }
            else
            {
                var today = DateTime.Today;
                if (balance.LastUpdated.Month != today.Month || balance.LastUpdated.Year != today.Year)
                {
                    balance.TotalBalance = monthlyLimit;
                    balance.UsedBalance = 0;
                    balance.LastUpdated = DateTime.Now;
                    await _uow.SaveChangesAsync();
                }
            }

            var todayMenu = await _menus.NoTrackingQuery()
                .Include(dm => dm.FoodItem)
                .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
                .OrderBy(dm => dm.DisplayOrder)
                .Select(dm => new MenuItemViewModel
                {
                    DailyMenuID = dm.DailyMenuID,
                    FoodItemID = dm.FoodItemID,
                    ItemName = dm.FoodItem.ItemName,
                    Description = dm.FoodItem.Description ?? string.Empty,
                    Price = dm.FoodItem.Price,
                    Category = dm.FoodItem.Category,
                    AvailableQuantity = dm.AvailableQuantity,
                    InStock = dm.AvailableQuantity > 0,
                    ImageUrl = dm.FoodItem.ImageUrl ?? string.Empty
                })
                .ToListAsync();

            var viewModel = new VerificationViewModel
            {
                Success = true,
                Message = "ব্যবহারকারী যাচাই সম্পন্ন হয়েছে",
                UserType = userType,
                TotalBalance = balance.TotalBalance,
                UsedBalance = balance.UsedBalance,
                AvailableBalance = balance.AvailableBalance,
                TodayMenu = todayMenu
            };

            if (userType == CanteenUserType.Student)
            {
                var s = await _students.FirstOrDefaultAsync(s => s.ExternalId == userId);
                if (s is not null)
                {
                    viewModel.UserId = s.ExternalId;
                    viewModel.UserIdentifier = s.CardIdentifier ?? s.ExternalId;
                    viewModel.UserName = s.Name;
                    viewModel.UserPhotoUrl = _clientCache.GetPhotoUrl(s.PhotoPath);
                    viewModel.UserMobileNo = s.ContactNo ?? string.Empty;
                    viewModel.UserGender = s.Gender ?? string.Empty;
                    viewModel.AcademicInformation = $"Program: {s.Program ?? "N/A"}; Version: {s.Version ?? "N/A"}; Session: {s.Session ?? "N/A"}; Section: {s.Section ?? "N/A"}";
                }
            }
            else if (userType == CanteenUserType.Employee)
            {
                var e = await _employees.FirstOrDefaultAsync(e => e.ExternalId == userId);
                if (e is not null)
                {
                    viewModel.UserId = e.ExternalId;
                    viewModel.UserIdentifier = e.CardIdentifier ?? e.ExternalId;
                    viewModel.UserName = e.Name;
                    viewModel.UserPhotoUrl = _clientCache.GetPhotoUrl(e.PhotoPath);
                    viewModel.UserMobileNo = e.ContactNo ?? string.Empty;
                    viewModel.UserGender = e.Gender ?? string.Empty;
                    viewModel.EmployeeTypeName = e.EmployeeType ?? string.Empty;
                    viewModel.AcademicInformation = $"Designation: {e.Designation ?? "N/A"}; Type: {e.EmployeeType ?? "N/A"}";
                }
            }

            return viewModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user verification data for {UserId}", userId);
            return new VerificationViewModel { Success = false, Message = "ব্যবহারকারী যাচাইয়ের সময় সমস্যা হয়েছে" };
        }
    }
}
