using CanteenManagementSystem.Application.Verifications.Dtos;

namespace CanteenManagementSystem.Application.Verifications;

public interface IVerificationQueryService
{
    Task<VerificationViewModel> VerifyUserAsync(string identifier);
}
