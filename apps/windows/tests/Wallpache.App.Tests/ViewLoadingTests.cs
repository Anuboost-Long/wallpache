using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.LogicalTree;
using Wallpache.App.UI;
using Wallpache.App.Views;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// Loads every piece of view markup for real.
///
/// The XAML compiler catches type and compiled-binding errors, but a broken
/// <c>avares://</c> path, a missing <c>StaticResource</c> key, or a control
/// theme that fails to apply only shows up when the markup is actually
/// instantiated — which, for a wallpaper app, would be the moment the user first
/// opens the window.
/// </summary>
public class ViewLoadingTests
{
    [AvaloniaFact]
    public void BrandResourcesResolve()
    {
        Assert.True(Application.Current!.Resources.TryGetResource("BrandWaveBrush", null, out var wave));
        Assert.IsAssignableFrom<IBrush>(wave);

        Assert.True(Application.Current.Resources.TryGetResource("BrandHeaderBrush", null, out var header));
        Assert.IsAssignableFrom<IBrush>(header);

        Assert.True(Application.Current.Resources.TryGetResource("BrandWavePurpleBrush", null, out _));
        Assert.True(Application.Current.Resources.TryGetResource("BrandTagline", null, out var tagline));
        Assert.Equal("Bring your desktop to life.", tagline);
    }

    // Brand.axaml is compiled XAML rather than a loose asset, so it is not in the
    // asset index; BrandResourcesResolve is what proves its avares path works.
    [AvaloniaTheory]
    [InlineData("avares://Wallpache/Assets/brand-mark.png")]
    [InlineData("avares://Wallpache/Assets/wallpache.ico")]
    public void EmbeddedAssetsExist(string uri) =>
        Assert.True(AssetLoader.Exists(new Uri(uri)), $"{uri} is missing from the assembly");

    [AvaloniaFact]
    public void TrayIconLoadsFromTheEmbeddedIco()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Wallpache/Assets/wallpache.ico"));
        var icon = new WindowIcon(stream);

        Assert.NotNull(icon);
    }

    [AvaloniaFact]
    public void LibraryViewLoads()
    {
        var view = new LibraryView();

        Assert.NotNull(view.FindControl<Border>("DropHighlight"));
    }

    [AvaloniaFact]
    public void DisplayConfigurationViewLoads() => Assert.NotNull(new DisplayConfigurationView().Content);

    [AvaloniaFact]
    public void SettingsViewLoads() => Assert.NotNull(new SettingsView().Content);

    // The windows are loaded but never shown: laying one out would shape text,
    // and the text-shaping natives are not restored for a Windows-only target
    // framework on this build host. What is being checked here is that the
    // markup produces the object graph the code-behind expects, which is exactly
    // where a renamed control or a bad resource key would bite.

    [AvaloniaFact]
    public void MainWindowLoadsWithAllThreeTabs()
    {
        var window = new MainWindow();

        var tabs = window.GetLogicalDescendants().OfType<TabControl>().Single();
        var headers = tabs.Items.OfType<TabItem>().Select(item => item.Header).ToList();

        Assert.Equal(["Library", "Displays", "Settings"], headers);
        Assert.NotNull(window.Icon);
    }

    [AvaloniaFact]
    public void MainWindowHostsTheLibraryViewItDelegatesImportTo()
    {
        var window = new MainWindow();

        Assert.Single(window.GetLogicalDescendants().OfType<LibraryView>());
        Assert.Single(window.GetLogicalDescendants().OfType<DisplayConfigurationView>());
        Assert.Single(window.GetLogicalDescendants().OfType<SettingsView>());
    }

    [AvaloniaFact]
    public void PreviewWindowLoads()
    {
        var window = new WallpaperPreviewWindow();

        Assert.NotNull(window.FindControl<Button>("PlayButton"));
        Assert.NotNull(window.FindControl<Image>("StillImage"));
        Assert.NotNull(window.FindControl<NativeVideoView>("VideoView"));
    }

    [AvaloniaFact]
    public void DialogWindowLoads()
    {
        var window = new DialogWindow();

        Assert.NotNull(window.FindControl<TextBox>("InputBox"));
        Assert.NotNull(window.FindControl<Button>("ConfirmButton"));
        Assert.NotNull(window.FindControl<Button>("CancelButton"));
    }
}
