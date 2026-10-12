namespace SGSClient.UI;

/// <summary>Applies the chosen theme ("system", "light", "dark") to the running UI. Each client provides one.</summary>
public interface IThemeApplier
{
    void Apply(string theme);
}
