using Wallpache.App.ViewModels;
using Xunit;

namespace Wallpache.Tests;

/// <summary>
/// The staging tray: what a dropped file becomes before the user confirms.
///
/// Inspecting a real video decodes it through WinRT, which only exists on
/// Windows, so what runs on any host is the bookkeeping around it — rejection,
/// de-duplication, removal, and the summary the tray shows. The checklist covers
/// a staged import end to end on real hardware.
/// </summary>
public class ImportQueueViewModelTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "WallpacheTests", Guid.NewGuid().ToString("N"));

    private readonly List<string> _imported = [];

    public ImportQueueViewModelTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private ImportQueueViewModel MakeQueue() => new(path =>
    {
        _imported.Add(path);
        return Task.CompletedTask;
    });

    private string MakeFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "not really a video");
        return path;
    }

    [Fact]
    public async Task UnsupportedFilesAreRejectedWithAReason()
    {
        var queue = MakeQueue();
        queue.Stage([MakeFile("Holiday clip.txt")]);

        await Task.Yield();

        var item = Assert.Single(queue.Items);
        Assert.True(item.HasProblem);
        Assert.Contains("cannot be played", item.Subtitle);
        Assert.False(queue.CanImport);
    }

    [Fact]
    public void TheSameFileIsNotStagedTwice()
    {
        var queue = MakeQueue();
        var path = MakeFile("Clip.txt");

        queue.Stage([path, path]);
        queue.Stage([path]);

        Assert.Single(queue.Items);
    }

    [Fact]
    public void NothingIsImportedWhileEverythingStagedIsUnusable()
    {
        var queue = MakeQueue();
        queue.Stage([MakeFile("Clip.txt")]);

        queue.ImportReadyItems();

        Assert.Empty(_imported);
        Assert.False(queue.IsImporting);
        Assert.Equal("Nothing ready to import", queue.Title);
    }

    [Fact]
    public void RemovingAStagedFileTakesItOutOfTheTray()
    {
        var queue = MakeQueue();
        queue.Stage([MakeFile("First.txt"), MakeFile("Second.txt")]);

        queue.Remove(queue.Items[0]);

        Assert.Single(queue.Items);
        Assert.True(queue.HasItems);

        queue.RemoveAll();

        Assert.Empty(queue.Items);
        Assert.False(queue.HasItems);
    }
}
