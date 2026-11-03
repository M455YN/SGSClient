using System.Runtime.InteropServices;
using System.Text;

namespace SGSClient.Infrastructure.Client.Services;

/// <summary>
/// Creates and removes .lnk shortcuts on the user's desktop via COM IShellLink.
/// Windows-only; silently no-ops on other platforms.
/// </summary>
internal static class DesktopShortcutHelper
{
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
