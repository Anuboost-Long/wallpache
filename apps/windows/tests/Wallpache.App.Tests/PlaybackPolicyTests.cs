using Wallpache.App.Core;
using Wallpache.App.Persistence;
using Wallpache.App.System;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// The whole pause/resume rule set lives in one derived value, so it is worth
/// pinning down precisely: a wrong answer here means a wallpaper that runs on
/// battery, or one that never comes back after unlocking.
/// </summary>
public class PlaybackPolicyTests
{
    private sealed class FakePower : IPowerConditions
    {
        public bool IsBatterySaverEnabled { get; set; }

        public bool IsOnBatteryPower { get; set; }
    }

    private static PlaybackPolicyController MakeRunningPolicy(FakePower power)
    {
        var policy = new PlaybackPolicyController(power) { IsWallpaperEnabled = true };
        return policy;
    }

    [Fact]
    public void DoesNotPlayUntilAWallpaperIsEnabled()
    {
        var policy = new PlaybackPolicyController(new FakePower());

        Assert.False(policy.ShouldPlay);
        Assert.Equal(SuspensionReason.NotEnabled, policy.Reason);
    }

    [Fact]
    public void PlaysWhenEnabledAndNothingObjects()
    {
        var policy = MakeRunningPolicy(new FakePower());

        Assert.True(policy.ShouldPlay);
        Assert.Equal(SuspensionReason.None, policy.Reason);
    }

    [Fact]
    public void UserPauseOutranksEverythingExceptBeingDisabled()
    {
        var policy = MakeRunningPolicy(new FakePower { IsOnBatteryPower = true });
        policy.IsUserPaused = true;

        Assert.Equal(SuspensionReason.UserPaused, policy.Reason);
    }

    [Fact]
    public void SleepPausesRegardlessOfPreferences()
    {
        var policy = MakeRunningPolicy(new FakePower());
        policy.Preferences = new EnergyPreferences
        {
            PauseInBatterySaver = false,
            PauseOnBatteryPower = false,
            PauseWhenScreenLocked = false
        };

        policy.IsSystemAsleep = true;

        Assert.Equal(SuspensionReason.SystemAsleep, policy.Reason);
    }

    [Fact]
    public void ScreenLockOnlyPausesWhenThePreferenceIsOn()
    {
        var policy = MakeRunningPolicy(new FakePower());
        policy.IsScreenLocked = true;
        Assert.Equal(SuspensionReason.ScreenLocked, policy.Reason);

        policy.Preferences = new EnergyPreferences { PauseWhenScreenLocked = false };
        Assert.Equal(SuspensionReason.None, policy.Reason);
    }

    [Fact]
    public void BatterySaverOutranksPlainBatteryPower()
    {
        var power = new FakePower { IsBatterySaverEnabled = true, IsOnBatteryPower = true };
        var policy = MakeRunningPolicy(power);

        Assert.Equal(SuspensionReason.BatterySaver, policy.Reason);
    }

    [Fact]
    public void BatteryPowerPausesWhenThePreferenceIsOn()
    {
        var policy = MakeRunningPolicy(new FakePower { IsOnBatteryPower = true });

        Assert.Equal(SuspensionReason.BatteryPower, policy.Reason);
    }

    [Fact]
    public void PublishesOnlyWhenTheDecisionActuallyFlips()
    {
        var policy = new PlaybackPolicyController(new FakePower());
        var decisions = new List<bool>();
        policy.DecisionChanged += decision => decisions.Add(decision);

        policy.IsWallpaperEnabled = true;   // false -> true
        policy.IsScreenLocked = true;       // true  -> false
        policy.IsUserPaused = true;         // already false, no event
        policy.IsUserPaused = false;        // still false (locked), no event
        policy.IsScreenLocked = false;      // false -> true

        Assert.Equal([true, false, true], decisions);
    }

    [Fact]
    public void ReevaluatePicksUpAPowerChangeThatCameFromOutside()
    {
        var power = new FakePower();
        var policy = MakeRunningPolicy(power);

        var decisions = new List<bool>();
        policy.DecisionChanged += decision => decisions.Add(decision);

        power.IsOnBatteryPower = true;
        policy.Reevaluate();

        Assert.Equal([false], decisions);
    }
}
