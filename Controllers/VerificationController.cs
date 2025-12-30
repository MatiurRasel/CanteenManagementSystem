using CanteenManagementSystem.Data;
using CanteenManagementSystem.Models;
using CanteenManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Controllers
{
    public class VerificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VerificationController> _logger;

        public VerificationController(ApplicationDbContext context, IConfiguration configuration, ILogger<VerificationController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // Main verification page (Kiosk Entry Point)
        public IActionResult Index()
        {
            return View();
        }

        // Verify user by card/barcode/QR/NFC
        [HttpPost]
        public async Task<IActionResult> VerifyUser([FromBody] VerifyRequest request)
        {
            try
            {
                var identifier = request.Identifier?.Trim();

                if (string.IsNullOrEmpty(identifier))
                {
                    return Json(new VerificationViewModel
                    {
                        Success = false,
                        Message = "অনুগ্রহ করে আপনার কার্ড স্ক্যান করুন বা ID লিখুন"
                    });
                }

                _logger.LogInformation($"Verifying user with identifier: {identifier}");

                // Try to find student first (using StudentIDC from view)
                var student = await _context.StudentInfo_Canteens
                    .FirstOrDefaultAsync(s => s.StudentIDC == identifier);

                if (student != null)
                {
                    _logger.LogInformation($"Found student: {student.StudentName}");
                    return await GetUserVerificationData(student.StudentID, CanteenUserType.Student);
                }

                // Try to find employee (EmployeeID is string in your view)
                var employee = await _context.EmployeeInfo_Canteens
                    .FirstOrDefaultAsync(e => e.EmployeeID == identifier);

                if (employee != null)
                {
                    _logger.LogInformation($"Found employee: {employee.EmployeeName}");
                    return await GetUserVerificationData(employee.EmployeeID, CanteenUserType.Employee, employee.EmployeeTypeName);
                }

                _logger.LogWarning($"User not found with identifier: {identifier}");
                return Json(new VerificationViewModel
                {
                    Success = false,
                    Message = "ব্যবহারকারী খুঁজে পাওয়া যায়নি। অনুগ্রহ করে আবার চেষ্টা করুন।"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying user");
                return Json(new VerificationViewModel
                {
                    Success = false,
                    Message = "একটি ত্রুটি ঘটেছে। অনুগ্রহ করে আবার চেষ্টা করুন।"
                });
            }
        }

        private async Task<JsonResult> GetUserVerificationData(string userId, CanteenUserType userType, string employeeTypeName = "")
        {
            try
            {
                var azureStorageUrl = _configuration.GetValue<string>("AzureStorageUrl")?.TrimEnd('/') ?? "";
                var clientSettings = await _context.UTClientSettings
                    .FirstOrDefaultAsync();
                var clientVal = clientSettings?.UserVal;

                // Get or create user balance
                var balance = await _context.UserBalances
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.UserType == userType);

                decimal monthlyLimit = _configuration.GetValue<decimal>("CanteenSettings:DefaultMonthlyLimit", 2000);

                if (userType == CanteenUserType.Employee)
                {
                    if (employeeTypeName?.Equals("TEACHER", StringComparison.CurrentCultureIgnoreCase) == true)
                    {
                        var percentage = _configuration.GetValue<int>("CanteenSettings:TeacherLimitPercentage", 100);
                        monthlyLimit = monthlyLimit * percentage / 100;
                    }
                    else if (employeeTypeName?.Equals("STAFF", StringComparison.CurrentCultureIgnoreCase) == true)
                    {
                        var percentage = _configuration.GetValue<int>("CanteenSettings:StaffLimitPercentage", 100);
                        monthlyLimit = monthlyLimit * percentage / 100;
                    }
                }

                // If balance doesn't exist, create it
                if (balance == null)
                {
                    balance = new UserBalance
                    {
                        UserId = userId,
                        UserType = userType,
                        TotalBalance = monthlyLimit,
                        UsedBalance = 0,
                        LastUpdated = DateTime.Now
                    };
                    _context.UserBalances.Add(balance);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Reset balance at the beginning of the month if needed
                    var today = DateTime.Today;
                    if (balance.LastUpdated.Month != today.Month || balance.LastUpdated.Year != today.Year)
                    {
                        balance.TotalBalance = monthlyLimit;
                        balance.UsedBalance = 0;
                        balance.LastUpdated = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }

                // Get today's menu
                var todayMenu = await _context.DailyMenus
                    .Include(dm => dm.FoodItem)
                    .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
                    .OrderBy(dm => dm.DisplayOrder)
                    .Select(dm => new MenuItemViewModel
                    {
                        DailyMenuID = dm.DailyMenuID,
                        FoodItemID = dm.FoodItemID,
                        ItemName = dm.FoodItem.ItemName,
                        Description = dm.FoodItem.Description ?? "",
                        Price = dm.FoodItem.Price,
                        Category = dm.FoodItem.Category,
                        AvailableQuantity = dm.AvailableQuantity,
                        InStock = dm.AvailableQuantity > 0,
                        ImageUrl = dm.FoodItem.ImageUrl ?? ""
                    })
                    .ToListAsync();

                var viewModel = new VerificationViewModel
                {
                    Success = true,
                    Message = "সফলভাবে যাচাই হয়েছে",
                    UserType = userType,
                    TotalBalance = balance.TotalBalance,
                    UsedBalance = balance.UsedBalance,
                    AvailableBalance = balance.AvailableBalance,
                    TodayMenu = todayMenu
                };

                // Populate user-specific information
                if (userType == CanteenUserType.Student)
                {
                    var student = await _context.StudentInfo_Canteens
                        .FirstOrDefaultAsync(s => s.StudentID == userId);

                    if (student != null)
                    {
                        viewModel.UserId = student.StudentID;
                        viewModel.UserIdentifier = student.StudentIDC ?? student.StudentID;
                        viewModel.UserName = student.StudentName ?? "";
                        viewModel.UserPhotoUrl = !string.IsNullOrWhiteSpace(student.PhotoPathS) &&
                            student.PhotoPathS != "--" && student.PhotoPathS != "-"
                            ? $"{azureStorageUrl}/{clientVal}/{student.PhotoPathS.TrimStart('/')}"
                            : "/images/default_logo.png";
                        viewModel.UserMobileNo = student.ContactNo ?? "";
                        viewModel.UserGender = student.StudentGender;
                        viewModel.AcademicInformation = $"Program: {student.ProgramName ?? "N/A"}; " +
                            $"Version: {student.VersionName ?? "N/A"}; " +
                            $"Session: {student.SessionName ?? "N/A"}; " +
                            $"Section: {student.SectionName ?? "N/A"}";
                    }
                }
                else if (userType == CanteenUserType.Employee)
                {
                    var employee = await _context.EmployeeInfo_Canteens
                        .FirstOrDefaultAsync(e => e.EmployeeID == userId);

                    if (employee != null)
                    {
                        viewModel.UserId = employee.EmployeeID;
                        viewModel.UserIdentifier = employee.EmployeeID;
                        viewModel.UserName = employee.EmployeeName ?? "";
                        viewModel.UserPhotoUrl = !string.IsNullOrWhiteSpace(employee.EmployeePhotoPath) &&
                            employee.EmployeePhotoPath != "--" && employee.EmployeePhotoPath != "-"
                            ? $"{azureStorageUrl}/{clientVal}/{employee.EmployeePhotoPath.TrimStart('/')}"
                            : "/images/default_logo.png";
                        viewModel.UserMobileNo = employee.MobileNo ?? "";
                        viewModel.UserGender = employee.EmployeeGender;
                        viewModel.EmployeeTypeName = employee.EmployeeTypeName ?? "";

                        viewModel.AcademicInformation = $"Designation: {employee.DesignationName ?? "N/A"}; " +
                            $"Type: {employee.EmployeeTypeName ?? "N/A"}";
                    }
                }

                return Json(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user verification data for {userId}");
                return Json(new VerificationViewModel
                {
                    Success = false,
                    Message = "ব্যবহারকারী তথ্য লোড করতে সমস্যা হয়েছে।"
                });
            }
        }
    }

    public class VerifyRequest
    {
        public string Identifier { get; set; } = string.Empty;
    }
}