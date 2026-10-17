namespace SGSClient.Core.Enums;

/// <summary>Catalog content type. DLC entries are child games linked via ParentGameId; every other kind is standalone.</summary>
public enum GameContentKind
{
    Game = 0,
    Dlc = 1,
    App = 2,
    Book = 3,
    Music = 4,
    Art = 5
}
