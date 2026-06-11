// =============================================================================
// IDirectorySource  (Platform.Application.Abstractions.Directory)
// -----------------------------------------------------------------------------
// Strategy seam for "where does a tenant's directory data come from?". One impl
// per supported source:
//   * DatabaseDirectorySource  — reads vw_StudentInfo_Canteen etc.
//   * ApiDirectorySource       — calls school portal API (HMAC signed)
//   * ManualDirectorySource    — reads from canteen-owned DirectoryStaging or
//                                returns whatever an admin CSV upload handed in
//
// RESOLUTION
//   IDirectorySourceFactory picks the right impl per tenant from
//   ITenantSettings (key "Directory.Source"). The orchestrator
//   (DirectorySyncService) never knows which source it's talking to.
// =============================================================================

namespace Platform.Application.Abstractions.Directory;

public interface IDirectorySource
{
    /// <summary>
    /// Pull a delta of directory rows. Implementations decide whether to honour
    /// <paramref name="sinceUtc"/> (incremental) or return everything
    /// (full snapshot) — they signal which via DirectoryDelta.IsFullSnapshot.
    /// </summary>
    Task<DirectoryDelta> FetchAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
