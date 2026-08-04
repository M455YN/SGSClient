using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AccountView : UserControl
{
    public AccountView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindWindowHandle();
        AttachedToVisualTree += (_, _) => BindWindowHandle();
    }

    private void BindWindowHandle()
    {
        if (DataContext is AccountViewModel vm)
        {
            vm.SetParentWindowHandleProvider(() =>
                TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? 0);
        }
    }

    private async void SignInButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.LoginCommand.ExecuteAsync(null);
        }
    }

    private async void SignOutButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.LogoutCommand.ExecuteAsync(null);
        }
    }

    private void EditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            vm.BeginEditCommand.Execute(null);
        }
    }

    private void CancelEditButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            vm.CancelEditCommand.Execute(null);
        }
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AccountViewModel vm)
        {
            await vm.SaveProfileCommand.ExecuteAsync(null);
        }
    }
}
