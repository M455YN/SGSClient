namespace SGSClient.Core.Configuration;

public sealed class AzureAdSettings
{
    public const string SectionName = "AzureAd";

    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string SigninPolicy { get; set; } = string.Empty;
    public string PasswordResetPolicy { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = ["openid", "offline_access"];

    /// <summary>
    /// API audience / App ID URI used when validating access tokens. Defaults to ClientId when empty.
    /// </summary>
    public string ApiAudience { get; set; } = string.Empty;

    public string Domain => $"{TenantName}.onmicrosoft.com";

    public string Authority =>
        $"https://{TenantName}.b2clogin.com/tfp/{Domain}/{SigninPolicy}";

    public string PasswordResetAuthority =>
        $"https://{TenantName}.b2clogin.com/tfp/{Domain}/{PasswordResetPolicy}";

    public string MetadataAddress =>
        $"https://{TenantName}.b2clogin.com/{Domain}/{SigninPolicy}/v2.0/.well-known/openid-configuration";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(TenantName)
        && !string.IsNullOrWhiteSpace(SigninPolicy);
}
