namespace SGSClient.Core.Enums;

/// <summary>Where the game is in its lifecycle; drives the store badge.</summary>
public enum GameReleaseStatus
{
    /// <summary>Announced, nothing playable yet.</summary>
    Announced = 0,

    /// <summary>Playable but still in development.</summary>
    EarlyAccess = 1,

    /// <summary>Finished game.</summary>
    Released = 2,

    /// <summary>Free sample of a bigger game.</summary>
    Demo = 3
}
