namespace Nelderim;

public class Manifest(int version, FileInfo[] files, string entryPoint)
{
    public int Version { get; } = version;
    public FileInfo[] Files { get; } = files;
    public string EntryPoint { get; } = entryPoint;

    public List<FileInfo> ChangesBetween(Manifest otherManifest)
    {
        if (otherManifest.Version != Version)
        {
            var added = otherManifest.Files.ExceptBy<FileInfo, string>(Files.Select(f => f.File), f => f.File);
            var removed = Files.ExceptBy<FileInfo, string>(otherManifest.Files.Select(f => f.File), f => f.File)
                .Select(f => new FileInfo(f.File, -1, ""));
            var changed = otherManifest.Files.Join(Files,
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

public class FileInfo(string file, int version, string sha1)
{
    public string File { get; set; } = file;
    public int Version { get; set; } = version;
    public string Sha1 { get; set; } = sha1;
}
