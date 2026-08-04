using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace Wallpache.Tests;

/// <summary>
/// A minimal application that carries the same theme and brand resources as the
/// real one, so a view that resolves <c>BrandWaveBrush</c> or a Fluent theme key
/// behaves in tests exactly as it does at runtime.
/// </summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        Resources.MergedDictionaries.Add(
            new ResourceInclude((Uri?)null) { Source = new Uri("avares://Wallpache/UI/Brand.axaml") });
    }
}
