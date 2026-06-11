namespace CanteenManagementSystem.Domain.Wallets;

public enum WalletLedgerEntryType
{
    Recharge = 1,
    OrderBlock = 2,
    OrderRelease = 3,
    DeliveryDeduction = 4,
    Refund = 5,
    EmergencyConsume = 6,
    EmergencyRecover = 7,
    ManualAdjustment = 8
}
