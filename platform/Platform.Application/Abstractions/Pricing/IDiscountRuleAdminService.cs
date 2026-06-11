// =============================================================================
// IDiscountRuleAdminService  (Platform.Application.Abstractions.Pricing)
// -----------------------------------------------------------------------------
// Admin-side CRUD over DiscountRule rows. Used by /admin/discount-rules so the
// controller never touches IAppDbContext directly (ADR 0004).
// =============================================================================

using Platform.Application.Results;
using Platform.Domain.Pricing;

namespace Platform.Application.Abstractions.Pricing;

public interface IDiscountRuleAdminService
{
    Task<IReadOnlyList<DiscountRule>> ListAsync(CancellationToken cancellationToken = default);
    Task<DiscountRule?> GetByIdAsync(int ruleId, CancellationToken cancellationToken = default);

    /// <summary>Create or update. Returns the resulting row on success.</summary>
    Task<Result<DiscountRule>> SaveAsync(DiscountRule input, CancellationToken cancellationToken = default);

    Task<Result> ToggleActiveAsync(int ruleId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int ruleId, CancellationToken cancellationToken = default);
}
