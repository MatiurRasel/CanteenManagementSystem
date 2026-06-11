// =============================================================================
// DiscountRuleAdminService  (Platform.Infrastructure.Pricing)
// -----------------------------------------------------------------------------
// IDiscountRuleAdminService impl. ADR 0004 — uses IUnitOfWork, no DbContext.
// =============================================================================

using Platform.Application.Abstractions.Pricing;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Pricing;

namespace Platform.Infrastructure.Pricing;

public sealed class DiscountRuleAdminService : IDiscountRuleAdminService
{
    private readonly IUnitOfWork _uow;
    public DiscountRuleAdminService(IUnitOfWork uow) => _uow = uow;

    private IRepository<DiscountRule> Repo => _uow.Repository<DiscountRule>();

    public async Task<IReadOnlyList<DiscountRule>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Repo.ListAsync(cancellationToken);
        return rows
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.Priority)
            .ToList();
    }

    public Task<DiscountRule?> GetByIdAsync(int ruleId, CancellationToken cancellationToken = default)
        => Repo.FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);

    public async Task<Result<DiscountRule>> SaveAsync(DiscountRule input, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        if (input.RuleId == 0)
        {
            input.CreatedAtUtc = now;
            await Repo.AddAsync(input, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Success(input);
        }

        var existing = await Repo.FirstOrDefaultAsync(r => r.RuleId == input.RuleId, cancellationToken);
        if (existing is null) return Result.Failure<DiscountRule>(Error.NotFound("Discount rule not found."));

        existing.RuleCode      = input.RuleCode;
        existing.DisplayName   = input.DisplayName;
        existing.Description   = input.Description;
        existing.ConditionJson = input.ConditionJson;
        existing.EffectJson    = input.EffectJson;
        existing.Priority      = input.Priority;
        existing.IsActive      = input.IsActive;
        existing.ValidFromUtc  = input.ValidFromUtc;
        existing.ValidUntilUtc = input.ValidUntilUtc;
        existing.UpdatedAtUtc  = now;
        Repo.Update(existing);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(existing);
    }

    public async Task<Result> ToggleActiveAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        var row = await Repo.FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);
        if (row is null) return Result.Failure(Error.NotFound("Discount rule not found."));
        row.IsActive = !row.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        Repo.Update(row);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        var row = await Repo.FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);
        if (row is null) return Result.Failure(Error.NotFound("Discount rule not found."));
        Repo.Remove(row);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
