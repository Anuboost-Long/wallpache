using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wallpache.App.Core;
using Wallpache.App.Library;
using Wallpache.App.UI;

namespace Wallpache.App.ViewModels;

/// <summary>One wallpaper in the library grid.</summary>
public sealed partial class WallpaperItemViewModel : ViewModelBase
{
    private readonly WallpaperCoordinator _coordinator;

    public WallpaperItemViewModel(WallpaperCoordinator coordinator, WallpaperRecord record)
    {
        _coordinator = coordinator;
        Record = record;
    }

    public WallpaperRecord Record { get; }

    public string Name => Record.Name;

    public string Subtitle => Record.Subtitle;

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; private set; }

    /// <summary>
    /// The per-display targets offered in the cell's Display menu. Each carries
    /// its own command so the menu can be built by data binding rather than by
    /// wiring a click handler per item.
    /// </summary>
    public ObservableCollection<DisplayTargetViewModel> DisplayTargets { get; } = [];

    /// <summary>A single-display setup has nothing to choose between.</summary>
    public bool CanChooseDisplay => DisplayTargets.Count > 1;

    public async Task LoadThumbnailAsync()
    {
        if (Thumbnail is not null)
        {
            return;
        }

        Thumbnail = await ThumbnailLoader.LoadAsync(
            _coordinator.Storage.ThumbnailPath(Record),
            ThumbnailLoader.CellPixelWidth);
    }

    public void ApplyToAllDisplays() => _coordinator.ApplyToAllDisplays(Record);

    public void RefreshDisplays()
    {
        DisplayTargets.Clear();
        foreach (var display in _coordinator.Displays)
        {
            DisplayTargets.Add(new DisplayTargetViewModel(
                display.Name,
                new RelayCommand(() => _coordinator.Apply(Record, display.Id))));
        }

        OnPropertyChanged(nameof(CanChooseDisplay));
    }

    public void RaiseNameChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>One entry in a wallpaper cell's "apply to this display" menu.</summary>
public sealed class DisplayTargetViewModel
{
    public DisplayTargetViewModel(string name, ICommand applyCommand)
    {
        Name = name;
        ApplyCommand = applyCommand;
    }

    public string Name { get; }

    public ICommand ApplyCommand { get; }
}
