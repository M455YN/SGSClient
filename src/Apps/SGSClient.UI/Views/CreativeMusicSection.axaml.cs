using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Avalonia has no built-in audio playback, so a started track opens in the system's default player.</summary>
public partial class CreativeMusicSection : UserControl
{
    private CreativeMusicViewModel? _viewModel;

    public CreativeMusicSection()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.TrackStarted -= OnTrackStarted;
            }

            _viewModel = DataContext as CreativeMusicViewModel;
            if (_viewModel is not null)
            {
                _viewModel.TrackStarted += OnTrackStarted;
            }
        };
    }

    private void OnTrackStarted(object? sender, CreativeItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FileUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            if (_viewModel is not null)
            {
                _viewModel.StatusText = Loc.F("Nie udało się otworzyć: {0}", ex.Message);
            }
        }
    }

    private void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
        {
            return;
        }

        _viewModel.SearchText = SearchBox.Text?.Trim() ?? string.Empty;
        _ = _viewModel.LoadAsync();
        e.Handled = true;
    }

    private void Play_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CreativeItem item })
        {
            _viewModel?.PlayCommand.Execute(item);
        }
    }

    private void Next_OnClick(object? sender, RoutedEventArgs e) => _viewModel?.NextCommand.Execute(null);
}
