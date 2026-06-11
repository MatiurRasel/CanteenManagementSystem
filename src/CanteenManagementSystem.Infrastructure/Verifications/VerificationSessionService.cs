// =============================================================================
// VerificationSessionService  (CanteenManagementSystem.Infrastructure.Verifications)
// -----------------------------------------------------------------------------
// IVerificationSessionService impl. Repos only — no IAppDbContext. UoW commits
// the EnsureMonthlyBalance write.
// =============================================================================

using CanteenManagementSystem.Application.Verifications;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Verifications;

public sealed class VerificationSessionService : IVerificationSessionService
{
    private readonly IReadOnlyRepository<Student>  _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IReadOnlyRepository<DailyMenu> _menus;
    private readonly IRepository<UserBalance> _balances;
    private readonly IUnitOfWork _uow;

    public VerificationSessionService(
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IReadOnlyRepository<DailyMenu> menus,
        IRepository<UserBalance> balances,
        IUnitOfWork uow)
    {
        _students = students; _employees = employees; _menus = menus;
        _balances = balances; _uow = uow;
    }

    public async Task<ScannedUser?> LookupByIdentifierAsync(string identifier, CancellationToken ct = default)
    {
        var student = await _students.NoTrackingQuery()
            .FirstOrDefaultAsync(s => s.CardIdentifier == identifier || s.ExternalId == identifier, ct);
        if (student is not null)
        {
            return new ScannedUser(
                student.ExternalId, student.Name, CanteenUserType.Student,
                student.PhotoPath ?? string.Empty,
                $"{student.Program}, {student.Session}",
                null);
        }

        var employee = await _employees.NoTrackingQuery()
            .FirstOrDefaultAsync(e => e.CardIdentifier == identifier || e.ExternalId == identifier, ct);
        if (employee is null) return null;

        return new ScannedUser(
            employee.ExternalId, employee.Name, CanteenUserType.Employee,
            employee.PhotoPath ?? string.Empty,
            $"{employee.Designation}, {employee.EmployeeType}",
            employee.EmployeeType);
    }

    public async Task<UserBalance> EnsureMonthlyBalanceAsync(string userId, CanteenUserType userType, decimal monthlyLimit, CancellationToken ct = default)
    {
        var balance = await _balances.FirstOrDefaultAsync(
            b => b.UserId == userId && b.UserType == userType, ct);

        if (balance is null)
        {
            balance = new UserBalance
            {
                UserId = userId, UserType = userType,
                TotalBalance = monthlyLimit, UsedBalance = 0, LastUpdated = DateTime.Now
            };
            await _balances.AddAsync(balance, ct);
            await _uow.SaveChangesAsync(ct);
        }
        else
        {
            var today = DateTime.Today;
            if (balance.LastUpdated.Month != today.Month || balance.LastUpdated.Year != today.Year)
            {
                balance.TotalBalance = monthlyLimit;
                balance.UsedBalance  = 0;
                balance.LastUpdated  = DateTime.Now;
                _balances.Update(balance);
                await _uow.SaveChangesAsync(ct);
            }
        }
        return balance;
    }

    public async Task<IReadOnlyList<MenuTile>> GetTodayMenuTilesAsync(CancellationToken ct = default)
        => await _menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
            .OrderBy(dm => dm.DisplayOrder)
            .Select(dm => new MenuTile(
                dm.DailyMenuID, dm.FoodItemID, dm.FoodItem.ItemName, dm.FoodItem.Price,
                dm.AvailableQuantity, dm.IsAvailable && dm.AvailableQuantity > 0,
                dm.FoodItem.Category))
            .ToListAsync(ct);

    public Task<DailyMenu?> GetDailyMenuByPositionAsync(int itemNumber, CancellationToken ct = default)
        => _menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == DateTime.Today && dm.IsAvailable)
            .OrderBy(dm => dm.DisplayOrder)
            .Skip(itemNumber - 1)
            .FirstOrDefaultAsync(ct);
}
