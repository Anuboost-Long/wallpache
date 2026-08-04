using Avalonia.Controls;
using Avalonia.Interactivity;
using Wallpache.App.Core;
using Wallpache.App.Interop;
using Wallpache.App.Library;
using Wallpache.App.Playback;
using Wallpache.App.UI;

namespace Wallpache.App.Views;

/// <summary>
/// Plays a wallpaper in a window before the user commits it to a display.
///
/// It reuses <see cref="VideoLoopPlayer"/> and the composition surface, so the
/// preview loops exactly the way the real wallpaper will.
/// </summary>
public partial class WallpaperPreviewWindow : Window
{
    private readonly WallpaperCoordinator? _coordinator;
    private readonly WallpaperRecord? _record;
    private VideoLoopPlayer? _player;

    /// <summary>Parameterless constructor for the XAML previewer.</summary>
    public WallpaperPreviewWindow()
    {
        InitializeComponent();
    }

    public WallpaperPreviewWindow(WallpaperCoordinator coordinator, WallpaperRecord record)
        : this()
    {
        _coordinator = coordinator;
        _record = record;

        NameText.Text = record.Name;
        SubtitleText.Text = record.Subtitle;

        Opened += async (_, _) =>
        {
            StillImage.Source = await ThumbnailLoader.LoadAsync(
                coordinator.Storage.PreviewImagePath(record),
                ThumbnailLoader.PreviewPixelWidth);

            // Reduced motion means the preview waits for an explicit start.
            if (NativeMethods.AreClientAreaAnimationsEnabled())
            {
                StartPreview();
            }
        };

        Closed += (_, _) => StopPreview();
    }

    private void OnStartPreview(object? sender, RoutedEventArgs args) => StartPreview();

    private void OnClose(object? sender, RoutedEventArgs args) => Close();

    private void OnApply(object? sender, RoutedEventArgs args)
    {
        if (_coordinator is not null && _record is not null)
        {
            _coordinator.ApplyToAllDisplays(_record);
        }

        Close();
    }

    private void StartPreview()
    {
        if (_player is not null || _coordinator is null || _record is null)
        {
            return;
        }

        _player = new VideoLoopPlayer(_coordinator.Storage.VideoPath(_record), isMuted: true, rate: 1.0);

        VideoView.Attach(_player, _record.Width, _record.Height, ScalingMode.Fit);
        VideoView.IsVisible = true;
        PlayButton.IsVisible = false;
        StillImage.IsVisible = false;

        _player.Play();
    }

    private void StopPreview()
    {
        VideoView.Attach(null, null, null, ScalingMode.Fit);
        _player?.Stop();
        _player = null;
    }
}
