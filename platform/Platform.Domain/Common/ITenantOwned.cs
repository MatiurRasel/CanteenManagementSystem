namespace Platform.Domain.Common;

/// Marker for entities partitioned by tenant. The ClientId is enforced as a
/// shadow property via the EF model so domain code stays clean.
public interface ITenantOwned;
