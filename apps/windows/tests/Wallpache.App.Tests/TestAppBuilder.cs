using Avalonia;
using Avalonia.Headless;
using Wallpache.App;

[assembly: AvaloniaTestApplication(typeof(Wallpache.Tests.TestAppBuilder))]

namespace Wallpache.Tests;

/// <summary>
/// Boots Avalonia against the headless platform so views can be loaded and
/// constructed without a display server.
///
/// <see cref="App"/> itself is not used: its
/// <c>OnFrameworkInitializationCompleted</c> builds the coordinator, which talks
/// to Win32 on the first line. The tests load the app's styles and resources
/// directly instead, which is what the view markup actually depends on.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
