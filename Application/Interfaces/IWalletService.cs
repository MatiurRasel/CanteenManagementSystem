using CanteenManagementSystem.Application.DTOs;

namespace CanteenManagementSystem.Application.Interfaces
{
    public interface IWalletService
    {
        Task<WalletDto> GetWalletAsync(Guid userId);
        Task<RechargeResponse> InitiateRechargeAsync(Guid userId, RechargeRequest request);
        Task<bool> ProcessRechargeAsync(string orderReference, string paymentGatewayTxnId);
        Task<List<TransactionDto>> GetTransactionsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> BlockAmountAsync(Guid userId, decimal amount);
        Task<bool> DeductAmountAsync(Guid userId, decimal amount, Guid? orderId = null);
        Task<bool> UnblockAmountAsync(Guid userId, decimal amount);
    }
}

