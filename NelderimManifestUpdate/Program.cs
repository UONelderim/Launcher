using System.Diagnostics;
using System.Text.Json;

namespace Nelderim;

public class Program
{
    // Server layout: common client assets and ClassicUO package per platform (see scripts/build_package.py)
    private const string CommonPath = "Nelderim";
    private static readonly Dictionary<Platform, (string Path, string EntryPoint)> PlatformSections = new()
    {
        [Platform.win] = ("dist/win", "ClassicUO/ClassicUO.exe"),
        [Platform.linux] = ("dist/linux", "ClassicUO/ClassicUO"),
        [Platform.osx] = ("dist/osx", "ClassicUO/ClassicUO"),
    };
    private static readonly Dictionary<Platform, string> LauncherPaths = new()
    {
        [Platform.win] = "launcher/win/NelderimLauncher.exe",
        [Platform.linux] = "launcher/linux/NelderimLauncher",
        [Platform.osx] = "launcher/osx/NelderimLauncher",
    };

    public static void Main(string[] args)
    {
        var manifestPath = "Nelderim.manifest.json";
        var oldManifestPath = $"{manifestPath}.old";
        var procName = Process.GetCurrentProcess().ProcessName;

        var excludes = File.ReadAllLines($"{procName}.exclude").Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        // Files holding user data, only listed keys are forced from server copy. Format: path: key1, key2
        var mergeFiles = File.ReadAllLines($"{procName}.merge")
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split(':', 2))
            .ToDictionary(p => p[0].Trim(),
                p => p[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        var currentManifest = Manifest.Empty;
        if (File.Exists(manifestPath))
        {
            using (var currentManifestStream = File.OpenRead(manifestPath))
                currentManifest = JsonSerializer.Deserialize(currentManifestStream, ManifestJsonContext.Default.Manifest)!;
            File.Move(manifestPath, oldManifestPath, true); //Just in case
        }

        var common = ProcessSection(CommonPath, "", currentManifest.Common, excludes, mergeFiles, false);
        var platforms = new Dictionary<Platform, ManifestSection>();
        foreach (var (platform, (path, entryPoint)) in PlatformSections)
        {
            currentManifest.Platforms.TryGetValue(platform, out var prevSection);
            var section = ProcessSection(path, entryPoint, prevSection, excludes, mergeFiles, platform != Platform.win);
            if (section != null)
                platforms[platform] = section;
        }

        var launchers = new Dictionary<Platform, FileInfo>();
        foreach (var (platform, path) in LauncherPaths)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Warning: {path} not found, skipping");
                continue;
            }
            launchers[platform] = ProcessFile(path, path, currentManifest.LauncherFor(platform));
        }

        var newManifest = new Manifest(currentManifest.Version + 1, launchers, common, platforms);

        using var newManifestStream = File.Create(manifestPath);
        JsonSerializer.Serialize(newManifestStream, newManifest, ManifestJsonContext.Default.Manifest);
    }

    private static ManifestSection? ProcessSection(string path, string entryPoint, ManifestSection? prevSection,
        string[] excludes, Dictionary<string, string[]> mergeFiles, bool unix)
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
                fileInfo.MergeKeys = mergeFiles.GetValueOrDefault(f.relative);
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
