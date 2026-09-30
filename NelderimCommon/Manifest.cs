using System.Text.Json.Serialization;

namespace Nelderim;

public enum Platform
{
    win,
    linux,
    osx
}

public class Manifest(int version, Dictionary<Platform, FileInfo>? launchers, ManifestSection? common, Dictionary<Platform, ManifestSection>? platforms)
{
    public int Version { get; } = version;
    public Dictionary<Platform, FileInfo> Launchers { get; } = launchers ?? new();
    public ManifestSection Common { get; } = common ?? new ManifestSection("", "", []);
    public Dictionary<Platform, ManifestSection> Platforms { get; } = platforms ?? new();

    public static Manifest Empty => new(0, null, null, null);

    public FileInfo? LauncherFor(Platform platform)
    {
        return Launchers.GetValueOrDefault(platform);
    }

    public string EntryPointFor(Platform platform)
    {
        return Platforms.TryGetValue(platform, out var section) ? section.EntryPoint : "";
    }

    // Common files + platform files, platform entry wins on duplicate local path
    public List<FileInfo> FilesFor(Platform platform)
    {
        var sections = new List<ManifestSection> { Common };
        if (Platforms.TryGetValue(platform, out var platformSection))
            sections.Add(platformSection);

        var result = new Dictionary<string, FileInfo>();
        foreach (var section in sections)
        {
            foreach (var file in section.Files)
            {
                file.Source = section.Path;
                result[file.File] = file;
            }
        }
        return result.Values.ToList();
    }

    public List<FileInfo> ChangesBetween(Manifest otherManifest, Platform platform)
    {
        if (otherManifest.Version != Version)
        {
            var files = FilesFor(platform);
            var otherFiles = otherManifest.FilesFor(platform);
            var added = otherFiles.ExceptBy<FileInfo, string>(files.Select(f => f.File), f => f.File);
            var removed = files.ExceptBy<FileInfo, string>(otherFiles.Select(f => f.File), f => f.File)
                .Select(f => new FileInfo(f.File, -1, ""));
            var changed = otherFiles.Join(files,
                    f => f.File,
                    f => f.File,
                    (thisFile, otherFile) => new { thisFile, otherFile })
                .Where(p => p.thisFile.Version != p.otherFile.Version)
                .Select(p => p.thisFile);
            return added.Concat(removed).Concat(changed).ToList();
        }
        return [];
    }
}

public class ManifestSection(string path, string entryPoint, List<FileInfo>? files)
{
    // Directory on the server, relative to patch url
    public string Path { get; } = path;
    public string EntryPoint { get; } = entryPoint;
    // Paths relative to Path on the server, and to the launcher directory locally
    public List<FileInfo> Files { get; } = files ?? [];
}

public class FileInfo(string file, int version, string sha1)
{
    public string File { get; set; } = file;
    public int Version { get; set; } = version;
    public string Sha1 { get; set; } = sha1;

    // JSON file keys enforced from server copy; rest of an existing local file is kept
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? MergeKeys { get; set; }

    [JsonIgnore]
    public string Source { get; set; } = "";
}

[JsonSerializable(typeof(Manifest))]
public partial class ManifestJsonContext : JsonSerializerContext;
