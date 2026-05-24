using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorfulLedKeyboard.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    static SettingsStore()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public string SettingsPath { get; }

    public SettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? AppPaths.SettingsPath;
    }

    public KeyboardSettings Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        if (!File.Exists(SettingsPath))
        {
            var defaults = new KeyboardSettings().Normalize();
            TrySaveDefaults(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var migrateLegacyMode = !json.Contains("\"Effect\"", StringComparison.Ordinal);
            return (JsonSerializer.Deserialize<KeyboardSettings>(json, SerializerOptions) ?? new KeyboardSettings()).Normalize(migrateLegacyMode);
        }
        catch (JsonException)
        {
            var defaults = new KeyboardSettings().Normalize();
            TrySaveDefaults(defaults);
            return defaults;
        }
        catch (UnauthorizedAccessException)
        {
            return new KeyboardSettings().Normalize();
        }
        catch (IOException)
        {
            return new KeyboardSettings().Normalize();
        }
    }

    public void Save(KeyboardSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(settings.Normalize(), SerializerOptions);
        var tempPath = $"{SettingsPath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(SettingsPath))
        {
            File.Replace(tempPath, SettingsPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, SettingsPath);
        }
    }

    private void TrySaveDefaults(KeyboardSettings defaults)
    {
        try
        {
            Save(defaults);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }
}
