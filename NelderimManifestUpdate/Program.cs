using System.Diagnostics;
using System.Text.Json;

namespace Nelderim;

public class Program
{
    // Server layout: common client assets and ClassicUO package per platform (see scripts/build_package.py)
    private const string CommonPath = "Nelderim";
    private static readonly Dictionary<string, (string Path, string EntryPoint)> PlatformSections = new()
    {
        ["win"] = ("dist/win", "ClassicUO/ClassicUO.exe"),
        ["linux"] = ("dist/linux", "ClassicUO/ClassicUO"),
        ["osx"] = ("dist/osx", "ClassicUO/ClassicUO"),
    };

    public static void Main(string[] args)
    {
        var manifestPath = "Nelderim.manifest.json";
        var oldManifestPath = $"{manifestPath}.old";
        var procName = Process.GetCurrentProcess().ProcessName;

        var excludes = File.ReadAllLines($"{procName}.exclude").Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

        var currentManifest = Manifest.Empty;
        if (File.Exists(manifestPath))
        {
            using (var currentManifestStream = File.OpenRead(manifestPath))
                currentManifest = JsonSerializer.Deserialize<Manifest>(currentManifestStream)!;
            File.Move(manifestPath, oldManifestPath, true); //Just in case
        }

        var common = ProcessSection(CommonPath, "", currentManifest.Common, excludes, false);
        var platforms = new Dictionary<string, ManifestSection>();
        foreach (var (platform, (path, entryPoint)) in PlatformSections)
        {
            currentManifest.Platforms.TryGetValue(platform, out var prevSection);
            var section = ProcessSection(path, entryPoint, prevSection, excludes, platform != "win");
            if (section != null)
                platforms[platform] = section;
        }

        FileInfo? launcherInfo = null;
        var launcherPath = "NelderimLauncher.exe";
        if (File.Exists(launcherPath))
        {
            launcherInfo = ProcessFile(launcherPath, launcherPath, currentManifest.Launcher);
        }

        var newManifest = new Manifest(currentManifest.Version + 1, launcherInfo, common, platforms);

        using var newManifestStream = File.Create(manifestPath);
        JsonSerializer.Serialize(newManifestStream, newManifest);
    }

    private static ManifestSection? ProcessSection(string path, string entryPoint, ManifestSection? prevSection,
        string[] excludes, bool unix)
    {
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"Warning: {path} not found, skipping");
            return null;
        }

        var prefix = path.TrimEnd('/') + "/";
        var fileInfos = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Select(realFilename => (realFilename,
                relative: realFilename.Replace(Path.DirectorySeparatorChar, '/').Substring(prefix.Length))) //Normalize to unix style
            .Where(f => !excludes.Any(f.relative.StartsWith)) //Exclude based on prefix
            .OrderBy(f => f.relative, StringComparer.Ordinal)
            .Select(f =>
            {
                var prevFileInfo = prevSection?.Files.FirstOrDefault(p => p.File == f.relative);
                var fileInfo = ProcessFile(f.realFilename, f.relative, prevFileInfo);
                return fileInfo;
            }).ToList();

        return new ManifestSection(path, entryPoint, fileInfos);
    }

    private static FileInfo ProcessFile(string realFilename, string filename, FileInfo? prevFileInfo)
    {
        Console.WriteLine("Hashing: " + realFilename);
        var newSha = Utils.Sha1Hash(realFilename);

        var prevVersion = prevFileInfo?.Version ?? 0;
        var prevSha = prevFileInfo?.Sha1 ?? "";
        var newVersion = newSha != prevSha ? prevVersion + 1 : prevVersion;

        return new FileInfo(filename, newVersion, newSha);
    }
}
