using Avalonia.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class MainWindow : Window
{
    private bool _wired;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
        Opened += async (_, _) =>
        {
            Wire();
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }

    private void Wire()
    {
        if (DataContext is not MainWindowViewModel main || _wired)
        {
            return;
        }

        _wired = true;
        main.Sidebar.SetNavigateHandler(tag => main.NavigateCommand.Execute(tag));
        main.Sidebar.SetOpenDetailsHandler(id => main.OpenGameDetails(id, "Library"));
        main.Sidebar.SetOpenPropertiesHandler(OpenProperties);
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedNavTag))
            {
                main.Sidebar.SelectNav(main.SelectedNavTag);
            }

            if (e.PropertyName == nameof(MainWindowViewModel.IsAdminVisible))
            {
                main.Sidebar.SetAdminVisible(main.IsAdminVisible);
                main.Sidebar.RefreshProfile();
            }
        };
        main.Sidebar.SelectNav(main.SelectedNavTag);
        main.Sidebar.SetAdminVisible(main.IsAdminVisible);
        main.Sidebar.RefreshProfile();
    }

    private void OpenProperties(SidebarInstalledGame item)
    {
        var window = new GamePropertiesWindow();
        window.ViewModel.ViewInStoreRequested += (_, _) =>
        {
            window.Close();
            if (DataContext is MainWindowViewModel main)
            {
                main.OpenGameDetails(item.GameId, "Library");
            }
        };
        window.SetGame(item.GameId, item.BuildId, item.Title);
        window.Closed += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel main)
            {
                await main.Sidebar.LoadInstalledAsync();
            }
        };
        window.Show(this);
    }
}
