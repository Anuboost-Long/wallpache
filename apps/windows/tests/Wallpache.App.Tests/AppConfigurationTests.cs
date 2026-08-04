using System.Text.Json;
using Wallpache.App.Displays;
using Wallpache.App.Library;
using Wallpache.App.Persistence;
using Wallpache.App.Playback;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// Settings survive every relaunch, so the decoding side has to tolerate files
/// written by older builds and files that were damaged. A configuration that
/// refuses to load is a library the user has lost.
/// </summary>
public class AppConfigurationTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "WallpacheTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public AppConfigurationTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    // MARK: - Model behaviour

    [Fact]
    public void SetConfigurationReplacesRatherThanDuplicating()
    {
        var configuration = new AppConfiguration();
        configuration.SetConfiguration(new DisplayWallpaperConfiguration("DISPLAY1"));
        configuration.SetConfiguration(new DisplayWallpaperConfiguration("DISPLAY1")
        {
            ScalingMode = ScalingMode.Fit
        });

        Assert.Single(configuration.DisplayConfigurations);
        Assert.Equal(ScalingMode.Fit, configuration.DisplayConfigurations[0].ScalingMode);
    }

    [Fact]
    public void RemoveAssignmentsClearsEveryDisplayUsingTheWallpaper()
    {
        var id = Guid.NewGuid();
        var configuration = new AppConfiguration();
        configuration.SetConfiguration(new DisplayWallpaperConfiguration("DISPLAY1") { WallpaperId = id });
        configuration.SetConfiguration(new DisplayWallpaperConfiguration("DISPLAY2") { WallpaperId = id });
        configuration.SetConfiguration(new DisplayWallpaperConfiguration("DISPLAY3") { WallpaperId = Guid.NewGuid() });

        configuration.RemoveAssignments(id);

        Assert.Null(configuration.Configuration("DISPLAY1")!.WallpaperId);
        Assert.Null(configuration.Configuration("DISPLAY2")!.WallpaperId);
        Assert.NotNull(configuration.Configuration("DISPLAY3")!.WallpaperId);
    }

    [Fact]
    public void NormalizeRepairsAnOutOfRangeRateAndDropsEmptyEntries()
    {
        var configuration = new AppConfiguration();
        configuration.DisplayConfigurations.Add(new DisplayWallpaperConfiguration("DISPLAY1") { PlaybackRate = 0 });
        configuration.DisplayConfigurations.Add(new DisplayWallpaperConfiguration(string.Empty));
        configuration.Library.Add(new WallpaperRecord { RelativePath = string.Empty });

        configuration.Normalize();

        Assert.Single(configuration.DisplayConfigurations);
        Assert.Equal(1.0, configuration.DisplayConfigurations[0].PlaybackRate);
        Assert.Empty(configuration.Library);
    }

    // MARK: - Round trip

    [Fact]
    public void SurvivesASaveAndLoadRoundTrip()
    {
        var store = new SettingsStore(SettingsPath);
        var wallpaperId = Guid.NewGuid();

        var original = new AppConfiguration
        {
            IsWallpaperEnabled = true,
            LaunchAtSignIn = true,
            Library =
            [
                new WallpaperRecord
                {
                    Id = wallpaperId,
                    Name = "Aurora",
                    RelativePath = "Wallpapers/aurora.mp4",
                    ThumbnailRelativePath = "Thumbnails/aurora.png",
                    Duration = 12.5,
                    Width = 3840,
                    Height = 2160,
                    FileSize = 4242,
                    SourceFileName = "aurora.mp4"
                }
            ]
        };

        original.SetConfiguration(new DisplayWallpaperConfiguration(@"\\?\DISPLAY#ABC1234")
        {
            WallpaperId = wallpaperId,
            ScalingMode = ScalingMode.Stretch,
            PlaybackRate = 1.5,
            IsMuted = false,
            SetsDesktopPicture = true
        });
        original.PreviousDesktopPictures[@"\\?\DISPLAY#ABC1234"] = @"C:\Windows\Web\Wallpaper\img0.jpg";

        store.Save(original);
        var loaded = new SettingsStore(SettingsPath).Load();

        Assert.True(loaded.IsWallpaperEnabled);
        Assert.True(loaded.LaunchAtSignIn);

        var record = Assert.Single(loaded.Library);
        Assert.Equal("Aurora", record.Name);
        Assert.Equal(3840, record.Width);
        Assert.Equal("aurora.mp4", record.SourceFileName);

        var display = Assert.Single(loaded.DisplayConfigurations);
        Assert.Equal(wallpaperId, display.WallpaperId);
        Assert.Equal(ScalingMode.Stretch, display.ScalingMode);
        Assert.Equal(1.5, display.PlaybackRate);
        Assert.False(display.IsMuted);
        Assert.True(display.SetsDesktopPicture);

        Assert.Equal(
            @"C:\Windows\Web\Wallpaper\img0.jpg",
            loaded.PreviousDesktopPictures[@"\\?\DISPLAY#ABC1234"]);
    }

    [Fact]
    public void ScalingModeIsWrittenAsAStableName()
    {
        var store = new SettingsStore(SettingsPath);
        var configuration = new AppConfiguration();
        configuration.SetConfiguration(new DisplayWallpaperConfiguration("DISPLAY1")
        {
            ScalingMode = ScalingMode.Center
        });

        store.Save(configuration);

        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        var mode = document.RootElement
            .GetProperty("displays")[0]
            .GetProperty("scalingMode")
            .GetString();

        Assert.Equal("Center", mode);
    }

    // MARK: - Damage tolerance

    [Fact]
    public void MissingFileYieldsAnEmptyConfiguration()
    {
        var loaded = new SettingsStore(SettingsPath).Load();

        Assert.Empty(loaded.Library);
        Assert.False(loaded.IsWallpaperEnabled);
    }

    [Fact]
    public void CorruptFileIsQuarantinedInsteadOfBlockingLaunch()
    {
        File.WriteAllText(SettingsPath, "{ this is not json");

        var loaded = new SettingsStore(SettingsPath).Load();

        Assert.Empty(loaded.Library);
        Assert.True(File.Exists(SettingsPath + ".corrupt"));
    }

    [Fact]
    public void AFileFromAnOlderBuildKeepsWhatItDoesCarry()
    {
        // Only the fields an early build wrote; everything else must default.
        File.WriteAllText(SettingsPath, """
        {
          "version": 1,
          "wallpapers": [{ "id": "0f8fad5b-d9cb-469f-a165-70867728950e",
                           "name": "Old", "relativePath": "Wallpapers/old.mp4" }],
          "displays": [{ "displayId": "DISPLAY1" }]
        }
        """);

        var loaded = new SettingsStore(SettingsPath).Load();

        var record = Assert.Single(loaded.Library);
        Assert.Equal("Old", record.Name);
        Assert.Null(record.StillRelativePath);

        var display = Assert.Single(loaded.DisplayConfigurations);
        Assert.Equal(ScalingMode.Fill, display.ScalingMode);
        Assert.Equal(1.0, display.PlaybackRate);
        Assert.True(display.IsMuted);
        Assert.False(display.SetsDesktopPicture);
        Assert.NotNull(loaded.EnergyPreferences);
    }

    [Fact]
    public void SavingTwiceLeavesNoTemporaryFileBehind()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new AppConfiguration());
        store.Save(new AppConfiguration { IsWallpaperEnabled = true });

        Assert.True(new SettingsStore(SettingsPath).Load().IsWallpaperEnabled);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }
}
