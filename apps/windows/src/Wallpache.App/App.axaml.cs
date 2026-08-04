using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Microsoft.Win32;
using Wallpache.App.Core;
using Wallpache.App.Desktop;
using Wallpache.App.Displays;
using Wallpache.App.Library;
using Wallpache.App.Persistence;
using Wallpache.App.Support;
using Wallpache.App.System;
using Wallpache.App.UI;
using Wallpache.App.ViewModels;
using Wallpache.App.Views;

namespace Wallpache.App;

/// <summary>
/// Composition root and lifecycle owner.
///
/// The coordinator is built here so its dependencies are wired in exactly one
/// place, and so the wallpaper is restored only after the app has fully
/// launched.
/// </summary>
public partial class App : Application
{
    private const string SettingsKeyPath = @"Software\Wallpache";
    private const string FirstRunValueName = "HasOpenedLibraryOnFirstRun";

    private WallpaperCoordinator? _coordinator;
    private SystemEventPump? _pump;
    private TrayIconService? _tray;
    private MainWindow? _window;
    private MainViewModel? _model;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A tray utility: closing the window must not end the process, and no
            // window is required for the app to be running.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Shutdown();

            Compose();

            // A tray-only app is easy to miss, so the library window is opened
            // once, the first time Wallpache runs.
            if (!HasOpenedOnFirstRun())
            {
                MarkOpenedOnFirstRun();
                ShowLibrary();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    // MARK: - Composition

    private void Compose()
    {
        var storage = MakeStorage();
        Log.Configure(storage.LogsDirectory);

        _pump = new SystemEventPump();

        var desktopHost = new DesktopHostService();
        var displayManager = new DisplayManager();
        var systemMonitor = new PowerAndSessionMonitor(_pump);
        var explorerMonitor = new ExplorerMonitor(_pump, () => desktopHost.IsHostValid);

        _coordinator = new WallpaperCoordinator(
            new SettingsStore(storage.SettingsPath),
            new WallpaperLibraryService(storage),
            displayManager,
            desktopHost,
            explorerMonitor,
            systemMonitor,
            new StartupService(),
            new DesktopPictureService());

        _coordinator.Start();

        _model = new MainViewModel(_coordinator);
        _tray = new TrayIconService(_coordinator, ShowLibrary, ImportVideo, Quit);
    }

    /// <summary>
    /// Falls back to a temporary folder when Local Application Data is
    /// unavailable. Without storage the library cannot work, but the app should
    /// still launch and explain itself rather than crash on start.
    /// </summary>
    private static WallpaperStorage MakeStorage()
    {
        try
        {
            return WallpaperStorage.MakeDefault();
        }
        catch (Exception error)
        {
            Log.Lifecycle.Error($"Storage unavailable: {error.Message}");

            var fallback = new WallpaperStorage(Path.Combine(Path.GetTempPath(), "Wallpache"));

            try
            {
                fallback.PrepareDirectories();
            }
            catch (Exception)
            {
                // Nothing further to try; imports will report the failure.
            }

            return fallback;
        }
    }

    // MARK: - Window and tray actions

    private void ShowLibrary()
    {
        if (_coordinator is null || _model is null)
        {
            return;
        }

        if (_window is null)
        {
            _window = new MainWindow { DataContext = _model };
            DragDrop.SetAllowDrop(_window, true);
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private async void ImportVideo()
    {
        ShowLibrary();

        if (_window is not null)
        {
            await _window.PresentImportPickerAsync();
        }
    }

    private void Quit()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void Shutdown()
    {
        _tray?.Dispose();
        _coordinator?.Shutdown();
        _pump?.Dispose();
    }

    // MARK: - First run

    private static bool HasOpenedOnFirstRun()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
            return key?.GetValue(FirstRunValueName) is int value && value != 0;
        }
        catch (Exception)
        {
            // Treat an unreadable key as "already shown": opening the window on
            // every launch would be worse than never opening it automatically.
            return true;
        }
    }

    private static void MarkOpenedOnFirstRun()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            key?.SetValue(FirstRunValueName, 1, RegistryValueKind.DWord);
        }
        catch (Exception error)
        {
            Log.Lifecycle.Error($"Could not record the first run: {error.Message}");
        }
    }
}
