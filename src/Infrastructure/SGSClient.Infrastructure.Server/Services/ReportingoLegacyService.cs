using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.DTOs.Reportingo;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Server.Services;

/// <summary>
/// Drop-in compatibility with the old SGSClient.Api /api/reportingo/* endpoints.
/// Uses the existing REPORTINGO database.
/// </summary>
public sealed class ReportingoLegacyService(IConfiguration configuration) : IReportingoLegacyService
{
    private readonly string? _connectionString =
        configuration.GetConnectionString("ReportingoDatabase")
        ?? configuration.GetConnectionString("ReportingoConnectionString");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task ReportBugAsync(
        ReportingoBugReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Connection string 'ReportingoDatabase' is missing. Set ConnectionStrings:ReportingoDatabase.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO server_bug_reports (
              reporter_name, reporter_ip, reporter_game_id, position,
              report_type, message, source, status, created_at)
            VALUES (
              @reporterName, @reporterIp, @reporterGameId, @position,
              @reportType, @message, @source, 'OPEN', GETDATE());
            """;
        cmd.Parameters.AddWithValue("@reporterName", (object?)request.ReporterName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@reporterIp", (object?)request.ReporterIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@reporterGameId", request.ReporterGameId);
        cmd.Parameters.AddWithValue("@position", (object?)request.Position ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@reportType", (object?)request.ReportType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@message", (object?)request.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@source", (object?)request.Source ?? DBNull.Value);
        await connection.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
