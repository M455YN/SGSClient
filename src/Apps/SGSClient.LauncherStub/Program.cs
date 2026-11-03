using System.Diagnostics;
using System.Runtime.InteropServices;

const string MutexName = "Global\\SGSClient_Running";

var self = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
if (self is null)
{
    ShowError("Nie można ustalić ścieżki pliku.");
    return 1;
}

var payloadPath = Path.ChangeExtension(self, ".sgs-payload");
if (!File.Exists(payloadPath))
{
    ShowError($"Brak pliku gry: {Path.GetFileName(payloadPath)}");
    return 1;
}

bool launcherRunning;
try
{
    launcherRunning = Mutex.TryOpenExisting(MutexName, out _);
}
catch
{
    launcherRunning = false;
}

if (!launcherRunning)
{
    ShowError("Uruchom tę grę przez SGSClient.");
    return 1;
}

try
{
    var startInfo = new ProcessStartInfo
    {
        FileName = payloadPath,
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(self)
    };

    foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
    {
        startInfo.ArgumentList.Add(arg);
    }

    foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
    {
        if (entry.Key is string key && key.StartsWith("SGS_", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.Environment[key] = entry.Value?.ToString() ?? string.Empty;
        }
    }

    Process.Start(startInfo);
    return 0;
}
catch (Exception ex)
{
    ShowError($"Nie udało się uruchomić gry: {ex.Message}");
    return 1;
}

static void ShowError(string message)
{
    if (OperatingSystem.IsWindows())
    {
        NativeMessageBox(IntPtr.Zero, message, "SGSClient", 0x10);
    }
}

[DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
static extern int NativeMessageBox(IntPtr hWnd, string text, string caption, uint type);
