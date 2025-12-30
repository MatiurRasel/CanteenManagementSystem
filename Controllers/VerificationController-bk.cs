//using CanteenManagementSystem.Data;
//using CanteenManagementSystem.Models;
//using CanteenManagementSystem.Models.ViewModels;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace CanteenManagementSystem.Controllers
//{
//    public class VerificationController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IConfiguration _configuration;

//        public VerificationController(ApplicationDbContext context, IConfiguration configuration)
//        {
//            _context = context;
//            _configuration = configuration;
//        }

//        // Main verification page (Kiosk Entry Point)
//        public IActionResult Index()
//        {
//            return View();
//        }

//        // Verify user by card/barcode/QR/NFC
//        [HttpPost]
//        public async Task<IActionResult> VerifyUser([FromBody] VerifyRequest request)
//        {
//            try
//            {
//                var identifier = request.Identifier?.Trim();

//                if (string.IsNullOrEmpty(identifier))
//                {
//                    return Json(new VerificationViewModel
//                    {
//                        Success = false,
//                        Message = "অনুগ্রহ করে আপনার কার্ড স্ক্যান করুন বা ID লিখুন"
//                    });
//                }

//                // Try to find student first
//                var student = await _context.StudentInfo_Canteens
//                    .FirstOrDefaultAsync(s =>
//                        (s.StudentIDC == identifier));

//                if (student != null)
//                {
//                    return await GetUserVerificationData(student.StudentID, CanteenUserType.Student);
//                }

//                // Try to find employee
//                var employee = await _context.EmployeeInfo_Canteens
//                    .FirstOrDefaultAsync(e =>
//                        (e.EmployeeID == identifier || e.EmployeeID.ToString() == identifier));

//                if (employee != null)
//                {
//                    return await GetUserVerificationData(employee.EmployeeID, CanteenUserType.Employee,employee.EmployeeTypeName);
//                }

//                return Json(new VerificationViewModel
//                {
//                    Success = false,
//                    Message = "ব্যবহারকারী খুঁজে পাওয়া যায়নি। অনুগ্রহ করে আবার চেষ্টা করুন।"
//                });
//            }
//            catch (Exception ex)
//            {
//                return Json(new VerificationViewModel
//                {
//                    Success = false,
//                    Message = "একটি ত্রুটি ঘটেছে। অনুগ্রহ করে আবার চেষ্টা করুন।"
//                });
//            }
//        }

//        //studentId
//        private async Task<JsonResult> GetUserVerificationData(string userId, CanteenUserType userType, string employeeTypeName = "")
//        {
//            var azureStorageUrl = _configuration.GetValue<string>("AzureStorageUrl").TrimEnd('/') ?? "";
//            var clientSettings = _context.UTClientSettings.FirstOrDefault().UserVal;
//            // Get or create user balance
//            var balance = await _context.UserBalances
//                .FirstOrDefaultAsync(b => b.UserId == userId && b.UserType == userType);

//            decimal monthlyLimit = _configuration.GetValue<decimal>("CanteenSettings:DefaultMonthlyLimit", 2000);
//            if (userType == CanteenUserType.Employee)
//            {
//                if(employeeTypeName.Equals("TEACHER", StringComparison.CurrentCultureIgnoreCase))
//                {
//                    var percentage = _configuration.GetValue<int>("CanteenSettings:TeacherLimitPercentage", 100);
//                    monthlyLimit = monthlyLimit * percentage / 100;
//                }

//                if (employeeTypeName.Equals("STAFF", StringComparison.CurrentCultureIgnoreCase))
//                {
//                    var percentage = _configuration.GetValue<int>("CanteenSettings:StaffLimitPercentage", 100);
//                    monthlyLimit = monthlyLimit * percentage / 100;
//                }

//            }


//            if (balance == null)
//            {
//                balance = new UserBalance
//                {
//                    UserId = userId,
//                    UserType = userType,
//                    TotalBalance = monthlyLimit,
//                    UsedBalance = 0
//                };
//                _context.UserBalances.Add(balance);
//                await _context.SaveChangesAsync();
//            }

//            // Get today's menu
//            var todayMenu = await _context.DailyMenus
//                .Include(dm => dm.FoodItem)
//                .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
//                .OrderBy(dm => dm.DisplayOrder)
//                .Select(dm => new MenuItemViewModel
//                {
//                    DailyMenuID = dm.DailyMenuID,
//                    FoodItemID = dm.FoodItemID,
//                    ItemName = dm.FoodItem.ItemName,
//                    Description = dm.FoodItem.Description ?? "",
//                    Price = dm.FoodItem.Price,
//                    Category = dm.FoodItem.Category,
//                    AvailableQuantity = dm.AvailableQuantity,
//                    InStock = dm.AvailableQuantity > 0,
//                    ImageUrl = dm.FoodItem.ImageUrl ?? ""
//                })
//                .ToListAsync();

//            var viewModel = new VerificationViewModel
//            {
//                Success = true,
//                Message = "সফলভাবে যাচাই হয়েছে",
//                UserType = userType,
//                TotalBalance = balance.TotalBalance,
//                UsedBalance = balance.UsedBalance,
//                AvailableBalance = balance.AvailableBalance,
//                TodayMenu = todayMenu
//            };

//            if (userType == CanteenUserType.Student && !string.IsNullOrWhiteSpace(userId))
//            {
//                var student = await _context.StudentInfo_Canteens.FirstOrDefaultAsync(x=>x.StudentID == userId);
//                if (student != null)
//                {
//                    viewModel.UserId = student.StudentIDC;
//                    viewModel.UserName = student.StudentName ?? "";
//                    viewModel.UserPhotoUrl = !string.IsNullOrWhiteSpace(student.PhotoPathS) && student.PhotoPathS != "--" && student.PhotoPathS != "-"
//                ? $"{azureStorageUrl}/{clientSettings}/{student.PhotoPathS.TrimStart('/')}" : student.PhotoPathS;
//                    viewModel.UserMobileNo = student.ContactNo ?? "";
//                    viewModel.AcademicInformation = $"Program: {student.ProgramID};Version: {student.VersionName}; Session: {student.SessionName}; Section: {student.SectionName}; Gender: {student.StudentGender};";
//                    //viewModel.UserIdentifier = student.StudentIDC ?? "";
//                    //viewModel.ClassName = $"{student.ClassName} - {student.Section}";
//                }
//            }
//            else if (userType == CanteenUserType.Employee && !string.IsNullOrWhiteSpace(userId))
//            {
//                var employee = await _context.EmployeeInfo_Canteens.FirstOrDefaultAsync(x=>x.EmployeeID == userId);
//                if (employee != null)
//                {
//                    viewModel.UserId = employee.EmployeeID;
//                    viewModel.UserName = employee.EmployeeName ?? "";
//                    viewModel.UserIdentifier = employee.EmployeeID ?? "";
//                    viewModel.UserPhotoUrl = !string.IsNullOrWhiteSpace(employee.EmployeePhotoPath) && employee.EmployeePhotoPath != "--" && employee.EmployeePhotoPath != "-"
//                ? $"{azureStorageUrl}/{clientSettings}/{employee.EmployeePhotoPath.TrimStart('/')}" : employee.EmployeePhotoPath;
//                    viewModel.UserMobileNo = employee.MobileNo ?? "";
//                    viewModel.AcademicInformation = $"Designation: {employee.DesignationName}; Type: {employee.EmployeeTypeName}; Gender: {employee.EmployeeGender}";
//                }
//            }

//            return Json(viewModel);
//        }
//    }
//    public class VerifyRequest
//    {
//        public string Identifier { get; set; } = string.Empty;
//    }
//}
