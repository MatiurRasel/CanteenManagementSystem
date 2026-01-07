using CanteenManagementSystem.Application.DTOs;

namespace CanteenManagementSystem.Application.Interfaces
{
    public class NfcUserInfo
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? RollNumber { get; set; }
        public WalletDto Wallet { get; set; } = null!;
        public List<OrderDto> ActiveOrders { get; set; } = new();
    }

    public interface INfcService
    {
        Task<NfcUserInfo?> AuthenticateCardAsync(string cardNumber);
        Task<bool> AssignCardAsync(string cardNumber, Guid userId);
        Task<bool> BlockCardAsync(string cardNumber);
    }
}

