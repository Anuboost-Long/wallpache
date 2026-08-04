using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Wallpache.App.Core;
using Wallpache.App.Support;
using Wallpache.App.System;

namespace Wallpache.App.UI;

/// <summary>
/// The system-tray menu: status, transport, and the few actions worth reaching
/// without opening a window.
///
/// It is the Windows counterpart of the macOS menu-bar panel, and it is rebuilt
/// whenever the library, the status, or the startup state changes so the two
/// surfaces never disagree.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly WallpaperCoordinator _coordinator;
    private readonly Action _openLibrary;
    private readonly Action _import;
    private readonly Action _quit;
    private readonly TrayIcon _trayIcon;

    public TrayIconService(
        WallpaperCoordinator coordinator,
        Action openLibrary,
        Action import,
        Action quit)
    {
        _coordinator = coordinator;
        _openLibrary = openLibrary;
        _import = import;
        _quit = quit;

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Wallpache",
            IsVisible = true,
            Icon = LoadIcon()
        };

        // Double-clicking the tray icon is the Windows habit for "show me the app".
        _trayIcon.Clicked += (_, _) => _openLibrary();

        coordinator.LibraryChanged += Rebuild;
        coordinator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(WallpaperCoordinator.StatusMessage)
                or nameof(WallpaperCoordinator.IsWallpaperEnabled)
                or nameof(WallpaperCoordinator.IsUserPaused)
                or nameof(WallpaperCoordinator.StartupState)
                or nameof(WallpaperCoordinator.IsImporting))
            {
                Rebuild();
            }
        };

        Rebuild();
    }

    public void Dispose()
    {
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
    }

    private void Rebuild()
    {
        var menu = new NativeMenu();

        // Header: the same one-line status the library toolbar shows.
        menu.Add(new NativeMenuItem($"Wallpache — {_coordinator.StatusMessage}") { IsEnabled = false });
        menu.Add(new NativeMenuItemSeparator());

        if (_coordinator.Library.Count == 0)
        {
            menu.Add(new NativeMenuItem("Import a video to use it as your live wallpaper.") { IsEnabled = false });
        }
        else
        {
            menu.Add(BuildApplyMenu());

            var pause = new NativeMenuItem(_coordinator.IsUserPaused ? "Resume" : "Pause")
            {
                IsEnabled = _coordinator.IsWallpaperEnabled
            };
            pause.Click += (_, _) => _coordinator.SetPaused(!_coordinator.IsUserPaused);
            menu.Add(pause);

            var stop = new NativeMenuItem("Stop") { IsEnabled = _coordinator.IsWallpaperEnabled };
            stop.Click += (_, _) => _coordinator.StopAll();
            menu.Add(stop);
        }

        menu.Add(new NativeMenuItemSeparator());

        var import = new NativeMenuItem("Import Video…") { IsEnabled = !_coordinator.IsImporting };
        import.Click += (_, _) => _import();
        menu.Add(import);

        var library = new NativeMenuItem("Wallpaper Library & Settings…");
        library.Click += (_, _) => _openLibrary();
        menu.Add(library);

        var startup = new NativeMenuItem("Launch at Sign-In")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _coordinator.StartupState == StartupService.State.Enabled,
            IsEnabled = _coordinator.StartupState != StartupService.State.Unavailable
        };
        startup.Click += (_, _) =>
            _coordinator.SetLaunchAtSignIn(_coordinator.StartupState != StartupService.State.Enabled);
        menu.Add(startup);

        menu.Add(new NativeMenuItemSeparator());

        var quit = new NativeMenuItem("Quit Wallpache");
        quit.Click += (_, _) => _quit();
        menu.Add(quit);

        _trayIcon.Menu = menu;
    }

    private NativeMenuItem BuildApplyMenu()
    {
        var submenu = new NativeMenu();

        foreach (var record in _coordinator.Library)
        {
            var captured = record;
            var item = new NativeMenuItem(captured.Name);
            item.Click += (_, _) => _coordinator.ApplyToAllDisplays(captured);
            submenu.Add(item);
        }

        return new NativeMenuItem("Apply to All Displays") { Menu = submenu };
    }

    private static WindowIcon? LoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Wallpache/Assets/wallpache.ico"));
            return new WindowIcon(stream);
        }
        catch (Exception error)
        {
            // A missing tray icon is cosmetic; the menu still works.
            Log.Lifecycle.Error($"Tray icon could not be loaded: {error.Message}");
            return null;
        }
    }
}
