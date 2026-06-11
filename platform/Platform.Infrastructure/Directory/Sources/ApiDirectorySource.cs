// =============================================================================
// ApiDirectorySource  (Platform.Infrastructure.Directory.Sources)
// -----------------------------------------------------------------------------
// Calls the tenant's school portal API for student + employee directory rows.
// Uses the same HMAC scheme as our own ApiKeyAuthenticationHandler so the
// signing code matches what the portal team has to implement on the other end.
//
// HMAC SCHEME (mirrors Platform.Presentation.Auth.ApiKeyAuthorizer)
//   Headers sent on every request:
//     X-App-Key       <publicAppKey>
//     X-App-Timestamp <unix-utc-seconds>
//     X-App-Signature base64( HMAC-SHA256(
//                                 secret,
//                                 "{key}\n{ts}\n{verb}\n{path}\n{bodySha256}"))
//   bodySha256 = lowercase-hex SHA-256 of request body (empty string for GET).
//   Timestamp window ±300 s on the server.
//
// ENDPOINTS EXPECTED
//   GET {base}/students?page=N&pageSize=M
//   GET {base}/employees?page=N&pageSize=M
//   Each returns { "items": [...], "totalPages": N, "totalCount": N }.
//
// PAGINATION
//   Walks pages 1..totalPages. Stops on empty page or 1000-page cap.
//   pageSize is tenant-configurable (Directory.Api.PageSize, default 500).
//
// FAILURES
//   Non-2xx -> InvalidOperationException with status + body excerpt. Sync
//   orchestrator catches, records on the DirectorySyncRun, applies circuit
//   breaker (Phase 6).
// =============================================================================

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Platform.Application.Abstractions.Directory;

namespace Platform.Infrastructure.Directory.Sources;

public sealed class ApiDirectorySource : IDirectorySource
{
    private const int MaxPages = 1000;

    private readonly HttpClient _http;
    private readonly string _appKey;
    private readonly string _secret;
    private readonly int _pageSize;

    public ApiDirectorySource(HttpClient http, string appKey, string secret, int pageSize)
    {
        _http     = http;
        _appKey   = appKey;
        _secret   = secret;
        _pageSize = Math.Clamp(pageSize, 50, 5000);
    }

    public async Task<DirectoryDelta> FetchAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var students  = await PageThroughAsync<ApiStudent>("students",  cancellationToken);
        var employees = await PageThroughAsync<ApiEmployee>("employees", cancellationToken);

        return new DirectoryDelta(
            Students:         students.Select(MapStudent).ToArray(),
            Employees:        employees.Select(MapEmployee).ToArray(),
            HighWatermarkUtc: DateTime.UtcNow,
            IsFullSnapshot:   true);
    }

    private async Task<List<T>> PageThroughAsync<T>(string resource, CancellationToken ct)
    {
        var all = new List<T>(4096);
        for (var page = 1; page <= MaxPages; page++)
        {
            var path = $"{resource}?page={page}&pageSize={_pageSize}";
            using var req = new HttpRequestMessage(HttpMethod.Get, path);
            Sign(req, HttpMethod.Get.Method, "/" + path, bodyBytes: Array.Empty<byte>());

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await SafelyReadBodyAsync(res, ct);
                throw new InvalidOperationException(
                    $"Directory API call to {resource} returned {(int)res.StatusCode} {res.StatusCode}: {Truncate(body, 500)}");
            }

            var payload = await res.Content.ReadFromJsonAsync<ApiPage<T>>(cancellationToken: ct)
                          ?? new ApiPage<T>(Array.Empty<T>(), 0, 0);
            if (payload.Items.Count == 0) break;

            all.AddRange(payload.Items);
            if (payload.TotalPages > 0 && page >= payload.TotalPages) break;
        }
        return all;
    }

    // ─── HMAC signing — mirrors ApiKeyAuthorizer on the server ────────────

    private void Sign(HttpRequestMessage request, string verb, string path, byte[] bodyBytes)
    {
        var timestamp   = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var bodyHashHex = Convert.ToHexString(SHA256.HashData(bodyBytes)).ToLowerInvariant();
        var canonical   = $"{_appKey}\n{timestamp}\n{verb}\n{path}\n{bodyHashHex}";
        var signature   = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_secret), Encoding.UTF8.GetBytes(canonical)));

        request.Headers.TryAddWithoutValidation("X-App-Key",       _appKey);
        request.Headers.TryAddWithoutValidation("X-App-Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("X-App-Signature", signature);
    }

    private static async Task<string> SafelyReadBodyAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try { return await res.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ─── DTOs ─────────────────────────────────────────────────────────────

    private sealed record ApiPage<T>(
        [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
        [property: JsonPropertyName("totalPages")] int TotalPages,
        [property: JsonPropertyName("totalCount")] int TotalCount);

    private sealed record ApiStudent(
        [property: JsonPropertyName("externalId")]     string ExternalId,
        [property: JsonPropertyName("name")]           string Name,
        [property: JsonPropertyName("cardIdentifier")] string? CardIdentifier,
        [property: JsonPropertyName("gender")]         string? Gender,
        [property: JsonPropertyName("contactNo")]      string? ContactNo,
        [property: JsonPropertyName("photoPath")]      string? PhotoPath,
        [property: JsonPropertyName("program")]        string? Program,
        [property: JsonPropertyName("class")]          string? Class,
        [property: JsonPropertyName("section")]        string? Section,
        [property: JsonPropertyName("session")]        string? Session,
        [property: JsonPropertyName("version")]        string? Version);

    private sealed record ApiEmployee(
        [property: JsonPropertyName("externalId")]     string ExternalId,
        [property: JsonPropertyName("name")]           string Name,
        [property: JsonPropertyName("cardIdentifier")] string? CardIdentifier,
        [property: JsonPropertyName("gender")]         string? Gender,
        [property: JsonPropertyName("contactNo")]      string? ContactNo,
        [property: JsonPropertyName("photoPath")]      string? PhotoPath,
        [property: JsonPropertyName("designation")]    string? Designation,
        [property: JsonPropertyName("employeeType")]   string? EmployeeType);

    private static DirectoryStudent MapStudent(ApiStudent s) => new(
        ExternalId: s.ExternalId, Name: s.Name, CardIdentifier: s.CardIdentifier, Gender: s.Gender,
        ContactNo: s.ContactNo, PhotoPath: s.PhotoPath, Program: s.Program, Class: s.Class,
        Section: s.Section, Session: s.Session, Version: s.Version);

    private static DirectoryEmployee MapEmployee(ApiEmployee e) => new(
        ExternalId: e.ExternalId, Name: e.Name, CardIdentifier: e.CardIdentifier, Gender: e.Gender,
        ContactNo: e.ContactNo, PhotoPath: e.PhotoPath, Designation: e.Designation, EmployeeType: e.EmployeeType);
}
