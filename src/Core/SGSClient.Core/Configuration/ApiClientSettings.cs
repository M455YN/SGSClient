namespace SGSClient.Core.Configuration;

public sealed class ApiClientSettings
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = "https://api.m455yn.dev/";
}
