// =============================================================================
// UsersAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IUserAdminService — no IAppDbContext.
// Controller still owns the CSV parsing (presentation-layer concern) before
// handing rows to the service.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Identity;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/users")]
public sealed class UsersAdminController : Controller
{
    private readonly IUserAdminService _users;
    public UsersAdminController(IUserAdminService users) => _users = users;

    public sealed record UserRow(int UserId, int? ClientId, string UserName, string DisplayName,
        string? Email, string UserKind, bool IsActive, DateTime? LastLoginAtUtc, string Roles);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var crossTenant = User.IsInRole("SystemAdmin");
        var list = await _users.ListAsync(crossTenant, ct);
        return View(list.Select(u => new UserRow(
            u.UserId, u.ClientId, u.UserName, u.DisplayName, u.Email, u.UserKind,
            u.IsActive, u.LastLoginAtUtc, string.Join(", ", u.Roles))).ToList());
    }

    public sealed class InviteForm
    {
        [Required, StringLength(64)] public string UserName { get; set; } = string.Empty;
        [Required, StringLength(200)] public string DisplayName { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
        [Required, StringLength(32)] public string UserKind { get; set; } = "Operator";
        [Required] public string RoleCode { get; set; } = "Operator";
    }

    [HttpGet("invite")]
    public async Task<IActionResult> Invite(CancellationToken ct)
    {
        ViewBag.Roles = await _users.ListAssignableRolesAsync(ct);
        return View(new InviteForm());
    }

    [HttpPost("invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(InviteForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Roles = await _users.ListAssignableRolesAsync(ct);
            return View(form);
        }

        var boundClientId = User.IsInRole("SystemAdmin") && (User.FindFirst("client_id")?.Value == "0")
            ? null
            : ResolveBoundClientId();

        var result = await _users.InviteAsync(new InviteUserInput(
            form.UserName, form.DisplayName, form.Email, form.UserKind,
            form.RoleCode, boundClientId), ct);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(nameof(form.UserName), result.Error.Message ?? "Invite failed.");
            ViewBag.Roles = await _users.ListAssignableRolesAsync(ct);
            return View(form);
        }
        var outcome = result.Value!;
        TempData["Flash.Success"] = $"User {outcome.User.UserName} created. Temporary password: {outcome.TempPassword}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var result = await _users.ToggleActiveAsync(id, User.IsInRole("SystemAdmin"), ResolveBoundClientId(), ct);
        if (!result.IsSuccess)
            return result.Error.Code == "unauthorized" ? Forbid() : NotFound();
        var u = result.Value!;
        TempData["Flash.Success"] = $"{u.UserName} {(u.IsActive ? "unlocked" : "locked")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken ct)
    {
        var result = await _users.ResetPasswordAsync(id, User.IsInRole("SystemAdmin"), ResolveBoundClientId(), ct);
        if (!result.IsSuccess)
            return result.Error.Code == "unauthorized" ? Forbid() : NotFound();
        var o = result.Value!;
        TempData["Flash.Warning"] = $"Password for {o.User.UserName} reset. New temp password: {o.TempPassword}";
        return RedirectToAction(nameof(Index));
    }

    // ─── Bulk CSV import — controller parses CSV, service does the inserts ──

    public sealed class BulkImportResult
    {
        public int Created { get; init; }
        public int Skipped { get; init; }
        public List<string> CreatedSamples { get; init; } = new();
    }

    [HttpGet("bulk-import")]
    public IActionResult BulkImport() => View(new BulkImportResult());

    [HttpPost("bulk-import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> BulkImport(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Flash.Error"] = "Please choose a CSV file.";
            return RedirectToAction(nameof(BulkImport));
        }
        var rows = await ParseCsvAsync(file, ct);
        if (rows is null)
        {
            TempData["Flash.Error"] = "CSV is missing required columns 'UserName' and 'DisplayName'.";
            return RedirectToAction(nameof(BulkImport));
        }

        var boundClientId = ResolveBoundClientId();
        var result = await _users.BulkImportAsync(rows, boundClientId, ct);
        if (!result.IsSuccess)
        {
            TempData["Flash.Error"] = "Import failed: " + (result.Error.Message ?? "");
            return RedirectToAction(nameof(BulkImport));
        }
        var o = result.Value!;
        var sampleText = o.SampleCredentials.Count > 0
            ? "Sample temp passwords: " + string.Join("; ", o.SampleCredentials.Select(s => $"{s.UserName} → {s.TempPassword}"))
            : "";
        TempData["Flash.Success"] = $"Bulk import: {o.Created} created, {o.Skipped} skipped (already existed). {sampleText}";
        return RedirectToAction(nameof(Index));
    }

    private static async Task<List<BulkImportRow>?> ParseCsvAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new System.IO.StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return new List<BulkImportRow>();
        var headers = SplitCsv(header).Select(h => h.Trim()).ToList();
        int IdxOf(string name) => headers.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
        var iUser   = IdxOf("UserName");    var iName   = IdxOf("DisplayName");
        var iEmail  = IdxOf("Email");       var iKind   = IdxOf("UserKind");
        var iRole   = IdxOf("RoleCode");    var iLinked = IdxOf("ExternalId");
        if (iUser < 0 || iName < 0) return null;

        var output = new List<BulkImportRow>();
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = SplitCsv(line);
            string? Cell(int idx) => idx >= 0 && idx < cells.Count ? cells[idx]?.Trim() : null;
            output.Add(new BulkImportRow(
                Cell(iUser) ?? "", Cell(iName) ?? "",
                Cell(iEmail), Cell(iKind), Cell(iRole), Cell(iLinked)));
        }
        return output;
    }

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    private int? ResolveBoundClientId()
    {
        var raw = User.FindFirst("client_id")?.Value;
        if (int.TryParse(raw, out var n) && n > 0) return n;

        if (Request.Cookies.TryGetValue("ccs.impersonate", out var imp) && !string.IsNullOrEmpty(imp))
        {
            var parts = imp.Split('|', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var impId)) return impId;
        }
        return null;
    }
}
