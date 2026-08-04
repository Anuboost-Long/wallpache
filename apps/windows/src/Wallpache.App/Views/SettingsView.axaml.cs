using Avalonia.Controls;
using Avalonia.Interactivity;
using Wallpache.App.ViewModels;

namespace Wallpache.App.Views;

/// <summary>Energy behaviour, startup, storage, and privacy information.</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    private void OnRevealStorage(object? sender, RoutedEventArgs args) =>
        Model?.Coordinator.RevealStorageInExplorer();

    private async void OnDeleteAll(object? sender, RoutedEventArgs args)
    {
        if (Model is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var confirmed = await DialogWindow.ConfirmAsync(
            owner,
            "Delete all imported videos?",
            "This removes every video from Wallpache's library and stops all live wallpapers. The original files on your PC are not touched.",
            "Delete All");

        if (confirmed)
        {
            Model.Coordinator.DeleteAllImportedFiles();
        }
    }
}
