using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Wallpache.App.Desktop;
using Wallpache.App.Displays;
using Wallpache.App.Library;
using Wallpache.App.Persistence;
using Wallpache.App.Playback;
using Wallpache.App.Support;
using Wallpache.App.System;

namespace Wallpache.App.Core;

/// <summary>
/// Central orchestrator. It owns the persisted configuration and keeps the live
/// sessions in sync with it.
///
/// Every mutation follows the same shape: change <c>_configuration</c>, persist,
/// then call <see cref="SynchronizeSessions"/>. Because reconciliation is the
/// only code that creates or destroys sessions, applying the same wallpaper
/// twice can never produce duplicate windows, and a display that disappears
/// cannot leave a stale player behind.
/// </summary>
public sealed partial class WallpaperCoordinator : ObservableObject, IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly WallpaperLibraryService _libraryService;
    private readonly DisplayManager _displayManager;
    private readonly DesktopHostService _desktopHost;
    private readonly ExplorerMonitor _explorerMonitor;
    private readonly PowerAndSessionMonitor _systemMonitor;
    private readonly StartupService _startupService;
    private readonly DesktopPictureService _desktopPictureService;
    private readonly PlaybackPolicyController _policy;

    private readonly Dictionary<string, WallpaperSession> _sessions = [];
    private readonly Dictionary<string, AppliedDesktopPicture> _appliedDesktopPictures = [];

    private AppConfiguration _configuration;
    private bool _hasStarted;

    /// <summary>
    /// What was last handed to the system per display, so reconciliation can
    /// skip redundant desktop picture updates.
    /// </summary>
    private readonly record struct AppliedDesktopPicture(string Path, ScalingMode ScalingMode);

    public WallpaperCoordinator(
        SettingsStore settingsStore,
        WallpaperLibraryService libraryService,
        DisplayManager displayManager,
        DesktopHostService desktopHost,
        ExplorerMonitor explorerMonitor,
        PowerAndSessionMonitor systemMonitor,
        StartupService startupService,
        DesktopPictureService desktopPictureService)
    {
        _settingsStore = settingsStore;
        _libraryService = libraryService;
        _displayManager = displayManager;
        _desktopHost = desktopHost;
        _explorerMonitor = explorerMonitor;
        _systemMonitor = systemMonitor;
        _startupService = startupService;
        _desktopPictureService = desktopPictureService;
        _policy = new PlaybackPolicyController(systemMonitor);
        _configuration = settingsStore.Load();
    }

    // MARK: - Observable state

    public event Action? LibraryChanged;

    public event Action? DisplaysChanged;

    [ObservableProperty]
    public partial IReadOnlyList<WallpaperRecord> Library { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<DisplayDescriptor> Displays { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsWallpaperEnabled { get; private set; }

    [ObservableProperty]
    public partial bool IsUserPaused { get; private set; }

    [ObservableProperty]
    public partial bool IsImporting { get; private set; }

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "No wallpaper running";

    [ObservableProperty]
    public partial StartupService.State StartupState { get; private set; } = StartupService.State.Disabled;

    /// <summary>Set to surface a message; the UI clears it once shown.</summary>
    [ObservableProperty]
    public partial string? AlertMessage { get; set; }

    public WallpaperStorage Storage => _libraryService.Storage;

    public EnergyPreferences EnergyPreferences => _configuration.EnergyPreferences;

    // MARK: - Lifecycle

    /// <summary>
    /// Restores the last configuration and starts observing the system. Safe to
    /// call once; later calls are ignored.
    /// </summary>
    public void Start()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;

        Library = _configuration.Library.ToList();
        _policy.Preferences = _configuration.EnergyPreferences;

        _startupService.RepairIfMoved();
        StartupState = _startupService.CurrentState;

        ConnectMonitors();
        _displayManager.Start();
        _systemMonitor.Start();
        _explorerMonitor.Start();
        _desktopHost.Discover();

        Displays = _displayManager.Displays;
        PruneMissingWallpapers();

        // Clears files left behind by a crash between copying and saving.
        _libraryService.RemoveOrphanedFiles(_configuration.Library);

        _policy.IsWallpaperEnabled = _configuration.IsWallpaperEnabled;
        IsWallpaperEnabled = _configuration.IsWallpaperEnabled;

        SynchronizeSessions();
        LibraryChanged?.Invoke();
        DisplaysChanged?.Invoke();

        _ = BackfillStillsAsync();
        Log.Lifecycle.Info($"Coordinator started with {Library.Count} wallpapers");
    }

    /// <summary>
    /// Stops playback and observation. The wallpaper windows close, so the
    /// normal Windows wallpaper is visible again.
    /// </summary>
    public void Shutdown()
    {
        foreach (var session in _sessions.Values)
        {
            session.Stop();
        }

        _sessions.Clear();
        DesktopHostService.RefreshDesktop();

        _explorerMonitor.Stop();
        _displayManager.Stop();
        _systemMonitor.Stop();
        Persist();
    }

    public void Dispose() => Shutdown();

    // MARK: - Import

    /// <summary>Imports files, e.g. from the file picker or a drag and drop.</summary>
    public async Task ImportAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        IsImporting = true;
        var failures = new List<string>();

        try
        {
            foreach (var path in paths)
            {
                try
                {
                    var record = await _libraryService.ImportAsync(path, _configuration.Library);
                    if (_configuration.Library.All(existing => existing.Id != record.Id))
                    {
                        _configuration.Library.Add(record);
                    }
                }
                catch (Exception error)
                {
                    failures.Add(WallpaperException.Describe(error));
                    Log.Library.Error($"Import failed: {error.Message}");
                }
            }
        }
        finally
        {
            IsImporting = false;
        }

        Library = _configuration.Library.ToList();
        Persist();
        LibraryChanged?.Invoke();

        if (failures.Count > 0)
        {
            AlertMessage = string.Join(Environment.NewLine, failures);
        }
    }

    /// <summary>
    /// Renames one library entry. Only the display name changes: stored files
    /// are named by GUID, so nothing moves on disk and no session is disturbed.
    /// An empty or unchanged name is ignored rather than rejected with an error.
    /// </summary>
    public void Rename(WallpaperRecord record, string newName)
    {
        var trimmed = newName.Trim();
        var stored = _configuration.Library.FirstOrDefault(item => item.Id == record.Id);

        if (trimmed.Length == 0 || stored is null || trimmed == stored.Name)
        {
            return;
        }

        stored.Name = trimmed;
        Library = _configuration.Library.ToList();
        Persist();
        LibraryChanged?.Invoke();
        Log.Library.Info("Renamed a wallpaper");
    }

    public void Delete(WallpaperRecord record)
    {
        _configuration.Library.RemoveAll(item => item.Id == record.Id);
        _configuration.RemoveAssignments(record.Id);
        _libraryService.Delete(record);

        Library = _configuration.Library.ToList();
        Persist();
        SynchronizeSessions();
        LibraryChanged?.Invoke();
    }

    /// <summary>Removes every imported video and stops all wallpapers.</summary>
    public void DeleteAllImportedFiles()
    {
        foreach (var record in _configuration.Library)
        {
            _libraryService.Delete(record);
        }

        _configuration.Library.Clear();
        _configuration.DisplayConfigurations.Clear();
        _configuration.IsWallpaperEnabled = false;

        Library = [];
        Persist();
        SetWallpaperEnabled(false);
        LibraryChanged?.Invoke();
    }

    public void RevealStorageInExplorer()
    {
        try
        {
            Storage.PrepareDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{Storage.VideosDirectory}\"",
                UseShellExecute = true
            });
        }
        catch (Exception error)
        {
            Log.Library.Error($"Could not open the storage folder: {error.Message}");
            AlertMessage = $"Wallpache could not open its storage folder. {error.Message}";
        }
    }

    // MARK: - Applying wallpapers

    public void Apply(WallpaperRecord record, string displayId)
    {
        var configuration = DisplayConfigurationFor(displayId);
        configuration.WallpaperId = record.Id;
        _configuration.SetConfiguration(configuration);

        ActivateAndSynchronize();
    }

    public void ApplyToAllDisplays(WallpaperRecord record)
    {
        foreach (var display in Displays)
        {
            var configuration = DisplayConfigurationFor(display.Id);
            configuration.WallpaperId = record.Id;
            _configuration.SetConfiguration(configuration);
        }

        ActivateAndSynchronize();
    }

    /// <summary>Removes the wallpaper from one display without touching the others.</summary>
    public void ClearWallpaper(string displayId)
    {
        var configuration = _configuration.Configuration(displayId);
        if (configuration is null)
        {
            return;
        }

        configuration.WallpaperId = null;
        _configuration.SetConfiguration(configuration);

        // Nothing left to show anywhere means the wallpaper is simply off.
        if (!_configuration.DisplayConfigurations.Any(item => item.WallpaperId is not null))
        {
            _configuration.IsWallpaperEnabled = false;
        }

        Persist();
        SyncEnabledState();
        SynchronizeSessions();
        DisplaysChanged?.Invoke();
    }

    public void StopAll()
    {
        _configuration.IsWallpaperEnabled = false;
        Persist();
        SetWallpaperEnabled(false);
    }

    public void SetPaused(bool paused)
    {
        IsUserPaused = paused;
        _policy.IsUserPaused = paused;
        UpdateStatus();
    }

    // MARK: - Per-display settings

    public void SetScalingMode(ScalingMode mode, string displayId) =>
        UpdateDisplayConfiguration(displayId, configuration => configuration.ScalingMode = mode);

    public void SetPlaybackRate(double rate, string displayId) =>
        UpdateDisplayConfiguration(displayId, configuration => configuration.PlaybackRate = rate);

    public void SetMuted(bool isMuted, string displayId) =>
        UpdateDisplayConfiguration(displayId, configuration => configuration.IsMuted = isMuted);

    /// <summary>
    /// Mirrors the wallpaper's still frame onto the Windows desktop picture, so
    /// the display keeps a matching background when the app is not running.
    /// </summary>
    public void SetDesktopPicture(bool enabled, string displayId) =>
        UpdateDisplayConfiguration(displayId, configuration => configuration.SetsDesktopPicture = enabled);

    /// <summary>Always returns a configuration; an unassigned display gets the defaults.</summary>
    public DisplayWallpaperConfiguration DisplayConfigurationFor(string displayId) =>
        _configuration.Configuration(displayId)?.Copy() ?? new DisplayWallpaperConfiguration(displayId);

    public WallpaperRecord? Wallpaper(Guid? id) => _configuration.Wallpaper(id);

    // MARK: - Energy preferences

    public void SetEnergyPreferences(Action<EnergyPreferences> mutate)
    {
        var updated = _configuration.EnergyPreferences.Copy();
        mutate(updated);

        if (_configuration.EnergyPreferences.Matches(updated))
        {
            return;
        }

        _configuration.EnergyPreferences = updated;
        _policy.Preferences = updated;
        Persist();
        OnPropertyChanged(nameof(EnergyPreferences));
        UpdatePlayback();
    }

    // MARK: - Startup

    public void SetLaunchAtSignIn(bool enabled)
    {
        StartupState = _startupService.SetEnabled(enabled);
        _configuration.LaunchAtSignIn = StartupState == StartupService.State.Enabled;
        Persist();
    }

    // MARK: - Session reconciliation

    /// <summary>Makes the running sessions match the configuration exactly.</summary>
    private void SynchronizeSessions()
    {
        var displaysById = Displays
            .GroupBy(display => display.Id)
            .ToDictionary(group => group.Key, group => group.First());

        // 1. Displays that went away, or a fully disabled wallpaper.
        foreach (var displayId in _sessions.Keys.ToList())
        {
            if (displaysById.ContainsKey(displayId) && _configuration.IsWallpaperEnabled)
            {
                continue;
            }

            _sessions[displayId].Stop();
            _sessions.Remove(displayId);
        }

        if (_configuration.IsWallpaperEnabled)
        {
            foreach (var (displayId, display) in displaysById)
            {
                SynchronizeSession(displayId, display);
            }
        }
        else
        {
            DesktopHostService.RefreshDesktop();
        }

        // Deliberately outside the enabled check: the still image is what the
        // user sees once playback stops, so it must survive a stopped wallpaper.
        SynchronizeDesktopPictures(displaysById);

        _explorerMonitor.HasActiveWallpapers = _sessions.Count > 0;
        UpdatePlayback();
    }

    private void SynchronizeSession(string displayId, DisplayDescriptor display)
    {
        var displayConfig = DisplayConfigurationFor(displayId);
        var record = _configuration.Wallpaper(displayConfig.WallpaperId);

        if (record is null || !_libraryService.FileExists(record))
        {
            // No assignment, or the file vanished: fall back to the plain
            // Windows wallpaper for this display only.
            if (_sessions.Remove(displayId, out var orphaned))
            {
                orphaned.Stop();
            }

            return;
        }

        var videoPath = Storage.VideoPath(record);

        if (_sessions.TryGetValue(displayId, out var session))
        {
            session.MoveTo(display);

            if (session.WallpaperId != record.Id)
            {
                session.ReplaceVideo(record.Id, videoPath, record.Width, record.Height);
            }

            session.Update(displayConfig, record.Width, record.Height);
            return;
        }

        var created = new WallpaperSession(_desktopHost, display, record, videoPath, displayConfig);
        created.PlaybackFailed += HandlePlaybackFailure;

        if (!created.IsUsable)
        {
            created.Stop();
            Log.Playback.Error($"Could not create a wallpaper window on {display.Name}");
            return;
        }

        _sessions[displayId] = created;
        Log.Playback.Info($"Started session on {display.Name}");
    }

    // MARK: - Desktop picture

    /// <summary>
    /// Makes each display's system wallpaper match its assigned still frame, and
    /// puts the user's own picture back once nothing of ours is assigned there
    /// any more.
    /// </summary>
    private void SynchronizeDesktopPictures(IReadOnlyDictionary<string, DisplayDescriptor> displaysById)
    {
        foreach (var displayId in displaysById.Keys)
        {
            var displayConfig = DisplayConfigurationFor(displayId);
            var record = _configuration.Wallpaper(displayConfig.WallpaperId);

            // The full-resolution still, not the grid thumbnail: this image is
            // about to fill a whole display.
            var stillPath = record is null ? null : Storage.PreviewImagePath(record);

            // No wallpaper assigned at all: nothing of ours belongs on the
            // desktop, so bring the user's own picture back.
            if (stillPath is null)
            {
                _ = RestoreDesktopPictureAsync(displayId);
                continue;
            }

            // Turning the option off only stops further updates. It behaves like
            // the Explorer "Set as desktop background" action: a one-time change
            // that later actions do not quietly undo, rather than a toggle that
            // keeps the desktop picture in lockstep going forward.
            if (!displayConfig.SetsDesktopPicture)
            {
                continue;
            }

            // Re-setting the same picture on every reconciliation would hit the
            // system on each wake and display change for no reason.
            var desired = new AppliedDesktopPicture(stillPath, displayConfig.ScalingMode);
            if (_appliedDesktopPictures.TryGetValue(displayId, out var applied) && applied == desired)
            {
                continue;
            }

            // Recorded before the (slow, background) call finishes, so a second
            // reconciliation triggered while it is still in flight does not
            // queue a duplicate call for the same picture.
            _appliedDesktopPictures[displayId] = desired;
            _ = ApplyDesktopPictureAsync(displayId, stillPath, displayConfig.ScalingMode);
        }
    }

    /// <summary>
    /// Remembers the user's picture, then applies the still. Runs the actual
    /// system calls on a background thread (see <see cref="DesktopPictureService"/>)
    /// and resumes here on the UI thread once each finishes, the same shape as
    /// <see cref="ImportAsync"/> and <see cref="BackfillStillsAsync"/> below.
    /// </summary>
    private async Task ApplyDesktopPictureAsync(string displayId, string stillPath, ScalingMode scalingMode)
    {
        await RememberCurrentPictureAsync(displayId);

        if (await _desktopPictureService.SetPictureAsync(displayId, stillPath, scalingMode))
        {
            Log.Desktop.Info("Desktop picture set");
        }
        else
        {
            // Nothing was actually applied, so let the next reconciliation try
            // again instead of believing it already matches.
            _appliedDesktopPictures.Remove(displayId);
        }
    }

    /// <summary>
    /// Stores the wallpaper the user had, once per display, so it can be put
    /// back later. Our own stills are never recorded as the previous picture.
    /// </summary>
    private async Task RememberCurrentPictureAsync(string displayId)
    {
        if (_configuration.PreviousDesktopPictures.ContainsKey(displayId))
        {
            return;
        }

        var current = await _desktopPictureService.CurrentPictureAsync(displayId);
        if (current is null || current.StartsWith(Storage.Root, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _configuration.PreviousDesktopPictures[displayId] = current;
        Persist();
    }

    private async Task RestoreDesktopPictureAsync(string displayId)
    {
        _appliedDesktopPictures.Remove(displayId);

        if (!_configuration.PreviousDesktopPictures.Remove(displayId, out var stored))
        {
            return;
        }

        Persist();

        // A picture that has since been deleted is simply left alone rather than
        // replaced with an error.
        if (!File.Exists(stored))
        {
            return;
        }

        if (await _desktopPictureService.SetPictureAsync(displayId, stored, ScalingMode.Fill))
        {
            Log.Desktop.Info("Desktop picture restored");
        }
    }

    private void UpdatePlayback()
    {
        var shouldPlay = _policy.ShouldPlay;

        foreach (var session in _sessions.Values)
        {
            if (shouldPlay)
            {
                session.Play();
            }
            else
            {
                session.Pause();
            }
        }

        UpdateStatus();
    }

    /// <summary>
    /// Re-asserts window placement and repairs players. Runs after wake, after
    /// an Explorer restart, and after display reconfiguration, where any of the
    /// three can be disturbed.
    /// </summary>
    private void RecoverSessions()
    {
        // Wallpaper windows are children of an Explorer window, so an Explorer
        // restart destroys them outright. Those sessions cannot be re-attached;
        // they have to be dropped so reconciliation builds them again.
        var destroyed = _sessions.Where(pair => !pair.Value.IsUsable).Select(pair => pair.Key).ToList();
        foreach (var displayId in destroyed)
        {
            _sessions[displayId].Stop();
            _sessions.Remove(displayId);
        }

        if (destroyed.Count > 0)
        {
            Log.Desktop.Info($"Rebuilding {destroyed.Count} wallpaper windows after the desktop host changed");
            SynchronizeSessions();
        }

        var shouldPlay = _policy.ShouldPlay;
        foreach (var session in _sessions.Values)
        {
            session.Recover(shouldPlay);
        }
    }

    // MARK: - System events

    private void ConnectMonitors()
    {
        _displayManager.DisplaysChanged += updated =>
        {
            Displays = updated;
            SynchronizeSessions();
            RecoverSessions();
            DisplaysChanged?.Invoke();
        };

        _systemMonitor.DisplaysMayHaveChanged += () => _displayManager.ScheduleReconcile();

        _systemMonitor.WillSleep += () => _policy.IsSystemAsleep = true;

        _systemMonitor.DidWake += () =>
        {
            _policy.IsSystemAsleep = false;

            // Displays and the desktop host are revalidated first: waking on a
            // dock can change the display set, and Explorer may have rebuilt the
            // desktop while the machine was asleep.
            _displayManager.Refresh();
            Displays = _displayManager.Displays;
            _desktopHost.Rediscover();
            SynchronizeSessions();
            RecoverSessions();
            DisplaysChanged?.Invoke();
        };

        _systemMonitor.SessionLockChanged += isLocked => _policy.IsScreenLocked = isLocked;

        _systemMonitor.PowerConditionsChanged += () => _policy.Reevaluate();

        _explorerMonitor.DesktopHostInvalidated += () =>
        {
            _desktopHost.Rediscover();
            RecoverSessions();
        };

        _policy.DecisionChanged += _ => UpdatePlayback();
    }

    private void HandlePlaybackFailure(string displayId, Exception? error)
    {
        var record = _configuration.Wallpaper(DisplayConfigurationFor(displayId).WallpaperId);
        Log.Playback.Error($"Unrecoverable playback failure on display {displayId}");

        if (_sessions.Remove(displayId, out var session))
        {
            session.Stop();
        }

        ClearWallpaper(displayId);

        var name = record?.Name ?? "This wallpaper";
        AlertMessage = $"{name} stopped playing and was removed from that display. {error?.Message}".Trim();
    }

    // MARK: - Helpers

    private void ActivateAndSynchronize()
    {
        _configuration.IsWallpaperEnabled = true;
        IsUserPaused = false;
        _policy.IsUserPaused = false;
        Persist();
        SyncEnabledState();
        SynchronizeSessions();
        DisplaysChanged?.Invoke();
    }

    private void SetWallpaperEnabled(bool enabled)
    {
        _configuration.IsWallpaperEnabled = enabled;
        SyncEnabledState();
        SynchronizeSessions();
        DisplaysChanged?.Invoke();
    }

    private void SyncEnabledState()
    {
        IsWallpaperEnabled = _configuration.IsWallpaperEnabled;
        _policy.IsWallpaperEnabled = _configuration.IsWallpaperEnabled;
    }

    private void UpdateDisplayConfiguration(string displayId, Action<DisplayWallpaperConfiguration> mutate)
    {
        var configuration = DisplayConfigurationFor(displayId);
        mutate(configuration);
        configuration.Normalize();
        _configuration.SetConfiguration(configuration);
        Persist();
        SynchronizeSessions();
        DisplaysChanged?.Invoke();
    }

    /// <summary>Drops library entries whose file disappeared while the app was closed.</summary>
    private void PruneMissingWallpapers()
    {
        var missing = _configuration.Library.Where(record => !_libraryService.FileExists(record)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var record in missing)
        {
            _configuration.Library.RemoveAll(item => item.Id == record.Id);
            _configuration.RemoveAssignments(record.Id);
        }

        Library = _configuration.Library.ToList();
        Persist();
        Log.Library.Info($"Removed {missing.Count} missing wallpapers");

        AlertMessage = missing.Count == 1
            ? $"“{missing[0].Name}” is no longer available and was removed from your library."
            : $"{missing.Count} wallpapers are no longer available and were removed from your library.";
    }

    /// <summary>
    /// Generates full-resolution stills for entries imported before stills
    /// existed. Runs after launch rather than during it: it decodes a frame per
    /// wallpaper, which is far too slow to block startup on.
    /// </summary>
    private async Task BackfillStillsAsync()
    {
        var pending = _configuration.Library.Where(record => record.StillRelativePath is null).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var updated = await _libraryService.BackfillStillsAsync(pending);
        if (updated.Count == 0)
        {
            return;
        }

        Library = _configuration.Library.ToList();
        Persist();

        // The desktop picture may have been set from the low-resolution
        // thumbnail before the still existed, so let it be re-applied.
        _appliedDesktopPictures.Clear();
        SynchronizeSessions();
        LibraryChanged?.Invoke();
        Log.Library.Info($"Backfilled {updated.Count} stills");
    }

    private void UpdateStatus()
    {
        var reason = _policy.Reason;
        if (reason != SuspensionReason.None)
        {
            StatusMessage = reason.Description();
            return;
        }

        StatusMessage = _sessions.Count switch
        {
            0 => "No wallpaper running",
            1 => "Playing on 1 display",
            var count => $"Playing on {count} displays"
        };
    }

    private void Persist() => _settingsStore.Save(_configuration);
}
