using System.IO;
using System.Text.Json;
using VoiceType.Models;

namespace VoiceType.Services;

public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceType");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    public bool IsFirstRun { get; private set; }

    public void Load()
    {
        if (!File.Exists(SettingsPath))
        {
            Settings = new AppSettings();
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
                         ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            IsFirstRun = string.IsNullOrWhiteSpace(apiKey);
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            IsFirstRun = false;
        }
        catch
        {
            Settings = new AppSettings();
            IsFirstRun = true;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
