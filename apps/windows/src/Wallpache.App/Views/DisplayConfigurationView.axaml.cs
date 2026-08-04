using Avalonia.Controls;
using Avalonia.Interactivity;
using Wallpache.App.ViewModels;

namespace Wallpache.App.Views;

/// <summary>Per-display wallpaper assignment and playback settings.</summary>
public partial class DisplayConfigurationView : UserControl
{
    public DisplayConfigurationView()
    {
        InitializeComponent();
    }

    private void OnRemoveWallpaper(object? sender, RoutedEventArgs args)
    {
        if ((sender as Control)?.DataContext is DisplayItemViewModel display)
        {
            display.RemoveWallpaper();
        }
    }
}
