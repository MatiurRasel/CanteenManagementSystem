# Testing

Two test projects ship as the seed; expand them as features land.

| Project | Scope | Tooling |
|---|---|---|
| `tests/CanteenManagementSystem.Domain.Tests` | Pure-domain logic, no DI / DB. | xUnit + FluentAssertions |
| `tests/CanteenManagementSystem.Application.Tests` | Validators, handlers (in-memory EF / NSubstitute). | xUnit + FluentAssertions + NSubstitute + EF InMemory |

## Running

```bash
# All tests
dotnet test CanteenManagementSystem.sln

# Single project
dotnet test tests/CanteenManagementSystem.Domain.Tests
```

CI runs `dotnet test --logger trx --collect:"XPlat Code Coverage"` and
uploads the TRX as an artifact (see `.github/workflows/build.yml`).

## What to add next

* **Integration tests** with `Testcontainers.MsSql` for `PlaceOrderCommand`
  hitting a real SQL Server in a container.
* **Webhook tests** for `PaymentOrchestrator.HandleCallbackAsync` using a
  fake `IPaymentGateway` (NSubstitute).
* **Playwright tests** for the kiosk verification flow.
