using System.Text.Json;

namespace SystemAnalysis.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config dosyasi bulunamadi: {path}");
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AppConfig>(json, Options);
        var result = config ?? throw new InvalidOperationException("Config okunamadi.");
        result.Timing.Normalize();
        result.ClipboardCheck.Normalize();
        result.Normalize();
        return result;
    }

    public static void Save(string path, AppConfig config)
    {
        config.Timing.Normalize();
        config.ClipboardCheck.Normalize();
        config.Normalize();
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(path, json);
    }
}
