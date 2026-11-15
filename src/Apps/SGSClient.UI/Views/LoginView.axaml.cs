using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private async void BrowseGamesFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginGateViewModel gate || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.T("Wybierz folder na gry"),
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            gate.GamesFolder = path;
        }
    }
}
