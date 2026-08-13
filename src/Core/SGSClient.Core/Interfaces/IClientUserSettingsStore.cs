using SGSClient.Core.Configuration;

namespace SGSClient.Core.Interfaces;

public interface IClientUserSettingsStore
{
    ClientUserSettings Load();
    Task SaveAsync(ClientUserSettings settings, CancellationToken cancellationToken = default);
}
