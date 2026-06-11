using Xunit;

namespace CanteenManagementSystem.IntegrationTests;

/// <summary>
/// All integration tests share ONE factory (and therefore one migrated +
/// seeded database) and run sequentially within the collection — keeps the
/// wallet/order assertions deterministic.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<CanteenWebApplicationFactory>
{
    public const string Name = "integration";
}
