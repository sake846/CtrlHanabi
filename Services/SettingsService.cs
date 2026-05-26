using System.IO;
using System.Text.Json;
using CtrlHanabi.Models;

namespace CtrlHanabi.Services;

public sealed class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CtrlHanabi");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

    public HanabiSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return HanabiSettings.Default;
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<HanabiSettings>(json) ?? HanabiSettings.Default;
        }
        catch
        {
            return HanabiSettings.Default;
        }
    }

    public void Save(HanabiSettings settings)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
