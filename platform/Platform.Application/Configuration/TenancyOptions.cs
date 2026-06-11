namespace Platform.Application.Configuration;

/// Bound from the "Tenancy" section. Per-deployment defaults used by the
/// tenant resolver when no explicit tenant header / subdomain is present.
public class TenancyOptions
{
    /// <summary>Numeric tenant id stamped onto rows when no tenant is resolved.</summary>
    public int DefaultClientId { get; set; } = 1;

    /// <summary>URL-safe tenant code (matches Client.ClientCode).</summary>
    public string DefaultClientCode { get; set; } = "DEFAULT";

    /// <summary>Order to walk tenant resolvers. Comma-separated: Header,Subdomain,Default.</summary>
    public string ResolutionStrategy { get; set; } = "Header,Subdomain,Default";
}
