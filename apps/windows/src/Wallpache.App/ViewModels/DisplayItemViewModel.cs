using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Wallpache.App.Core;
using Wallpache.App.Displays;
using Wallpache.App.Library;
using Wallpache.App.Playback;

namespace Wallpache.App.ViewModels;

/// <summary>
/// The "None" entry in a display's wallpaper picker. A null selection cannot be
/// distinguished from "nothing selected yet" in a combo box, so the absence of a
/// wallpaper is modelled as a real choice.
/// </summary>
public sealed class WallpaperChoice
{
    public static readonly WallpaperChoice None = new(null);

    public WallpaperChoice(WallpaperRecord? record) => Record = record;

    public WallpaperRecord? Record { get; }

    public string Name => Record?.Name ?? "None";

    public override string ToString() => Name;
}

/// <summary>Per-display wallpaper assignment and playback settings.</summary>
public sealed partial class DisplayItemViewModel : ViewModelBase
{
    private readonly WallpaperCoordinator _coordinator;
    private bool _isSyncing;

    public DisplayItemViewModel(WallpaperCoordinator coordinator, DisplayDescriptor display)
    {
        _coordinator = coordinator;
        Display = display;

        ScalingModes = new ObservableCollection<ScalingMode>(Enum.GetValues<ScalingMode>());
        Rates = new ObservableCollection<double>(DisplayWallpaperConfiguration.SupportedRates);

        Sync();
    }

    public DisplayDescriptor Display { get; private set; }

    public string Name => Display.Name;

    public string ResolutionDescription => Display.ResolutionDescription;

    public bool IsPrimary => Display.IsPrimary;

    public ObservableCollection<WallpaperChoice> Wallpapers { get; } = [];

    public ObservableCollection<ScalingMode> ScalingModes { get; }

    public ObservableCollection<double> Rates { get; }

    [ObservableProperty]
    public partial WallpaperChoice? SelectedWallpaper { get; set; }

    [ObservableProperty]
    public partial ScalingMode SelectedScalingMode { get; set; } = ScalingMode.Fill;

    [ObservableProperty]
    public partial double SelectedRate { get; set; } = 1.0;

    /// <summary>The stored value is "muted"; the switch reads better as "play audio".</summary>
    [ObservableProperty]
    public partial bool PlaysAudio { get; set; }

    [ObservableProperty]
    public partial bool MatchesDesktopPicture { get; set; }

    [ObservableProperty]
    public partial bool HasWallpaper { get; private set; }

    /// <summary>Re-reads the coordinator's state into the bound properties.</summary>
    public void Sync(DisplayDescriptor? display = null)
    {
        if (display is not null)
        {
            Display = display;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ResolutionDescription));
            OnPropertyChanged(nameof(IsPrimary));
        }

        _isSyncing = true;
        try
        {
            RebuildWallpaperChoices();

            var configuration = _coordinator.DisplayConfigurationFor(Display.Id);
            SelectedWallpaper = Wallpapers.FirstOrDefault(choice => choice.Record?.Id == configuration.WallpaperId)
                ?? Wallpapers.FirstOrDefault();
            SelectedScalingMode = configuration.ScalingMode;
            SelectedRate = NearestRate(configuration.PlaybackRate);
            PlaysAudio = !configuration.IsMuted;
            MatchesDesktopPicture = configuration.SetsDesktopPicture;
            HasWallpaper = configuration.WallpaperId is not null;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void RemoveWallpaper() => _coordinator.ClearWallpaper(Display.Id);

    // MARK: - Change handlers

    partial void OnSelectedWallpaperChanged(WallpaperChoice? value)
    {
        if (_isSyncing)
        {
            return;
        }

        if (value?.Record is null)
        {
            _coordinator.ClearWallpaper(Display.Id);
            return;
        }

        _coordinator.Apply(value.Record, Display.Id);
    }

    partial void OnSelectedScalingModeChanged(ScalingMode value)
    {
        if (!_isSyncing)
        {
            _coordinator.SetScalingMode(value, Display.Id);
        }
    }

    partial void OnSelectedRateChanged(double value)
    {
        if (!_isSyncing)
        {
            _coordinator.SetPlaybackRate(value, Display.Id);
        }
    }

    partial void OnPlaysAudioChanged(bool value)
    {
        if (!_isSyncing)
        {
            _coordinator.SetMuted(!value, Display.Id);
        }
    }

    partial void OnMatchesDesktopPictureChanged(bool value)
    {
        if (!_isSyncing)
        {
            _coordinator.SetDesktopPicture(value, Display.Id);
        }
    }

    // MARK: - Private

    private void RebuildWallpaperChoices()
    {
        var desired = new List<WallpaperChoice> { WallpaperChoice.None };
        desired.AddRange(_coordinator.Library.Select(record => new WallpaperChoice(record)));

        Wallpapers.Clear();
        foreach (var choice in desired)
        {
            Wallpapers.Add(choice);
        }
    }

    /// <summary>
    /// Snaps a stored rate onto one of the offered values, so a rate written by
    /// a future build cannot leave the picker with nothing selected.
    /// </summary>
    private static double NearestRate(double rate) =>
        DisplayWallpaperConfiguration.SupportedRates
            .OrderBy(candidate => Math.Abs(candidate - rate))
            .First();
}
