// Lobby Demo: the smallest game that uses the SGS client lobby.
// Dice race over TCP: everyone presses Enter to roll, the first to reach 30 wins.
// The host's game owns the scores; guests only send "ROLL" and print what the host broadcasts.
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

const int Goal = 30;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var api = Environment.GetEnvironmentVariable("SGS_API_URL");
var token = Environment.GetEnvironmentVariable("SGS_ACHIEVEMENTS_TOKEN");
var code = Environment.GetEnvironmentVariable("SGS_LOBBY_CODE");
var isHost = Environment.GetEnvironmentVariable("SGS_LOBBY_HOST") == "1";

// Without the SGS client: `LobbyDemo --host` or `LobbyDemo --join 192.168.0.5:7777`, to test on one machine.
if (args is ["--host"]) { isHost = true; code = null; api = null; }
string? joinAddress = args is ["--join", var a] ? a : null;

HttpClient? http = null;
if (api is not null && token is not null)
{
    http = new HttpClient { BaseAddress = new Uri(api) };
    http.DefaultRequestHeaders.Add("X-SGS-Token", token);
}

var name = Environment.UserName;
if (http is not null)
{
    try { name = (await http.GetFromJsonAsync<JsonElement>("api/sdk/me")).GetProperty("displayName").GetString() ?? name; }
    catch (HttpRequestException) { }
}

if (isHost) await HostAsync();
else if (joinAddress is not null) await GuestAsync(joinAddress);
else if (code is not null && http is not null) await GuestAsync(await WaitForHostAddressAsync());
else Console.WriteLine("Uruchom grę z lobby w kliencie SGS albo: LobbyDemo --host | LobbyDemo --join <adres:port>");

async Task<string> WaitForHostAddressAsync()
{
    Console.WriteLine($"Lobby {code}: czekam, aż host poda adres...");
    while (true)
    {
        var lobby = await http!.GetFromJsonAsync<JsonElement>($"api/sdk/lobbies/{code}");
        var address = lobby.GetProperty("address").GetString();
        if (!string.IsNullOrEmpty(address)) return address;
        await Task.Delay(1000);
    }
}

async Task HostAsync()
{
    var listener = new TcpListener(IPAddress.Any, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    // shortcut: LAN address only, works on one network or VPN; a game for the internet puts a relay join code here.
    var ip = Dns.GetHostAddresses(Dns.GetHostName())
        .FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(i)) ?? IPAddress.Loopback;
    var address = $"{ip}:{port}";
    Console.WriteLine($"Jesteś hostem. Adres: {address}");

    if (http is not null && code is not null)
    {
        (await http.PatchAsJsonAsync($"api/sdk/lobbies/{code}", new { address })).EnsureSuccessStatusCode();
        // Every PATCH keeps the lobby alive; it expires after 10 minutes without one.
        _ = Task.Run(async () =>
        {
            while (true) { await Task.Delay(TimeSpan.FromMinutes(2)); await http.PatchAsJsonAsync($"api/sdk/lobbies/{code}", new { }); }
        });
    }

    var scores = new Dictionary<string, int> { [name] = 0 };
    var clients = new List<StreamWriter>();
    var gate = new object();
    var over = false;

    void Broadcast(string line)
    {
        Console.WriteLine(line);
        lock (gate) foreach (var c in clients) { try { c.WriteLine(line); } catch (IOException) { } }
    }

    void Roll(string player)
    {
        lock (gate)
        {
            if (over) return;
            var roll = Random.Shared.Next(1, 7);
            scores[player] = scores.GetValueOrDefault(player) + roll;
            Broadcast($"{player} wyrzuca {roll} (razem {scores[player]})");
            if (scores[player] >= Goal) { over = true; Broadcast($"*** {player} wygrywa! ***"); }
        }
    }

    _ = Task.Run(async () =>
    {
        while (true)
        {
            var tcp = await listener.AcceptTcpClientAsync();
            _ = Task.Run(async () =>
            {
                using var reader = new StreamReader(tcp.GetStream());
                var writer = new StreamWriter(tcp.GetStream()) { AutoFlush = true };
                var player = await reader.ReadLineAsync() ?? "?";
                lock (gate)
                {
                    // Two players can share a display name; each still needs a score of their own.
                    for (var n = 2; scores.ContainsKey(player); n++) player = $"{player.Split(" #")[0]} #{n}";
                    clients.Add(writer);
                    scores[player] = 0;
                }
                Broadcast($"{player} dołącza do gry.");
                while (await reader.ReadLineAsync() is { } line)
                    if (line == "ROLL") Roll(player);
                lock (gate) clients.Remove(writer);
                Broadcast($"{player} wychodzi.");
            });
        }
    });

    Console.WriteLine($"Enter = rzut kością. Pierwszy do {Goal} wygrywa. q = wyjście.");
    while (Console.ReadLine() is { } input && input != "q") Roll(name);
    // No DELETE here: when this process exits the SGS client brings everyone back to the lobby for another round.
}

async Task GuestAsync(string address)
{
    var parts = address.Split(':');
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(parts[0], int.Parse(parts[1]));
    var writer = new StreamWriter(tcp.GetStream()) { AutoFlush = true };
    await writer.WriteLineAsync(name);
    Console.WriteLine($"Połączono z {address}. Enter = rzut kością, q = wyjście.");

    _ = Task.Run(async () =>
    {
        using var reader = new StreamReader(tcp.GetStream());
        while (await reader.ReadLineAsync() is { } line) Console.WriteLine(line);
        Console.WriteLine("Host zakończył grę.");
    });

    while (Console.ReadLine() is { } input && input != "q") await writer.WriteLineAsync("ROLL");
}
