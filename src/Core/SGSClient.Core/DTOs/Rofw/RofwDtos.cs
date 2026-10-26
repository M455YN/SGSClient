namespace SGSClient.Core.DTOs.Rofw;

public sealed record RofwScoreRequest(string PlayerName, int Score);

public sealed record RofwScoreResponse(int Id, int LP);

/// <param name="OnlyIfLonger">true = nadpisz tylko, gdy nowy zapis jest dłuższy niż ten w bazie (tak robi gra).</param>
public sealed record RofwUpdateAchievementsRequest(int PlayerId, string NewValue, bool OnlyIfLonger = false);

public sealed record RofwUpdateFaceRequest(int PlayerId, int NewFaceId);

public sealed record RofwSaveStatsRequest(
    int PlayerId,
    int Round,
    bool IsWin,
    string Comp,
    int Win,
    int Lose,
    string Fraction);

public sealed record RofwUpdateLpRequest(int PlayerId, int NewLP);

public sealed record RofwLoginRequest(string Username, string Password);

public sealed record RofwLoginResponse(
    int Id,
    string Name,
    string Email,
    string Password,
    int LP,
    int Face,
    string Synergies,
    string Achievements);

public sealed record RofwRegisterRequest(string Username, string Email, string Password);

public sealed record RofwUpdateSynergiesRequest(int PlayerId, string NewValue);

// ---- Autobattler: ranking, karta gracza, przeciwnicy ("duchy") ----

/// <param name="PlayerIds">Id graczy na stronie rankingu (posortowane po LP malejąco).</param>
/// <param name="PlayerPosition">Miejsce zapytanego gracza, -1 gdy nie podano gracza albo go nie ma.</param>
/// <param name="BestName">Gracz z najwyższą rundą w Stats, pusty gdy brak danych.</param>
public sealed record RofwRankingResponse(
    IReadOnlyList<int> PlayerIds,
    int PlayerPosition,
    string BestName,
    int BestWin);

/// <param name="Fraction">Najczęściej grana frakcja jako maska "01001", "00000" gdy brak gier.</param>
public sealed record RofwPlayerCardResponse(
    int Id,
    string Name,
    int Face,
    double AvgWin,
    int LP,
    string Fraction);

/// <param name="Rounds">Dla ilu rund (0..Rounds-1) dobrać przeciwnika.</param>
public sealed record RofwOpponentsRequest(int PlayerId, string PlayerName, int LP, bool Ranked, int Rounds);

/// <param name="Found">false = brak zapisanego przeciwnika na tę rundę (gra wstawia "System").</param>
public sealed record RofwOpponent(
    bool Found,
    int Id,
    string Version,
    string Name,
    int FaceId,
    int LP,
    int PlayerId,
    string Comp,
    int Round);

public sealed record RofwOpponentsResponse(IReadOnlyList<RofwOpponent> Items);

/// <param name="CurrentRound">StatsManager.Round z gry; tabela liczona z niego, w kolumnie Round ląduje CurrentRound - 1.</param>
public sealed record RofwAddGhostRequest(
    int PlayerId,
    string Name,
    int FaceId,
    int LP,
    bool Ranked,
    int CurrentRound,
    string Version,
    string Comp);
