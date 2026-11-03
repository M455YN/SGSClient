namespace SGSClient.Core;

public static class SgsClientConstants
{
    /// <summary>
    /// Named mutex held while SGSClient is running.
    /// Game launcher stubs check for its existence before starting the payload.
    /// </summary>
    public const string RunningMutexName = "Global\\SGSClient_Running";
}
