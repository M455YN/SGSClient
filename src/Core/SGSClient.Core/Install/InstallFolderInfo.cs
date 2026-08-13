namespace SGSClient.Core.Install;

public static class InstallFolderInfo
{
    public static InstallFolderStats Measure(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return InstallFolderStats.Empty;
        }

        long bytes = 0;
        var files = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                    files++;
                }
                catch
                {
                    // Skip locked/inaccessible files.
                }
            }
        }
        catch
        {
            return InstallFolderStats.Empty;
        }

        return new InstallFolderStats(bytes, files, path);
    }

    public static string FormatSize(long bytes)
    {
        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;

        return bytes switch
        {
            >= (long)gb => $"{bytes / gb:0.##} GB",
            >= (long)mb => $"{bytes / mb:0.##} MB",
            >= (long)kb => $"{bytes / kb:0.##} KB",
            _ => $"{bytes} B"
        };
    }
}

public readonly record struct InstallFolderStats(long Bytes, int FileCount, string? Path)
{
    public static InstallFolderStats Empty { get; } = new(0, 0, null);
    public bool Exists => !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);
}
