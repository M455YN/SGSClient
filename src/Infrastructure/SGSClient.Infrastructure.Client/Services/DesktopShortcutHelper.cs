using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace SGSClient.Infrastructure.Client.Services;

/// <summary>
/// Creates and removes .lnk shortcuts on the user's desktop via COM IShellLink.
/// Windows-only; silently no-ops on other platforms.
/// </summary>
internal static class DesktopShortcutHelper
{
    /// <summary>
    /// Downloads the game image from <paramref name="imageUrl"/>, converts it to .ico,
    /// saves it next to the exe, and creates a shortcut using that icon.
    /// </summary>
    public static async Task CreateForGameAsync(
        string title,
        string executablePath,
        string workingDirectory,
        HttpClient http,
        string? imageUrl,
        string? apiBaseUrl)
    {
        if (!OperatingSystem.IsWindows()) return;

        string? icoPath = null;
        try
        {
            icoPath = await TryDownloadIconAsync(http, imageUrl, apiBaseUrl, workingDirectory);
        }
        catch
        {
            // fall through — create shortcut without custom icon
        }

        CreateForGame(title, executablePath, workingDirectory, icoPath);
    }

    public static void CreateForGame(string title, string executablePath, string workingDirectory, string? iconPath = null)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop)) return;

            var lnkPath = Path.Combine(desktop, SanitizeFileName(title) + ".lnk");
            CreateShellLink(lnkPath, executablePath, workingDirectory, iconPath ?? executablePath);
        }
        catch
        {
            // best-effort
        }
    }

    public static void RemoveForGame(string title)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop)) return;

            var lnkPath = Path.Combine(desktop, SanitizeFileName(title) + ".lnk");
            if (File.Exists(lnkPath))
            {
                File.Delete(lnkPath);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static async Task<string?> TryDownloadIconAsync(
        HttpClient http, string? imageUrl, string? apiBaseUrl, string installDir)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;

        var url = imageUrl;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            url = apiBaseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode) return null;

        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        if (imageBytes.Length < 8) return null;

        var pngBytes = imageBytes;
        if (!IsPng(pngBytes))
        {
            // JPG or other format — skip custom icon, .lnk will use the exe icon
            return null;
        }

        var icoPath = Path.Combine(installDir, "game.ico");
        var icoBytes = WrapPngAsIco(pngBytes);
        await File.WriteAllBytesAsync(icoPath, icoBytes);
        return icoPath;
    }

    private static bool IsPng(byte[] data) =>
        data.Length >= 8
        && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;

    /// <summary>
    /// Wraps a PNG image as a single-frame .ico file.
    /// Windows Vista+ supports PNG-compressed icons natively.
    /// </summary>
    private static byte[] WrapPngAsIco(byte[] pngData)
    {
        // ICO header: 6 bytes + 1 entry (16 bytes) + PNG data
        var ico = new byte[6 + 16 + pngData.Length];
        // Reserved
        ico[0] = 0; ico[1] = 0;
        // Type: 1 = icon
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(2), 1);
        // Count: 1 image
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(4), 1);

        // ICONDIRENTRY (16 bytes)
        // Width, Height: 0 = 256px (PNG icons are typically 256x256 or get scaled)
        ico[6] = 0;
        ico[7] = 0;
        // Color palette count
        ico[8] = 0;
        // Reserved
        ico[9] = 0;
        // Color planes
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(10), 1);
        // Bits per pixel (32 for RGBA PNG)
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(12), 32);
        // Size of PNG data
        BinaryPrimitives.WriteUInt32LittleEndian(ico.AsSpan(14), (uint)pngData.Length);
        // Offset of PNG data (6 header + 16 entry = 22)
        BinaryPrimitives.WriteUInt32LittleEndian(ico.AsSpan(18), 22);

        pngData.CopyTo(ico, 22);
        return ico;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString().Trim();
    }

    private static void CreateShellLink(string lnkPath, string targetPath, string workingDirectory, string iconPath)
    {
        var link = (IShellLink)new ShellLink();
        link.SetPath(targetPath);
        link.SetWorkingDirectory(workingDirectory);
        link.SetIconLocation(iconPath, 0);

        var file = (IPersistFile)link;
        file.Save(lnkPath, false);

        Marshal.ReleaseComObject(file);
        Marshal.ReleaseComObject(link);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetClassID(out Guid pClassID);
    }
}
