using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using NelderimLauncher.Models;

namespace NelderimLauncher.Utility;

public class Updater
{
    private static readonly HttpClient HttpClient = new();

    public static string AppName()
    {
        var nelderimApp = Process.GetCurrentProcess().ProcessName;
        if (!nelderimApp.EndsWith(".exe")) nelderimApp += ".exe";
        return nelderimApp;
    }
    
    public static bool shouldSelfUpdate()
    {
        var patch = FetchPatch();

       
        using (FileStream stream = File.OpenRead(AppName()))
        {
            return Utils.Sha1Hash(stream) != patch.Sha1;
        }
    }

    public static Patch? FetchPatch()
    {
        var patchUrl = Config.Get(Config.Key.PatchUrl);
        var patchJson = HttpClient.GetAsync($"{patchUrl}/Nelderim.json").Result.Content.ReadAsStream();
        var patches = JsonSerializer.Deserialize<List<Patch>>(patchJson);
        
        return patches.First();
    }
}