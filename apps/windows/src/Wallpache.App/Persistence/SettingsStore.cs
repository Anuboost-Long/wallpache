using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wallpache.App.Support;

namespace Wallpache.App.Persistence;

/// <summary>
/// Loads and saves <see cref="AppConfiguration"/> as JSON under Local
/// Application Data.
///
/// Writes go through a temporary file and a replace, so a power cut in the
/// middle of a save cannot leave a half-written settings file. A damaged file
/// is discarded rather than allowed to block launch.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Returns an empty configuration when nothing is stored yet or when the
    /// stored payload cannot be decoded, so a corrupt file never blocks launch.
    /// </summary>
    public AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppConfiguration();
            }

            var json = File.ReadAllText(_path);
            var configuration = JsonSerializer.Deserialize<AppConfiguration>(json, Options) ?? new AppConfiguration();
            configuration.Normalize();
            return configuration;
        }
        catch (Exception error)
        {
            Log.Lifecycle.Error($"Discarding unreadable configuration: {error.Message}");
            TryQuarantine();
            return new AppConfiguration();
        }
    }

    public void Save(AppConfiguration configuration)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(configuration, Options));

            if (File.Exists(_path))
            {
                File.Replace(temporary, _path, null);
            }
            else
            {
                File.Move(temporary, _path);
            }
        }
        catch (Exception error)
        {
            Log.Lifecycle.Error($"Failed to save configuration: {error.Message}");
        }
    }

    /// <summary>Keeps the broken file for diagnosis instead of overwriting it silently.</summary>
    private void TryQuarantine()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Move(_path, _path + ".corrupt", overwrite: true);
            }
        }
        catch (Exception)
        {
            // Nothing further to do; a fresh configuration is written on the next save.
        }
    }
}
