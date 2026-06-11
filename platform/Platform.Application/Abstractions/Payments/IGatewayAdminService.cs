// =============================================================================
// IGatewayAdminService  (Platform.Application.Abstractions.Payments)
// -----------------------------------------------------------------------------
// Admin CRUD over PaymentGatewayConfig. Used by /admin/gateways (ADR 0004).
// =============================================================================

using Platform.Application.Results;
using Platform.Domain.Payments;

namespace Platform.Application.Abstractions.Payments;

public interface IGatewayAdminService
{
    Task<IReadOnlyList<PaymentGatewayConfig>> ListAsync(CancellationToken ct = default);
    Task<PaymentGatewayConfig?> GetByIdAsync(int gatewayConfigId, CancellationToken ct = default);

    /// <summary>Apply a non-destructive update — secret fields are overwritten ONLY when the input value is non-null and not the placeholder mask.</summary>
    Task<Result<PaymentGatewayConfig>> SaveAsync(int gatewayConfigId, PaymentGatewayConfig input, string secretMask, string? performedBy, CancellationToken ct = default);

    Task<Result<PaymentGatewayConfig>> ToggleEnabledAsync(int gatewayConfigId, CancellationToken ct = default);
}
