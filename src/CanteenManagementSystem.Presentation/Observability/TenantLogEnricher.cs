// =============================================================================
// TenantLogEnricher  (CanteenManagementSystem.Presentation.Observability)
// -----------------------------------------------------------------------------
// Stamps `Tenant=<ClientCode>` and `TenantId=<ClientId>` on every Serilog
// event. Reads the per-request ITenantContext from the IHttpContextAccessor
// scope so log searches in Seq / Loki / ELK can filter by tenant.
//
// Background / non-request events (hosted services, seeders, console logs
// during startup) emit `Tenant=null` — that's the desired behaviour.
// =============================================================================

using Microsoft.AspNetCore.Http;
using Platform.Domain.Tenancy;
using Serilog.Core;
using Serilog.Events;

namespace CanteenManagementSystem.Presentation.Observability;

public sealed class TenantLogEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _accessor;
    public TenantLogEnricher(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var tenant = _accessor.HttpContext?.RequestServices?.GetService(typeof(ITenantContext)) as ITenantContext;
        if (tenant is null || !tenant.IsResolved) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Tenant",   tenant.ClientCode));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenant.ClientId));
    }
}
