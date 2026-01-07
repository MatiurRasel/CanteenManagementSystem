using CanteenManagementSystem.Application.Interfaces;
using CanteenManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Services
{
    public class NfcService : INfcService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWalletService _walletService;
        private readonly IOrderService _orderService;
        private readonly ILogger<NfcService> _logger;

        public NfcService(
            ApplicationDbContext context,
            IWalletService walletService,
            IOrderService orderService,
            ILogger<NfcService> logger)
        {
            _context = context;
            _walletService = walletService;
            _orderService = orderService;
            _logger = logger;
        }

        public async Task<NfcUserInfo?> AuthenticateCardAsync(string cardNumber)
        {
            var card = await _context.NfcCards
                .Include(c => c.User)
                .ThenInclude(u => u!.UserBalance)
                .FirstOrDefaultAsync(c => c.CardNumber == cardNumber);

            if (card == null || card.CardStatus != "ACTIVE" || card.User == null)
                return null;

            // Check if card is expired
            if (card.ExpiresAt.HasValue && card.ExpiresAt.Value < DateTime.UtcNow)
                return null;

            // Update last used
            card.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Get wallet
            var wallet = card.User.UserBalance != null
                ? await _walletService.GetWalletAsync(card.User.UserId)
                : null;

            // Get active orders
            var activeOrders = await _orderService.GetUserOrdersAsync(card.User.UserId);
            activeOrders = activeOrders
                .Where(o => o.OrderStatus == "READY" || o.OrderStatus == "PREPARING")
                .ToList();

            return new NfcUserInfo
            {
                UserId = card.User.UserId,
                FullName = card.User.FullName,
                Role = card.User.Role,
                RollNumber = card.User.RollNumber,
                Wallet = wallet ?? new Application.DTOs.WalletDto(),
                ActiveOrders = activeOrders
            };
        }

        public async Task<bool> AssignCardAsync(string cardNumber, Guid userId)
        {
            var card = await _context.NfcCards
                .FirstOrDefaultAsync(c => c.CardNumber == cardNumber);

            if (card == null)
                return false;

            // Check if already assigned
            if (card.UserId.HasValue && card.CardStatus == "ACTIVE")
                return false;

            card.UserId = userId;
            card.CardStatus = "ACTIVE";
            card.AssignedAt = DateTime.UtcNow;
            card.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BlockCardAsync(string cardNumber)
        {
            var card = await _context.NfcCards
                .FirstOrDefaultAsync(c => c.CardNumber == cardNumber);

            if (card == null)
                return false;

            card.CardStatus = "BLOCKED";
            card.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}

