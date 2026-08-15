using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Wallpache.App.Support;

namespace Wallpache.App.ViewModels;

/// <summary>
/// Videos dropped on the library, held for review before anything is copied in.
///
/// Nothing enters storage until the user confirms, so an accidental drop costs
/// no disk space. Inspection and importing both run as their own tasks, which
/// keeps the rest of the app usable while a batch is on its way in.
/// </summary>
public sealed class ImportQueueViewModel : ViewModelBase
{
    private readonly Func<string, Task> _importVideo;
    private Task? _importTask;

    public ImportQueueViewModel(Func<string, Task> importVideo)
    {
        _importVideo = importVideo;
        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<ImportItemViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool IsImporting => ImportedCount < ImportTotal;

    public bool CanImport => !IsImporting && Items.Any(item => item.IsReady);

    public string Title => IsImporting
        ? $"Importing {Math.Min(ImportedCount + 1, ImportTotal)} of {ImportTotal}…"
        : ReadyCount switch
        {
            0 => "Nothing ready to import",
            1 => "1 video ready to import",
            var count => $"{count} videos ready to import"
        };

    public string ImportTitle => ReadyCount == 1 ? "Import 1 Video" : $"Import {ReadyCount} Videos";

    public double ImportProgress => ImportTotal == 0 ? 0 : (double)ImportedCount / ImportTotal * 100;

    private int ReadyCount => Items.Count(item => item.IsReady);

    private int ImportedCount { get; set; }

    private int ImportTotal { get; set; }

    /// <summary>
    /// Adds dropped files, skipping ones already waiting. Each is inspected on
    /// its own task so a slow file does not hold up the previews behind it.
    /// </summary>
    public void Stage(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (Items.Any(item => string.Equals(item.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new ImportItemViewModel(path);
            item.PropertyChanged += (_, _) => RaiseSummary();
            Items.Add(item);
            _ = item.InspectAsync();
        }
    }

    public void Remove(ImportItemViewModel item)
    {
        if (!item.IsImporting)
        {
            Items.Remove(item);
        }
    }

    /// <summary>
    /// Clears everything the user can still take back. An item already being
    /// copied is left to finish.
    /// </summary>
    public void RemoveAll()
    {
        foreach (var item in Items.Where(item => !item.IsImporting).ToList())
        {
            Items.Remove(item);
        }
    }

    /// <summary>
    /// Imports every ready item, one at a time so a large batch cannot saturate
    /// the machine. The task is not awaited by the caller: the tray reports its
    /// own progress and the app stays free for anything else.
    /// </summary>
    public void ImportReadyItems()
    {
        if (_importTask is not null)
        {
            return;
        }

        var queued = Items.Where(item => item.IsReady).ToList();
        if (queued.Count == 0)
        {
            return;
        }

        ImportedCount = 0;
        ImportTotal = queued.Count;
        RaiseSummary();

        _importTask = RunImportAsync(queued);
    }

    // MARK: - Private

    private async Task RunImportAsync(IReadOnlyList<ImportItemViewModel> queued)
    {
        foreach (var item in queued)
        {
            await ImportQueuedAsync(item);
            ImportedCount++;
            RaiseSummary();
        }

        foreach (var item in Items.Where(item => item.IsImported).ToList())
        {
            Items.Remove(item);
        }

        ImportedCount = 0;
        ImportTotal = 0;
        _importTask = null;
        RaiseSummary();
    }

    /// <summary>A file removed from the tray while it waited is simply skipped.</summary>
    private async Task ImportQueuedAsync(ImportItemViewModel item)
    {
        if (!Items.Contains(item))
        {
            return;
        }

        item.MarkImporting();

        try
        {
            await _importVideo(item.SourcePath);
            item.MarkImported();
        }
        catch (Exception error)
        {
            Log.Library.Error($"Import failed: {error.Message}");
            item.MarkFailed(WallpaperException.Describe(error));
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) => RaiseSummary();

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsImporting));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ImportTitle));
        OnPropertyChanged(nameof(ImportProgress));
    }
}
