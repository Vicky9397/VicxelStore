using Dpm.BuildingBlocks.Domain;
using Dpm.Downloads.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Dpm.Downloads.Infrastructure;

/// <summary>
/// Appends to files.DownloadLogs (spec 05 section 5.3). Writing directly keeps
/// the log outside any aggregate's transaction: an audit row is never the reason
/// a download fails.
/// </summary>
public sealed class DownloadLog(IConfiguration configuration, IClock clock) : IDownloadLog
{
    public async Task RecordAsync(
        long licenseId,
        long fileId,
        long userId,
        string? ipHash,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO files.DownloadLogs (LicenseId, FileId, UserId, IpHash, StartedAtUtc, Completed)
            VALUES (@LicenseId, @FileId, @UserId, @IpHash, @StartedAtUtc, 0);
            """;

        await using var connection = new SqlConnection(configuration.GetConnectionString("Database"));
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@LicenseId", licenseId));
        command.Parameters.Add(new SqlParameter("@FileId", fileId));
        command.Parameters.Add(new SqlParameter("@UserId", userId));
        command.Parameters.Add(new SqlParameter("@IpHash", (object?)ipHash ?? DBNull.Value));
        command.Parameters.Add(new SqlParameter("@StartedAtUtc", clock.UtcNow));

        await connection.OpenAsync(ct);
        await command.ExecuteNonQueryAsync(ct);
    }
}
