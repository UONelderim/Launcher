using System;
using System.Collections.Generic;
using System.IO;
using IniParser;
using IniParser.Model;

namespace NelderimLauncher.Utility;

public class Config
{
    public class Key
    {
        public static string PatchUrl => "config.patchUrl";
    }

    private static Dictionary<String, String> DefaultValues = new()
    {
        {Key.PatchUrl, "https://nelderim.pl/patch"},
    };
    
    private static FileIniDataParser parser = new();
    private static IniData? _iniData;
    private static readonly string ConfigFilePath = $"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}.ini";

    private static void Init()
    {
        if (!File.Exists(ConfigFilePath))
        {
            File.Create(ConfigFilePath).Close();
        }
        _iniData = parser.ReadFile(ConfigFilePath);
    }

    public static string Get(string key)
    {
        if(_iniData == null) Init();
        if (!_iniData.TryGetKey(key, out var result))
        {
            result = DefaultValues[key];
        }
        return result;
    }

    public static void Set(string key, string value)
    {
        if(_iniData == null) Init();
        var parts = key.Split(".");
        if (parts.Length != 2) throw new ArgumentException("Config key can have only two parts :(");
        _iniData[parts[0]][parts[1]] = value;
        parser.WriteFile(ConfigFilePath, _iniData);
    }
}