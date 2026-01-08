using Dapper;
using Microsoft.Data.SqlClient;

namespace LoadTrackerWeb.Data;

public sealed class UserAuthRepository
{
    private readonly IConfiguration _cfg;
    public UserAuthRepository(IConfiguration cfg) => _cfg = cfg;

    private SqlConnection OpenAuth() =>
        new SqlConnection(_cfg.GetConnectionString("AuthDb"));

    public async Task<(bool Ok, string? CustomerCode, byte CanDo)> ValidateCustomerAsync(
        string userName, string password, CancellationToken ct)
    {
        // NOTE: plain text password comparison (temporary).
        // Later you should store hash/salt and compare hash.
        const string sql = @"
SELECT TOP 1 CustomerCode, CanDo
FROM dbo.UserAuth
WHERE UserName = @UserName
  AND [Password] = @Password
  AND IsActive = 1;
";

        await using var con = OpenAuth();
        var row = await con.QueryFirstOrDefaultAsync<(string CustomerCode, byte CanDo)>(
            new CommandDefinition(sql, new { UserName = userName, Password = password }, cancellationToken: ct));

        if (string.IsNullOrWhiteSpace(row.CustomerCode))
            return (false, null, 0);

        // Update last login (safe, not schema change)
        const string upd = @"UPDATE dbo.UserAuth SET LastLogOnUtc = SYSUTCDATETIME() WHERE UserName = @UserName;";
        await con.ExecuteAsync(new CommandDefinition(upd, new { UserName = userName }, cancellationToken: ct));

        return (true, row.CustomerCode, row.CanDo);
    }
}
