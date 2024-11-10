using System.Diagnostics;
using System.Text.Json;

namespace Nelderim;

public class Program
{
    public static void Main(string[] args)
    {
        var entryPoint = "ClassicUO/ClassicUO.exe";
        var workDir = "Nelderim";
        var manifestPath = $"{workDir}.manifest.json";
        var oldManifestPath = $"{manifestPath}.old";
        var procName = Process.GetCurrentProcess().ProcessName;

        var excludes = File.ReadAllLines($"{procName}.exclude");
        var allFiles = Directory.GetFiles(workDir, "**", SearchOption.AllDirectories);

        var filteredFiles = allFiles
            .Select(s => s.Replace(Path.DirectorySeparatorChar, '/')) //Normalize to unix style
            .Where(f => !excludes.Any(f.StartsWith)) //Exclude based on prefix
            .Order();
        
        var currentManifest = new Manifest(0, [], null, entryPoint);
        if (File.Exists(manifestPath))
        {
            using var currentManifestStream = File.OpenRead(manifestPath);
            currentManifest = JsonSerializer.Deserialize<Manifest>(currentManifestStream);
            File.Move(manifestPath, oldManifestPath, true); //Just in case
        }

        var fileInfos = filteredFiles.Select(realFilename =>
        {
            var newFileName = realFilename.StartsWith(workDir + Path.DirectorySeparatorChar) ? realFilename.Substring(workDir.Length + 1) : realFilename; // Remove the workDir+separator prefix 
            var prevFileInfo = currentManifest!.Files.FirstOrDefault(f => f.File == newFileName);
            return ProcessFile(realFilename, newFileName, prevFileInfo);
        }).ToList();
        
        FileInfo launcherInfo = null;
        var launcherPath = "NelderimLauncher.exe";
        if (File.Exists(launcherPath))
        {
            launcherInfo = ProcessFile(launcherPath, launcherPath, currentManifest.Launcher);
        }
        
        var newManifest = new Manifest(currentManifest.Version + 1, fileInfos, launcherInfo, entryPoint);

        using var newManifestStream = File.OpenWrite(manifestPath);
        JsonSerializer.Serialize(newManifestStream, newManifest);
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








