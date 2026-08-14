namespace SGSClient.Core.DTOs.Reportingo;

public sealed class ReportingoBugReportRequest
{
    public string? ReporterName { get; set; }
    public string? ReporterIp { get; set; }
    public int ReporterGameId { get; set; }
    public string? Position { get; set; }
    public string? ReportType { get; set; }
    public string? Message { get; set; }
    public string? Source { get; set; }
}
