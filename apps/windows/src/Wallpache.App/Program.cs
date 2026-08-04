using System;
using System.Threading;
using Avalonia;

namespace Wallpache.App;

internal sealed class Program
{
    /// <summary>
    /// A second instance would create a second set of wallpaper windows and
    /// player sessions on the same desktop. The mutex is what makes "no
    /// duplicate sessions" true even when the app is launched twice.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\Wallpache.SingleInstance";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
