using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Platform;
using Nelderim.Model;
using Nelderim.Utility;

namespace NelderimLauncher;

public class Utils
{
    public static Stream GetAsset(string name)
    {
        return AvaloniaLocator.Current.GetService<IAssetLoader>()
            .Open(new Uri($"avares://NelderimLauncherOld/Assets/{name}"));
    }

    public static string AppName()
    {
        var app = Process.GetCurrentProcess().ProcessName;
        if (!app.EndsWith(".exe")) app += ".exe";
        return app;
    }

    public static Patch FetchPatch()
    {
        var patchUrl = Config.Get(Config.Key.PatchUrl);
        var patchJson = Http.HttpClient.GetAsync($"{patchUrl}/Nelderim.json").Result.Content.ReadAsStream();
        var patches = JsonSerializer.Deserialize<List<Patch>>(patchJson);

        return patches.First();
    }
}