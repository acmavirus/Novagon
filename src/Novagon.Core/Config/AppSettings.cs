using System;
using System.IO;
using System.Text.Json;

namespace Novagon.Core.Config;

public class AppSettings
{
    private static readonly string SettingsFile = Path.Combine(AppDirectoryHelper.AppRoot, "etc", "settings.json");

    public string Language { get; set; } = "vi";
    public string DefaultTld { get; set; } = "test";
    public bool AutoStartServices { get; set; } = false;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    if (string.IsNullOrWhiteSpace(settings.Language)) settings.Language = "vi";
                    if (string.IsNullOrWhiteSpace(settings.DefaultTld)) settings.DefaultTld = "test";
                    return settings;
                }
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }
}
