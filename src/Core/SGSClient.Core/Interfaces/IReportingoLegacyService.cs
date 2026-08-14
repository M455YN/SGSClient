using SGSClient.Core.DTOs.Reportingo;

namespace SGSClient.Core.Interfaces;

public interface IReportingoLegacyService
{
    bool IsConfigured { get; }

    Task ReportBugAsync(ReportingoBugReportRequest request, CancellationToken cancellationToken = default);
}
