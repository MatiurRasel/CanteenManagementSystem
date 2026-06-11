namespace Platform.Domain.Payments;

/// <summary>
/// Lifecycle for a tenant's gateway configuration. Mirrors the CC24
/// GatewayConfig.Status pattern so admin UIs can show the right badge:
/// <list type="bullet">
///   <item><c>1 Configured</c> — credentials saved, not yet tested.</item>
///   <item><c>2 Testing</c> — sandbox connectivity verified, awaiting go-live.</item>
///   <item><c>3 Live</c> — live credentials verified and accepting traffic.</item>
///   <item><c>4 Disabled</c> — temporarily turned off by the operator.</item>
/// </list>
/// </summary>
public enum CanteenGatewayStatus : byte
{
    Configured = 1,
    Testing    = 2,
    Live       = 3,
    Disabled   = 4
}
