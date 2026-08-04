using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Wallpache.App.Support;

/// <summary>
/// Centralised categories so subsystem strings are declared once, mirroring the
/// macOS <c>Log</c> enum. Lines go to the debugger and to a size-capped file
/// under the app's local data folder, which is what makes an eight-hour run
/// diagnosable after the fact.
/// </summary>
public static class Log
{
    public static readonly LogCategory Lifecycle = new("lifecycle");
    public static readonly LogCategory Playback = new("playback");
    public static readonly LogCategory Displays = new("displays");
    public static readonly LogCategory Library = new("library");
    public static readonly LogCategory Policy = new("policy");
    public static readonly LogCategory Desktop = new("desktop");

    private const long MaximumFileSize = 2 * 1024 * 1024;

    private static readonly object Gate = new();
    private static string? _logFilePath;
    private static bool _fileLoggingFailed;

    /// <summary>Points file logging at <paramref name="logsDirectory"/>.</summary>
    public static void Configure(string logsDirectory)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(logsDirectory);
                _logFilePath = Path.Combine(logsDirectory, "wallpache.log");
                _fileLoggingFailed = false;
            }
            catch (Exception)
            {
                // Logging must never be the reason the app fails to start.
                _fileLoggingFailed = true;
            }
        }
    }

    internal static void Write(string category, string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{category}] {message}";
        Debug.WriteLine(line);

        lock (Gate)
        {
            if (_logFilePath is null || _fileLoggingFailed)
            {
                return;
            }

            try
            {
                RollIfNeeded(_logFilePath);
                File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                _fileLoggingFailed = true;
            }
        }
    }

    /// <summary>Keeps one previous file so a long session cannot fill the disk.</summary>
    private static void RollIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaximumFileSize)
        {
            return;
        }

        var previous = path + ".1";
        File.Delete(previous);
        File.Move(path, previous);
    }
}

public sealed class LogCategory
{
    private readonly string _name;

    internal LogCategory(string name) => _name = name;

    public void Info(string message) => Log.Write(_name, "info", message);

    public void Error(string message) => Log.Write(_name, "error", message);
}
