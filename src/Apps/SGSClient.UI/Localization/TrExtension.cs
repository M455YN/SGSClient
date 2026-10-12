using Avalonia.Markup.Xaml;
using SGSClient.Core.Localization;

namespace SGSClient.UI.Localization;

/// <summary>XAML: <c>Text="{loc:Tr Key='Polski tekst'}"</c> resolves through <see cref="Loc"/>.</summary>
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
