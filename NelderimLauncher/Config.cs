using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nelderim.Launcher;

public class ConfigRoot
{
    public string PatchUrl = "https://www.nelderim.pl/patch";
}

public static class Config
{
    public static ConfigRoot Instance;
    private static string _configFilePath =  "NelderimLauncher.json";

    static Config()
    {
        if (File.Exists(_configFilePath))
        {
            var jsonText = File.ReadAllText(_configFilePath);
            try
            {
                Instance = JsonSerializer.Deserialize(jsonText, ConfigJsonContext.Default.ConfigRoot);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                File.Delete(_configFilePath);
            }
        }
        if (Instance == null || !File.Exists(_configFilePath))
        {
            Instance = new ConfigRoot();
            Save();
        }
    }

    public static void Save()
    {
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(Instance, ConfigJsonContext.Default.ConfigRoot));
    }
}

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(ConfigRoot))]
internal partial class ConfigJsonContext : JsonSerializerContext;
