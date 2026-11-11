using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Body of the "Edit profile" dialog; edits the AccountViewModel's Edit* fields.</summary>
public partial class ProfileEditForm : UserControl
{
    public ProfileEditForm()
    {
        InitializeComponent();
    }

    /// <summary>Raised after the avatar changed so the profile view can refresh its picture.</summary>
    public event Action? AvatarChanged;

    public void SetAvatar(IImage? image) => EditAvatarImage.Source = image;

    public void ShowError(string? message)
    {
        ErrorText.Text = message;
        ErrorPanel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private async void ChangeAvatar_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AccountViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Wybierz zdjęcie profilowe"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("Obrazy"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            await vm.UploadAvatarFromPathAsync(path);
        }
        else
        {
            await using var stream = await file.OpenReadAsync();
            await vm.UploadAvatarFromStreamAsync(stream, file.Name ?? "avatar.png");
        }

        AvatarChanged?.Invoke();
    }

    private async void ClearAvatar_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.ClearAvatarCommand.ExecuteAsync(null);
            AvatarChanged?.Invoke();
        }
    }
}
