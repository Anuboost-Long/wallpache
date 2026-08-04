using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Wallpache.App.Displays;
using Wallpache.App.Library;

namespace Wallpache.App.Persistence;

/// <summary>
/// Energy rules the user can opt out of. Nothing here changes behaviour
/// silently: every rule maps to one switch in Settings.
/// </summary>
public sealed class EnergyPreferences
{
    [JsonPropertyName("pauseInBatterySaver")]
    public bool PauseInBatterySaver { get; set; } = true;

    [JsonPropertyName("pauseOnBatteryPower")]
    public bool PauseOnBatteryPower { get; set; } = true;

    [JsonPropertyName("pauseWhenScreenLocked")]
    public bool PauseWhenScreenLocked { get; set; } = true;

    public EnergyPreferences Copy() => new()
    {
        PauseInBatterySaver = PauseInBatterySaver,
        PauseOnBatteryPower = PauseOnBatteryPower,
        PauseWhenScreenLocked = PauseWhenScreenLocked
    };

    public bool Matches(EnergyPreferences other) =>
        PauseInBatterySaver == other.PauseInBatterySaver
        && PauseOnBatteryPower == other.PauseOnBatteryPower
        && PauseWhenScreenLocked == other.PauseWhenScreenLocked;
}

/// <summary>
/// Everything the app restores after a relaunch. Transient objects (players,
/// monitor handles, window handles) are deliberately absent.
///
/// Every property tolerates a missing value so that a settings file written by
/// an older build never wipes a user's assignments.
/// </summary>
public sealed class AppConfiguration
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("wallpapers")]
    public List<WallpaperRecord> Library { get; set; } = [];

    [JsonPropertyName("displays")]
    public List<DisplayWallpaperConfiguration> DisplayConfigurations { get; set; } = [];

    /// <summary>Whether wallpapers were running when the app last quit.</summary>
    [JsonPropertyName("isWallpaperEnabled")]
    public bool IsWallpaperEnabled { get; set; }

    [JsonPropertyName("launchAtSignIn")]
    public bool LaunchAtSignIn { get; set; }

    [JsonPropertyName("energyPreferences")]
    public EnergyPreferences EnergyPreferences { get; set; } = new();

    /// <summary>
    /// The desktop picture each display had before the app replaced it, keyed by
    /// display id. Kept so switching the option off restores the user's own
    /// wallpaper instead of stranding them with a video frame.
    /// </summary>
    [JsonPropertyName("previousDesktopPictures")]
    public Dictionary<string, string> PreviousDesktopPictures { get; set; } = [];

    public WallpaperRecord? Wallpaper(Guid? id) =>
        id is null ? null : Library.FirstOrDefault(record => record.Id == id);

    public DisplayWallpaperConfiguration? Configuration(string displayId) =>
        DisplayConfigurations.FirstOrDefault(configuration => configuration.DisplayId == displayId);

    /// <summary>
    /// Inserts or replaces one display assignment, keeping the collection free
    /// of duplicates for the same display.
    /// </summary>
    public void SetConfiguration(DisplayWallpaperConfiguration configuration)
    {
        var index = DisplayConfigurations.FindIndex(existing => existing.DisplayId == configuration.DisplayId);
        if (index >= 0)
        {
            DisplayConfigurations[index] = configuration;
        }
        else
        {
            DisplayConfigurations.Add(configuration);
        }
    }

    /// <summary>
    /// Drops assignments that point at a wallpaper that is no longer in the
    /// library, so a deleted video cannot resurrect a broken session.
    /// </summary>
    public void RemoveAssignments(Guid wallpaperId)
    {
        foreach (var configuration in DisplayConfigurations.Where(item => item.WallpaperId == wallpaperId))
        {
            configuration.WallpaperId = null;
        }
    }

    /// <summary>Repairs anything a hand-edited or partially written file could carry.</summary>
    public void Normalize()
    {
        Version = CurrentVersion;
        Library ??= [];
        DisplayConfigurations ??= [];
        EnergyPreferences ??= new EnergyPreferences();
        PreviousDesktopPictures ??= [];

        Library.RemoveAll(record => string.IsNullOrEmpty(record.RelativePath));
        DisplayConfigurations.RemoveAll(configuration => string.IsNullOrEmpty(configuration.DisplayId));

        foreach (var configuration in DisplayConfigurations)
        {
            configuration.Normalize();
        }
    }
}
