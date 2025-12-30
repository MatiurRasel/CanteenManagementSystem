using CanteenManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var todayMenu = await _context.DailyMenus
                .Include(dm => dm.FoodItem)
                .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
                .OrderBy(dm => dm.DisplayOrder)
                .Take(6)
                .ToListAsync();

            return View(todayMenu);
        }

        public IActionResult SelectUserType()
        {
            return View();
        }
    }
}
