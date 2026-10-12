using Microsoft.UI.Xaml.Markup;
using SGSClient.Core.Localization;

namespace SGSClient.UI.WinUI.Localization;

/// <summary>XAML: <c>Text="{loc:Tr Key='Polski tekst'}"</c> resolves through <see cref="Loc"/>.</summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => Loc.T(Key);
}
