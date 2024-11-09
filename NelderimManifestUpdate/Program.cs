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
        
        var currentManifest = new Manifest(0, [], entryPoint);
        if (File.Exists(manifestPath))
        {
            using var currentManifestStream = File.OpenRead(manifestPath);
            currentManifest = JsonSerializer.Deserialize<Manifest>(currentManifestStream);
            File.Move(manifestPath, oldManifestPath, true); //Just in case
        }

        var fileInfos = filteredFiles.Select(filename =>
        {
            var newSha = Utils.Sha1Hash(filename);
            var newFileName = filename.StartsWith(workDir) ? filename.Substring(workDir.Length + 1) : filename; // Remove the workDir+separator prefix 
            var prevFileInfo = currentManifest!.Files.FirstOrDefault(f => f.File == newFileName);
            var prevVersion = prevFileInfo?.Version ?? 0;
            var prevSha = prevFileInfo?.Sha1 ?? "";
            var newVersion = newSha != prevSha ? prevVersion + 1 : prevVersion;

            return new FileInfo(newFileName, newVersion, newSha);
        }).ToArray();

        var newManifest = new Manifest(currentManifest.Version + 1, fileInfos, entryPoint);

        using var newManifestStream = File.OpenWrite(manifestPath);
        JsonSerializer.Serialize(newManifestStream, newManifest);
    }
}






