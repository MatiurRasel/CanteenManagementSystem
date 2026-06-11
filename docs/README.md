# Canteen Management System — Developer Guide

This directory documents the platform for new contributors. Read in order:

| # | Doc | Why |
|---|---|---|
| 1 | [Architecture.md](Architecture.md) | High-level layout, layer boundaries, the CQRS dispatcher. |
| 2 | [Configuration.md](Configuration.md) | The DB-first / appsettings-fallback / default config hierarchy. |
| 3 | [Caching.md](Caching.md) | Two-tier cache + output cache + tag-based invalidation. |
| 4 | [Payments.md](Payments.md) | bKash / Nagad / SSLCommerz / Stripe — flows + secrets. |
| 5 | [Notifications.md](Notifications.md) | SMS / WhatsApp / Email pipeline. |
| 6 | [Receipts.md](Receipts.md) | QuestPDF receipts + kitchen tickets + QR verify. |
| 7 | [Cards.md](Cards.md) | NFC / RFID card lifecycle. |
| 8 | [Reports.md](Reports.md) | Reporting endpoints + caching strategy. |
| 9 | [RealTime.md](RealTime.md) | SignalR hub + IOrderBroadcaster. |
| 10 | [Observability.md](Observability.md) | OpenTelemetry traces, metrics, log correlation. |
| 11 | [UI-System.md](UI-System.md) | Sneat-style design tokens, skeleton + loader patterns, mobile-first rules. |
| 12 | [Testing.md](Testing.md) | xUnit + FluentAssertions + Testcontainers strategy. |
| 13 | [Deployment.md](Deployment.md) | Docker, docker-compose, GitHub Actions, Azure target. |

## Quickstart

```bash
# 1. Clone
git clone <repo> && cd CanteenManagementSystem

# 2. Configure (any of these flows works)
#    a) edit src/CanteenManagementSystem.Presentation/appsettings.json
#    b) docker compose up -d
#    c) set ConnectionStrings__DefaultConnection env var

# 3. Apply migrations
dotnet ef migrations add Initial --project src/CanteenManagementSystem.Infrastructure --startup-project src/CanteenManagementSystem.Presentation
dotnet ef database update     --project src/CanteenManagementSystem.Infrastructure --startup-project src/CanteenManagementSystem.Presentation

# 4. Run
dotnet run --project src/CanteenManagementSystem.Presentation

# 5. Open
#    https://localhost:7093          (kiosk + admin UI)
#    https://localhost:7093/swagger  (OpenAPI)
```
