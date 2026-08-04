using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Wallpache.App.Support;

namespace Wallpache.App.System;

/// <summary>
/// Launch at sign-in, through the per-user Run key.
///
/// A per-user registration is what an unpackaged desktop app can do without an
/// installer or elevation, and it is removed with the user's profile rather than
/// left behind machine-wide.
/// </summary>
public sealed class StartupService
{
    public enum State
    {
        Enabled,
        Disabled,

        /// <summary>The registry could not be read or written, e.g. under policy.</summary>
        Unavailable
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wallpache";

    /// <summary>The launcher path, quoted so a space in the install path is safe.</summary>
    private static string CommandLine
    {
        get
        {
            var path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            return string.IsNullOrEmpty(path) ? string.Empty : $"\"{path}\"";
        }
    }

    public State CurrentState
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                return string.IsNullOrEmpty(value) ? State.Disabled : State.Enabled;
            }
            catch (Exception error)
            {
                Log.Lifecycle.Error($"Reading the startup entry failed: {error.Message}");
                return State.Unavailable;
            }
        }
    }

    /// <summary>
    /// Returns the resulting state. A failed write is reported rather than
    /// thrown so the UI can simply show what is actually true.
    /// </summary>
    public State SetEnabled(bool enabled)
    {
        var command = CommandLine;
        if (enabled && string.IsNullOrEmpty(command))
        {
            Log.Lifecycle.Error("Cannot register for startup: the executable path is unknown");
            return State.Unavailable;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return State.Unavailable;
            }

            if (enabled)
            {
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception error)
        {
            Log.Lifecycle.Error($"Startup entry update failed: {error.Message}");
            return State.Unavailable;
        }

        return CurrentState;
    }

    /// <summary>
    /// Repoints an existing registration at the current executable. Without it,
    /// moving or reinstalling the app leaves a startup entry aimed at a path
    /// that no longer exists.
    /// </summary>
    public void RepairIfMoved()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string stored || string.IsNullOrEmpty(stored))
            {
                return;
            }

            var command = CommandLine;
            if (string.IsNullOrEmpty(command) || string.Equals(stored, command, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var storedPath = stored.Trim('"');
            if (File.Exists(storedPath))
            {
                return;
            }

            key.SetValue(ValueName, command, RegistryValueKind.String);
            Log.Lifecycle.Info("Repaired the startup entry after the app moved");
        }
        catch (Exception error)
        {
            Log.Lifecycle.Error($"Startup entry repair failed: {error.Message}");
        }
    }
}
