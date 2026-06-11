
namespace Platform.Application.Abstractions.Users;

/// Single seam for resolving user profile / photo / academic info across
/// student & employee sources. Replaces the duplicated lookup paths previously
/// scattered across VerificationQueryService and OperatorQueryService.
public interface IUserDirectory
{
    Task<UserProfileSnapshot?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<(string UserId, string UserType), UserProfileSnapshot>> BatchLookupAsync(
        IEnumerable<(string UserId, string UserType)> keys,
        CancellationToken cancellationToken = default);
}

public sealed record UserProfileSnapshot(
    string UserId,
    string UserIdentifier,
    string UserType,
    string UserName,
    string PhotoUrl,
    string MobileNo,
    string Gender,
    string AcademicInformation,
    string EmployeeTypeName);
