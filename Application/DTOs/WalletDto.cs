namespace CanteenManagementSystem.Application.DTOs
{
    public class WalletDto
    {
        public Guid WalletId { get; set; }
        public Guid UserId { get; set; }
        public decimal MainBalance { get; set; }
        public decimal EmergencyBalanceLimit { get; set; }
        public decimal EmergencyBalanceUsed { get; set; }
        public decimal EmergencyAvailable => EmergencyBalanceLimit - EmergencyBalanceUsed;
        public decimal BlockedAmount { get; set; }
        public decimal AvailableBalance => MainBalance + EmergencyAvailable;
        public decimal SpendableBalance => AvailableBalance - BlockedAmount;
        public decimal LifetimeRecharge { get; set; }
        public decimal LifetimeSpent { get; set; }
        public DateTime? LastRechargeAt { get; set; }
    }

    public class RechargeRequest
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "ONLINE";
    }

    public class RechargeResponse
    {
        public Guid RechargeId { get; set; }
        public string OrderReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal EmergencyDebt { get; set; }
        public decimal NetCredit { get; set; }
        public string? PaymentGatewayUrl { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TransactionDto
    {
        public Guid TransactionId { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

