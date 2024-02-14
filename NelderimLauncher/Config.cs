using System.Text.Json;

namespace Nelderim.Launcher;

public class ConfigRoot
{
    public string PatchUrl = "https://www.nelderim.pl/patch";
}

public static class Config
{
    public static ConfigRoot Instance;
    private static string _configFilePath = "NelderimLauncher.json";
    
    static Config()
    {
        if (!File.Exists(_configFilePath))
        {
            var newConfig = new ConfigRoot();
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(newConfig));
        }

        var jsonText = File.ReadAllText(_configFilePath);
        Instance = JsonSerializer.Deserialize<ConfigRoot>(jsonText);
    }

    public static void Save()
    {
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(Instance));
    }
}