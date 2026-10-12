using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Body of the Steam-style achievements dialog; the dialog itself is created by the host page.</summary>
public sealed partial class AchievementsModal : UserControl
{
    public AchievementsModal()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not AchievementsModalViewModel vm)
            {
                return;
            }

            void Sync()
            {
                EmptyText.Text = vm.EmptyText;
                EmptyText.Visibility = vm.Visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            vm.Visible.CollectionChanged += (_, _) => Sync();
            Sync();
        };
    }

    private void Mine_Click(object sender, RoutedEventArgs e) =>
        (DataContext as AchievementsModalViewModel)?.SelectTabCommand.Execute("mine");

    private void Global_Click(object sender, RoutedEventArgs e) =>
        (DataContext as AchievementsModalViewModel)?.SelectTabCommand.Execute("global");
}
