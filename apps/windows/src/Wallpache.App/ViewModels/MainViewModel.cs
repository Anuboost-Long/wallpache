using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Wallpache.App.Core;
using Wallpache.App.Persistence;
using Wallpache.App.System;

namespace Wallpache.App.ViewModels;

/// <summary>
/// The main window's view model. It owns nothing: every value is a projection of
/// <see cref="WallpaperCoordinator"/>, so the window and the tray always agree.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    public MainViewModel(WallpaperCoordinator coordinator)
    {
        Coordinator = coordinator;

        coordinator.LibraryChanged += RebuildLibrary;
        coordinator.DisplaysChanged += RebuildDisplays;

        // The tray can toggle launch-at-sign-in too, so the Settings tab follows
        // the coordinator rather than only its own checkbox.
        coordinator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WallpaperCoordinator.StartupState))
            {
                OnPropertyChanged(nameof(LaunchAtSignIn));
                OnPropertyChanged(nameof(IsStartupUnavailable));
            }
        };

        RebuildLibrary();
        RebuildDisplays();
    }

    public WallpaperCoordinator Coordinator { get; }

    public ObservableCollection<WallpaperItemViewModel> LibraryItems { get; } = [];

    public ObservableCollection<DisplayItemViewModel> DisplayItems { get; } = [];

    [ObservableProperty]
    public partial bool IsLibraryEmpty { get; private set; } = true;

    // MARK: - Settings

    public bool LaunchAtSignIn
    {
        get => Coordinator.StartupState == StartupService.State.Enabled;
        set
        {
            Coordinator.SetLaunchAtSignIn(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStartupUnavailable));
        }
    }

    public bool IsStartupUnavailable => Coordinator.StartupState == StartupService.State.Unavailable;

    public bool PauseInBatterySaver
    {
        get => Coordinator.EnergyPreferences.PauseInBatterySaver;
        set => UpdateEnergy(preferences => preferences.PauseInBatterySaver = value);
    }

    public bool PauseOnBatteryPower
    {
        get => Coordinator.EnergyPreferences.PauseOnBatteryPower;
        set => UpdateEnergy(preferences => preferences.PauseOnBatteryPower = value);
    }

    public bool PauseWhenScreenLocked
    {
        get => Coordinator.EnergyPreferences.PauseWhenScreenLocked;
        set => UpdateEnergy(preferences => preferences.PauseWhenScreenLocked = value);
    }

    // MARK: - Private

    private void UpdateEnergy(Action<EnergyPreferences> mutate)
    {
        Coordinator.SetEnergyPreferences(mutate);
        OnPropertyChanged(nameof(PauseInBatterySaver));
        OnPropertyChanged(nameof(PauseOnBatteryPower));
        OnPropertyChanged(nameof(PauseWhenScreenLocked));
    }

    /// <summary>
    /// Rebuilds only what changed. Cells are reused by id so an unrelated
    /// library edit does not throw away every loaded thumbnail.
    /// </summary>
    private void RebuildLibrary()
    {
        var existing = LibraryItems.ToDictionary(item => item.Record.Id);
        LibraryItems.Clear();

        foreach (var record in Coordinator.Library)
        {
            if (existing.TryGetValue(record.Id, out var item) && ReferenceEquals(item.Record, record))
            {
                item.RaiseNameChanged();
                LibraryItems.Add(item);
                continue;
            }

            var created = new WallpaperItemViewModel(Coordinator, record);
            created.RefreshDisplays();
            LibraryItems.Add(created);
            _ = created.LoadThumbnailAsync();
        }

        IsLibraryEmpty = LibraryItems.Count == 0;

        foreach (var display in DisplayItems)
        {
            display.Sync();
        }
    }

    private void RebuildDisplays()
    {
        var existing = DisplayItems.ToDictionary(item => item.Display.Id);
        DisplayItems.Clear();

        foreach (var display in Coordinator.Displays)
        {
            if (existing.TryGetValue(display.Id, out var item))
            {
                item.Sync(display);
                DisplayItems.Add(item);
                continue;
            }

            DisplayItems.Add(new DisplayItemViewModel(Coordinator, display));
        }

        foreach (var item in LibraryItems)
        {
            item.RefreshDisplays();
        }
    }
}
