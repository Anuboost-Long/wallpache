using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Wallpache.App.Core;
using Wallpache.App.ViewModels;

namespace Wallpache.App.Views;

/// <summary>
/// The app's only window. Library, per-display setup, and settings live here so
/// the tray menu can stay small.
///
/// Closing it hides it rather than quitting: Wallpache is a tray app, and the
/// wallpaper is expected to keep running.
/// </summary>
public partial class MainWindow : Window
{
    private bool _isShowingAlert;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    /// <summary>Opens the file picker from outside the window, e.g. the tray menu.</summary>
    public Task PresentImportPickerAsync() => Library.PresentImportPickerAsync();

    protected override void OnDataContextChanged(global::System.EventArgs args)
    {
        base.OnDataContextChanged(args);

        if (Model is null)
        {
            return;
        }

        Model.Coordinator.PropertyChanged += OnCoordinatorPropertyChanged;

        // Startup work — pruning missing files, restoring sessions — can raise a
        // message before any window exists, so the pending one is shown here.
        PresentPendingAlert();
    }

    protected override void OnClosing(WindowClosingEventArgs args)
    {
        if (!args.IsProgrammatic)
        {
            args.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(args);
    }

    /// <summary>
    /// The coordinator raises messages from background work — a failed import, a
    /// wallpaper that stopped playing — and the window is where they are shown.
    /// </summary>
    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WallpaperCoordinator.AlertMessage))
        {
            PresentPendingAlert();
        }
    }

    private async void PresentPendingAlert()
    {
        if (Model is null)
        {
            return;
        }

        var message = Model.Coordinator.AlertMessage;
        if (string.IsNullOrEmpty(message) || _isShowingAlert)
        {
            return;
        }

        _isShowingAlert = true;
        Model.Coordinator.AlertMessage = null;

        try
        {
            if (!IsVisible)
            {
                Show();
            }

            await DialogWindow.ShowMessageAsync(this, "Wallpache", message);
        }
        finally
        {
            _isShowingAlert = false;
        }
    }
}
