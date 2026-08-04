using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Wallpache.App.Library;
using Wallpache.App.ViewModels;

namespace Wallpache.App.Views;

/// <summary>Grid of imported wallpapers with import, apply, preview, and delete actions.</summary>
public partial class LibraryView : UserControl
{
    private static readonly FilePickerFileType VideoFileType = new("Videos")
    {
        Patterns = ["*.mp4", "*.mov", "*.m4v"],
        AppleUniformTypeIdentifiers = ["public.movie"],
        MimeTypes = ["video/mp4", "video/quicktime"]
    };

    public LibraryView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    // MARK: - Import

    private async void OnImport(object? sender, RoutedEventArgs args) => await PresentImportPickerAsync();

    /// <summary>Also called from the tray, so the picker exists in exactly one place.</summary>
    public async Task PresentImportPickerAsync()
    {
        if (Model is null || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an .mp4, .mov, or .m4v video to use as a live wallpaper.",
            AllowMultiple = true,
            FileTypeFilter = [VideoFileType]
        });

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .ToList();

        await Model.Coordinator.ImportAsync(paths);
    }

    // MARK: - Drag and drop

    private void OnDragOver(object? sender, DragEventArgs args)
    {
        var hasFiles = args.DataTransfer.Contains(DataFormat.File);
        args.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        DropHighlight.IsVisible = hasFiles;
    }

    private void OnDragLeave(object? sender, DragEventArgs args) => DropHighlight.IsVisible = false;

    private async void OnDrop(object? sender, DragEventArgs args)
    {
        DropHighlight.IsVisible = false;

        if (Model is null)
        {
            return;
        }

        var paths = new List<string>();
        foreach (var item in args.DataTransfer.TryGetFiles() ?? [])
        {
            var path = item.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        await Model.Coordinator.ImportAsync(paths);
    }

    // MARK: - Cell actions

    private void OnApply(object? sender, RoutedEventArgs args) => ItemFor(sender)?.ApplyToAllDisplays();

    private async void OnPreview(object? sender, RoutedEventArgs args)
    {
        if (ItemFor(sender) is not { } item || Model is null || Owner is null)
        {
            return;
        }

        var preview = new WallpaperPreviewWindow(Model.Coordinator, item.Record);
        await preview.ShowDialog(Owner);
    }

    private async void OnRename(object? sender, RoutedEventArgs args)
    {
        if (ItemFor(sender) is not { } item || Model is null || Owner is null)
        {
            return;
        }

        var name = await DialogWindow.PromptAsync(
            Owner,
            "Rename Wallpaper",
            "Choose a new name for this wallpaper. The video file itself is not renamed.",
            item.Record.Name,
            "Rename");

        if (name is not null)
        {
            Model.Coordinator.Rename(item.Record, name);
        }
    }

    private async void OnDelete(object? sender, RoutedEventArgs args)
    {
        if (ItemFor(sender) is not { } item || Model is null || Owner is null)
        {
            return;
        }

        var confirmed = await DialogWindow.ConfirmAsync(
            Owner,
            $"Delete “{item.Record.Name}”?",
            "The imported copy is removed from your library and deleted from disk. Any display using it falls back to the normal Windows wallpaper.",
            "Delete");

        if (confirmed)
        {
            Model.Coordinator.Delete(item.Record);
        }
    }

    /// <summary>
    /// The cell a menu item or button belongs to. Menu items inside a flyout
    /// inherit the cell's data context, so one lookup serves both.
    /// </summary>
    private static WallpaperItemViewModel? ItemFor(object? sender) =>
        (sender as Control)?.DataContext as WallpaperItemViewModel;
}
