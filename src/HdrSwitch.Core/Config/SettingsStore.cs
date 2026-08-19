using System.Text.Json;
using System.Text.Json.Serialization;

namespace HdrSwitch.Core.Config;

/// <summary>
/// Source-generated serialization context. Reflection-based System.Text.Json is disabled
/// project-wide (JsonSerializerIsReflectionEnabledByDefault=false) because it behaves
/// unpredictably under single-file publish; this keeps serialization explicit and deterministic.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
public partial class SettingsJsonContext : JsonSerializerContext;

/// <summary>Loads and saves <see cref="AppSettings"/> to disk, tolerating a corrupt file.</summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly object _writeLock = new();

    public SettingsStore(string? path = null) => _path = path ?? DefaultPath;

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HdrSwitch");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public string Path_ => _path;

    /// <summary>Set when the previous settings file could not be read and defaults were used.</summary>
    public string? LoadWarning { get; private set; }

    public AppSettings Load()
    {
        LoadWarning = null;

        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings)
                   ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Losing preferences is annoying; refusing to start is worse. Keep the bad file
            // alongside so it can be inspected rather than overwriting it silently.
            LoadWarning = $"Could not read settings ({ex.Message}). Defaults are in use.";
            TryPreserveCorruptFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_writeLock)
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);

            // Write to a sibling then move, so an interrupted save cannot truncate the real file.
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _path, overwrite: true);
        }
    }

    private void TryPreserveCorruptFile()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Move(_path, _path + ".corrupt", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing further to do; defaults are already in effect.
        }
    }
}
