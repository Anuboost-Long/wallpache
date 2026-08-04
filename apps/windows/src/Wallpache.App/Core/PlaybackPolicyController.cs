using System;
using Wallpache.App.Persistence;
using Wallpache.App.Support;
using Wallpache.App.System;

namespace Wallpache.App.Core;

/// <summary>Why playback is currently suspended, for display in the UI and the tray.</summary>
public enum SuspensionReason
{
    None,
    NotEnabled,
    UserPaused,
    SystemAsleep,
    ScreenLocked,
    BatterySaver,
    BatteryPower
}

public static class SuspensionReasonExtensions
{
    public static string Description(this SuspensionReason reason) => reason switch
    {
        SuspensionReason.NotEnabled => "No wallpaper running",
        SuspensionReason.UserPaused => "Paused",
        SuspensionReason.SystemAsleep => "Paused while the PC sleeps",
        SuspensionReason.ScreenLocked => "Paused while the screen is locked",
        SuspensionReason.BatterySaver => "Paused in Battery Saver",
        SuspensionReason.BatteryPower => "Paused on battery power",
        _ => "Running"
    };
}

/// <summary>
/// Decides whether wallpapers may play right now.
///
/// The whole rule set is one derived value:
///
/// <code>
/// shouldPlay = wallpaperEnabled
///     AND userDidNotPause
///     AND systemIsAwake
///     AND energyPolicyAllowsPlayback
/// </code>
///
/// Keeping it in one place means the coordinator never has to reason about
/// sleep, lock, and battery state separately.
/// </summary>
public sealed class PlaybackPolicyController
{
    private readonly IPowerConditions _power;
    private bool _lastDecision;

    private bool _isWallpaperEnabled;
    private bool _isUserPaused;
    private bool _isSystemAsleep;
    private bool _isScreenLocked;
    private EnergyPreferences _preferences = new();

    public PlaybackPolicyController(IPowerConditions power)
    {
        _power = power;
    }

    /// <summary>Fired whenever the decision flips, never on redundant input changes.</summary>
    public event Action<bool>? DecisionChanged;

    public bool IsWallpaperEnabled
    {
        get => _isWallpaperEnabled;
        set => Update(ref _isWallpaperEnabled, value);
    }

    public bool IsUserPaused
    {
        get => _isUserPaused;
        set => Update(ref _isUserPaused, value);
    }

    public bool IsSystemAsleep
    {
        get => _isSystemAsleep;
        set => Update(ref _isSystemAsleep, value);
    }

    public bool IsScreenLocked
    {
        get => _isScreenLocked;
        set => Update(ref _isScreenLocked, value);
    }

    public EnergyPreferences Preferences
    {
        get => _preferences;
        set
        {
            if (_preferences.Matches(value))
            {
                return;
            }

            _preferences = value;
            PublishIfChanged();
        }
    }

    public bool ShouldPlay => Reason == SuspensionReason.None;

    public SuspensionReason Reason
    {
        get
        {
            if (!_isWallpaperEnabled)
            {
                return SuspensionReason.NotEnabled;
            }

            if (_isUserPaused)
            {
                return SuspensionReason.UserPaused;
            }

            if (_isSystemAsleep)
            {
                return SuspensionReason.SystemAsleep;
            }

            if (_preferences.PauseWhenScreenLocked && _isScreenLocked)
            {
                return SuspensionReason.ScreenLocked;
            }

            if (_preferences.PauseInBatterySaver && _power.IsBatterySaverEnabled)
            {
                return SuspensionReason.BatterySaver;
            }

            if (_preferences.PauseOnBatteryPower && _power.IsOnBatteryPower)
            {
                return SuspensionReason.BatteryPower;
            }

            return SuspensionReason.None;
        }
    }

    /// <summary>
    /// Called when an external input changed without going through a property,
    /// such as a Battery Saver or power-scheme notification.
    /// </summary>
    public void Reevaluate() => PublishIfChanged();

    private void Update(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PublishIfChanged();
    }

    private void PublishIfChanged()
    {
        var decision = ShouldPlay;
        if (decision == _lastDecision)
        {
            return;
        }

        _lastDecision = decision;
        Log.Policy.Info($"shouldPlay: {decision} ({Reason.Description()})");
        DecisionChanged?.Invoke(decision);
    }
}
