// =============================================================================
// ResetUserPassword  (tools/ResetUserPassword)
// -----------------------------------------------------------------------------
// Escape hatch for when the bootstrap sysadmin account gets stuck after a
// change-password attempt fails to round-trip. Resets ANY AppUsers row's:
//   * PasswordHash       → BCrypt hash of the supplied new password
//   * MustChangePassword → false (so the user can sign straight in)
//   * FailedLoginCount   → 0
//   * LockedOutUntilUtc  → NULL
//
// USAGE
//   dotnet run --project tools/ResetUserPassword -- \
//       --conn "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True" \
//       --user sysadmin \
//       --password "MyNewPassword!"
//
// SECURITY
//   This bypasses normal change-password validation and audit. Only run it
//   from a developer machine with database access — and rotate the password
//   you set here immediately after signing in.
// =============================================================================

using System.Globalization;
using Microsoft.Data.SqlClient;

string? connStr = null;
string? userName = null;
string? newPassword = null;
for (var i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--conn":     connStr = args[++i]; break;
        case "--user":     userName = args[++i]; break;
        case "--password": newPassword = args[++i]; break;
    }
}

if (string.IsNullOrEmpty(connStr) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(newPassword))
{
    Console.Error.WriteLine(@"Usage:
  dotnet run --project tools/ResetUserPassword -- \
      --conn ""<connection string>"" \
      --user <userName> \
      --password ""<new plaintext>""

Example:
  dotnet run --project tools/ResetUserPassword -- \
      --conn ""Server=(localdb)\MSSQLLocalDB;Database=Canteen;Integrated Security=True;TrustServerCertificate=True"" \
      --user sysadmin \
      --password ""ChangeMe!2026""
");
    return 1;
}

if (newPassword.Length < 8)
{
    Console.Error.WriteLine($"Password must be at least 8 characters (got {newPassword.Length}).");
    return 2;
}

var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
Console.WriteLine($"Generated BCrypt hash (length {hash.Length})");

await using var conn = new SqlConnection(connStr);
await conn.OpenAsync();

await using (var probe = conn.CreateCommand())
{
    probe.CommandText = "SELECT UserId, IsActive, MustChangePassword, FailedLoginCount, LockedOutUntilUtc FROM AppUsers WHERE UserName = @u";
    probe.Parameters.AddWithValue("@u", userName);
    await using var reader = await probe.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        Console.Error.WriteLine($"No user named '{userName}' found.");
        return 3;
    }
    Console.WriteLine($"Found user UserId={reader.GetInt32(0)} " +
                      $"IsActive={reader.GetBoolean(1)} MustChangePassword={reader.GetBoolean(2)} " +
                      $"FailedLoginCount={reader.GetInt32(3)} " +
                      $"LockedOutUntilUtc={(reader.IsDBNull(4) ? "NULL" : reader.GetDateTime(4).ToString("o", CultureInfo.InvariantCulture))}");
}

await using (var update = conn.CreateCommand())
{
    update.CommandText = @"
        UPDATE AppUsers
        SET PasswordHash        = @h,
            MustChangePassword  = 0,
            FailedLoginCount    = 0,
            LockedOutUntilUtc   = NULL,
            IsActive            = 1
        WHERE UserName = @u";
    update.Parameters.AddWithValue("@h", hash);
    update.Parameters.AddWithValue("@u", userName);
    var rows = await update.ExecuteNonQueryAsync();
    Console.WriteLine($"Updated {rows} row(s).");
}

Console.WriteLine($"Done. Sign in as '{userName}' with the new password.");
return 0;
