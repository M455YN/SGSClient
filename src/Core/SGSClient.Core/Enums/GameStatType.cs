using System.Text.Json.Serialization;

namespace SGSClient.Core.Enums;

/// <summary>Storage type of a game stat (Steam INT / FLOAT; AVGRATE is not supported). JSON: "Int" / "Float".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameStatType>))]
public enum GameStatType
{
    Int = 0,
    Float = 1
}
