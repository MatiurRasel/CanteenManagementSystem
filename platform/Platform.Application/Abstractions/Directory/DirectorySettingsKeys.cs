// =============================================================================
// DirectorySettingsKeys  (Platform.Application.Abstractions.Directory)
// -----------------------------------------------------------------------------
// Canonical list of every tenant-setting key the directory subsystem reads or
// writes. Keys are namespaced "Directory.*" so a single Set<> filter in the
// admin UI can list "all directory config for this tenant".
//
// USAGE — read via ITenantSettings:
//   await settings.GetAsync(DirectorySettingsKeys.Source, defaultValue: "Manual")
//
// SECRETS
//   ConnectionString / ApiSecret / ApiKey MUST be persisted with IsSecret=true
//   so they're DataProtection-encrypted at rest. Callers in source impls /
//   admin endpoints MUST pass isSecret=true to SetAsync.
// =============================================================================

namespace Platform.Application.Abstractions.Directory;

public static class DirectorySettingsKeys
{
    /// <summary>"Database" | "Api" | "Manual" | "Disabled". Default "Manual".</summary>
    public const string Source = "Directory.Source";

    /// <summary>Encrypted ADO.NET connection string. Only when Source="Database".</summary>
    public const string DatabaseConnectionString = "Directory.Database.ConnectionString";

    /// <summary>"SqlServer" today. Future: "MySql" / "Postgres".</summary>
    public const string DatabaseProvider = "Directory.Database.Provider";

    /// <summary>Students view name. Default "vw_StudentInfo_Canteen".</summary>
    public const string DatabaseStudentsView = "Directory.Database.StudentsView";

    /// <summary>Employees view name. Default "vw_EmployeeInfo_Canteen".</summary>
    public const string DatabaseEmployeesView = "Directory.Database.EmployeesView";

    /// <summary>School portal API base URL. Only when Source="Api".</summary>
    public const string ApiBaseUrl = "Directory.Api.BaseUrl";

    /// <summary>Encrypted public API key (X-App-Key header). Only when Source="Api".</summary>
    public const string ApiKey = "Directory.Api.Key";

    /// <summary>Encrypted HMAC signing secret. Only when Source="Api".</summary>
    public const string ApiSecret = "Directory.Api.Secret";

    /// <summary>Page size when paging API results. Default 500.</summary>
    public const string ApiPageSize = "Directory.Api.PageSize";

    /// <summary>Sync cadence in minutes. Default 30.</summary>
    public const string SyncIntervalMinutes = "Directory.SyncIntervalMinutes";

    /// <summary>"PerSync" or "Daily". Default "PerSync" — every sync is a full snapshot.</summary>
    public const string SnapshotMode = "Directory.SnapshotMode";

    /// <summary>ISO 8601 timestamp written after every successful sync. Source uses as "since".</summary>
    public const string LastSyncAtUtc = "Directory.LastSyncAtUtc";

    /// <summary>"Healthy" | "Failing" | "Disabled". Drives admin UI badge colour + bg-worker scheduling.</summary>
    public const string HealthStatus = "Directory.HealthStatus";

    /// <summary>Count of consecutive failures since last success. Reset on success.</summary>
    public const string ConsecutiveFailures = "Directory.ConsecutiveFailures";

    /// <summary>Failure threshold before HealthStatus flips to "Failing" + alert fires. Default 3.</summary>
    public const string MaxFailuresBeforeAlert = "Directory.MaxFailuresBeforeAlert";

    /// <summary>Email + phone destinations for failure alerts, CSV. Default empty (no alert).</summary>
    public const string AlertEmail = "Directory.AlertEmail";
    public const string AlertPhone = "Directory.AlertPhone";
}
