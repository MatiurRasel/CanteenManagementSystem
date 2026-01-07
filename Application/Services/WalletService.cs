using CanteenManagementSystem.Application.DTOs;
using CanteenManagementSystem.Application.Interfaces;
using CanteenManagementSystem.Domain.Entities;
using CanteenManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CanteenManagementSystem.Application.Services
{
    public class WalletService : IWalletService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public WalletService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<WalletDto> GetWalletAsync(Guid userId)
        {
            var wallet = await _context.UsersWallet
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                // Create wallet if doesn't exist
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    throw new InvalidOperationException("User not found");

                wallet = new UserBalance
                {
                    WalletId = Guid.NewGuid(),
                    UserId = userId,
                    ClientId = user.ClientId,
                    MainBalance = 0.00m,
                    EmergencyBalanceLimit = 0.00m,
                    EmergencyBalanceUsed = 0.00m,
                    BlockedAmount = 0.00m
                };

                _context.UsersWallet.Add(wallet);
                await _context.SaveChangesAsync();
            }

            return MapToDto(wallet);
        }

        public async Task<RechargeResponse> InitiateRechargeAsync(Guid userId, RechargeRequest request)
        {
            var wallet = await _context.UsersWallet
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
                throw new InvalidOperationException("Wallet not found");

            // Check emergency debt
            var emergencyDebt = wallet.EmergencyBalanceUsed;
            var netCredit = request.Amount - emergencyDebt;

            if (netCredit < 0)
                throw new InvalidOperationException("Recharge amount must cover emergency debt");

            // Generate order reference
            var orderReference = $"RCH-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";

            // Create transaction record
            var transaction = new Transaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = userId,
                ClientId = wallet.ClientId,
                WalletId = wallet.WalletId,
                TransactionType = "RECHARGE",
                Amount = request.Amount,
                BalanceBefore = wallet.MainBalance + (wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed) - wallet.BlockedAmount,
                BalanceAfter = wallet.MainBalance + (wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed) - wallet.BlockedAmount,
                PaymentMethod = request.PaymentMethod,
                PaymentReference = orderReference,
                Description = $"Recharge of ₹{request.Amount}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return new RechargeResponse
            {
                RechargeId = transaction.TransactionId,
                OrderReference = orderReference,
                Amount = request.Amount,
                EmergencyDebt = emergencyDebt,
                NetCredit = netCredit,
                PaymentGatewayUrl = $"{_configuration["PaymentGateway:BaseUrl"]}/pay?ref={orderReference}",
                Message = emergencyDebt > 0 
                    ? $"₹{emergencyDebt} will be deducted for emergency balance recovery. You'll receive ₹{netCredit}"
                    : $"You'll receive ₹{netCredit}"
            };
        }

        public async Task<bool> ProcessRechargeAsync(string orderReference, string paymentGatewayTxnId)
        {
            var transaction = await _context.Transactions
                .Include(t => t.UserBalance)
                .FirstOrDefaultAsync(t => t.PaymentReference == orderReference);

            if (transaction == null || transaction.TransactionType != "RECHARGE")
                return false;

            var wallet = transaction.UserBalance;

            // Calculate emergency recovery
            var emergencyDebt = wallet.EmergencyBalanceUsed;
            var rechargeAmount = transaction.Amount;
            var netCredit = rechargeAmount - emergencyDebt;

            // Update wallet
            wallet.MainBalance += netCredit;
            wallet.EmergencyBalanceUsed = 0; // Reset emergency balance
            wallet.LifetimeRecharge += rechargeAmount;
            wallet.LastRechargeAt = DateTime.UtcNow;
            wallet.LastTransactionAt = DateTime.UtcNow;
            wallet.UpdatedAt = DateTime.UtcNow;

            // Update transaction
            transaction.PaymentGatewayTxnId = paymentGatewayTxnId;
            transaction.BalanceAfter = wallet.MainBalance + (wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed) - wallet.BlockedAmount;
            transaction.EmergencyBalanceRecovered = emergencyDebt;
            transaction.Description = $"Recharge of ₹{rechargeAmount} processed. ₹{emergencyDebt} recovered for emergency balance.";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<TransactionDto>> GetTransactionsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Transactions
                .Where(t => t.UserId == userId);

            if (fromDate.HasValue)
                query = query.Where(t => t.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(t => t.CreatedAt <= toDate.Value);

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return transactions.Select(t => new TransactionDto
            {
                TransactionId = t.TransactionId,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                BalanceBefore = t.BalanceBefore,
                BalanceAfter = t.BalanceAfter,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();
        }

        public async Task<bool> BlockAmountAsync(Guid userId, decimal amount)
        {
            var wallet = await _context.UsersWallet
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
                return false;

            var availableBalance = wallet.MainBalance + (wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed);
            if (availableBalance - wallet.BlockedAmount < amount)
                return false;

            wallet.BlockedAmount += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeductAmountAsync(Guid userId, decimal amount, Guid? orderId = null)
        {
            var wallet = await _context.UsersWallet
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
                return false;

            // Calculate deduction from main and emergency
            var deductFromMain = Math.Min(wallet.MainBalance, amount);
            var deductFromEmergency = amount - deductFromMain;

            // Check if emergency balance is available
            var emergencyAvailable = wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed;
            if (deductFromEmergency > emergencyAvailable)
                return false;

            // Deduct from main
            wallet.MainBalance -= deductFromMain;
            wallet.EmergencyBalanceUsed += deductFromEmergency;
            wallet.BlockedAmount -= amount; // Unblock
            wallet.LifetimeSpent += amount;
            wallet.LastTransactionAt = DateTime.UtcNow;
            wallet.UpdatedAt = DateTime.UtcNow;

            // Create transaction
            var transaction = new Transaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = userId,
                ClientId = wallet.ClientId,
                WalletId = wallet.WalletId,
                TransactionType = "ORDER",
                Amount = -amount,
                BalanceBefore = wallet.MainBalance + amount + (wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed + deductFromEmergency) - wallet.BlockedAmount - amount,
                BalanceAfter = wallet.MainBalance + (wallet.EmergencyBalanceLimit - wallet.EmergencyBalanceUsed) - wallet.BlockedAmount,
                EmergencyBalanceUsedInTxn = deductFromEmergency,
                OrderId = orderId,
                Description = orderId.HasValue ? $"Order payment: ₹{amount}" : $"Deduction: ₹{amount}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnblockAmountAsync(Guid userId, decimal amount)
        {
            var wallet = await _context.UsersWallet
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null || wallet.BlockedAmount < amount)
                return false;

            wallet.BlockedAmount -= amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private WalletDto MapToDto(UserBalance wallet)
        {
            return new WalletDto
            {
                WalletId = wallet.WalletId,
                UserId = wallet.UserId,
                MainBalance = wallet.MainBalance,
                EmergencyBalanceLimit = wallet.EmergencyBalanceLimit,
                EmergencyBalanceUsed = wallet.EmergencyBalanceUsed,
                BlockedAmount = wallet.BlockedAmount,
                LifetimeRecharge = wallet.LifetimeRecharge,
                LifetimeSpent = wallet.LifetimeSpent,
                LastRechargeAt = wallet.LastRechargeAt
            };
        }
    }
}

