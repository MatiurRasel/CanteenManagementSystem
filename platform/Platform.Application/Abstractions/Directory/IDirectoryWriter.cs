// =============================================================================
// IDirectoryWriter  (Platform.Application.Abstractions.Directory)
// -----------------------------------------------------------------------------
// Strategy seam for "where do we persist the directory inside the product's
// own DB?". The canteen impl writes to Students / Employees; a rent product
// would write to Tenants / Residents.
//
// IDEMPOTENCY
//   The writer compares each incoming row's SHA-256 hash against the stored
//   SourceHash. Equal hash = no-op. Different hash = UPDATE. Missing local row
//   = INSERT. When DirectoryDelta.IsFullSnapshot is true, local rows NOT
//   present in the incoming batch get IsActive=false (soft delete).
// =============================================================================

namespace Platform.Application.Abstractions.Directory;

public interface IDirectoryWriter
{
    Task<DirectoryWriteResult> WriteAsync(DirectoryDelta delta, CancellationToken cancellationToken = default);
}
