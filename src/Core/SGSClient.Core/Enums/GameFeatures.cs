namespace SGSClient.Core.Enums;

/// <summary>Capabilities shown as a feature list on the store page.</summary>
[Flags]
public enum GameFeatures
{
    None = 0,
    SinglePlayer = 1,
    MultiPlayer = 1 << 1,
    CoOp = 1 << 2,
    ControllerSupport = 1 << 3,
    CloudSaves = 1 << 4,
    Achievements = 1 << 5,
    Modding = 1 << 6,
    Vr = 1 << 7
}
