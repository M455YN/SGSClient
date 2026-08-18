namespace SGSClient.Core.DTOs;

/// <summary>
/// Languages and tags are stored as one comma separated column so they stay translatable
/// inside EF projections; these helpers convert between the column and a list.
/// </summary>
public static class CsvList
{
    public static IReadOnlyList<string> Split(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    public static string? Join(IEnumerable<string>? values, int maxLength = 512)
    {
        if (values is null)
        {
            return null;
        }

        var cleaned = values
            .Select(v => v?.Trim() ?? string.Empty)
            .Where(v => v.Length > 0)
            .Select(v => v.Replace(',', ' '))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count == 0)
        {
            return null;
        }

        var joined = string.Join(",", cleaned);
        return joined.Length <= maxLength ? joined : joined[..maxLength].TrimEnd(',');
    }
}
