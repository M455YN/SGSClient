namespace SGSClient.Core.Enums;

public enum InstallState
{
    NotInstalled = 0,
    Queued = 1,
    Downloading = 2,
    Installing = 3,
    Installed = 4,
    Failed = 5,
    Updating = 6
}
