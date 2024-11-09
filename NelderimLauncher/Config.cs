using System.Text.Json;

namespace Nelderim.Launcher;

public class ConfigRoot
{
    public string PatchUrl = "https://www.nelderim.pl/patch";
}

public static class Config
{
    public static ConfigRoot Instance;
    private static string _configFilePath =  "NelderimLauncher.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true
    };
    
    static Config()
    {
        if (File.Exists(_configFilePath))
        {
            var jsonText = File.ReadAllText(_configFilePath);
            try
            {
                Instance = JsonSerializer.Deserialize<ConfigRoot>(jsonText, SerializerOptions);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                File.Delete(_configFilePath);
            }
        }
        if (!File.Exists(_configFilePath))
        {
            Instance = new ConfigRoot();
            Save();
        }
    }

    public static void Save()
    {
        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(Instance, SerializerOptions));
    }
}