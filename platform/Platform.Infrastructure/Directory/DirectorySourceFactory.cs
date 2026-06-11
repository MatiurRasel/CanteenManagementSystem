// =============================================================================
// DirectorySourceFactory  (Platform.Infrastructure.Directory)
// -----------------------------------------------------------------------------
// Reads CanteenTenantSettings.Directory.* for the CURRENT tenant context and
// builds the matching IDirectorySource. Called per-sync, not cached — tenant
// admins flip Source / credentials and we want the next sync to honour it.
//
// SOURCE TYPES (Directory.Source key)
//   "Database" -> DatabaseDirectorySource using DB connection string
//   "Api"      -> ApiDirectorySource using API base URL + key + secret (P5)
//   "Manual"   -> ManualDirectorySource (no-op; CSV path used separately)
//   "Disabled" -> null (worker skips this tenant entirely)
//   missing    -> ManualDirectorySource (safe default for new tenants)
//
// ConnectionStrings / API keys / secrets come back already decrypted because
// they were stored with IsSecret=true (Platform.Infrastructure.Configuration
// applies the DataProtection wrap/unwrap automatically).
// =============================================================================

using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Directory;
using Platform.Infrastructure.Directory.Sources;

namespace Platform.Infrastructure.Directory;

public sealed class DirectorySourceFactory : IDirectorySourceFactory
{
    private readonly ITenantSettings _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DirectorySourceFactory> _logger;

    public DirectorySourceFactory(
        ITenantSettings settings,
        IHttpClientFactory httpFactory,
        ILogger<DirectorySourceFactory> logger)
    {
        _settings   = settings;
        _httpFactory = httpFactory;
        _logger     = logger;
    }

    public async Task<IDirectorySource?> ResolveCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var source = (await _settings.GetAsync(DirectorySettingsKeys.Source, "Manual", cancellationToken)) ?? "Manual";

        switch (source.Trim())
        {
            case "Disabled":
                return null;

            case "Database":
                var connStr = await _settings.GetAsync(DirectorySettingsKeys.DatabaseConnectionString, defaultValue: null, cancellationToken);
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    _logger.LogWarning("Tenant has Source=Database but no Directory.Database.ConnectionString — treating as Disabled.");
                    return null;
                }
                var studentsView  = (await _settings.GetAsync(DirectorySettingsKeys.DatabaseStudentsView,  "vw_StudentInfo_Canteen",  cancellationToken)) ?? "vw_StudentInfo_Canteen";
                var employeesView = (await _settings.GetAsync(DirectorySettingsKeys.DatabaseEmployeesView, "vw_EmployeeInfo_Canteen", cancellationToken)) ?? "vw_EmployeeInfo_Canteen";
                return new DatabaseDirectorySource(connStr, studentsView, employeesView);

            case "Api":
                var baseUrl = await _settings.GetAsync(DirectorySettingsKeys.ApiBaseUrl, defaultValue: null, cancellationToken);
                var apiKey  = await _settings.GetAsync(DirectorySettingsKeys.ApiKey,     defaultValue: null, cancellationToken);
                var secret  = await _settings.GetAsync(DirectorySettingsKeys.ApiSecret,  defaultValue: null, cancellationToken);
                var pageSize = await _settings.GetIntAsync(DirectorySettingsKeys.ApiPageSize, 500, cancellationToken);
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secret))
                {
                    _logger.LogWarning("Tenant has Source=Api but missing BaseUrl / Key / Secret — treating as Disabled.");
                    return null;
                }
                var http = _httpFactory.CreateClient("directory.api");
                http.BaseAddress = new Uri(baseUrl);
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return new ApiDirectorySource(http, apiKey, secret, pageSize);

            case "Manual":
            default:
                return new ManualDirectorySource();
        }
    }
}
