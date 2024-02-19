using Nelderim.Utility;

namespace Nelderim.Model;

public class PatchInfo
{
    public string Filename { get; }
    public string Timestamp { get; }
    public string Remotesha1 { get; }
    public string Localsha1 { get; }
    public bool ShouldUpdate { get; }

    public PatchInfo(Patch patch)
    {
        Filename = patch.Filename;
        Timestamp = patch.Timestamp;
        Remotesha1 = patch.Sha1;
        if (File.Exists(Filename))
        {
            using var fileStream = File.OpenRead(Filename);
            Localsha1 = Crypto.Sha1Hash(fileStream);
            ShouldUpdate = Localsha1 != Remotesha1;
        }
        else
        {
            Localsha1 = "N/A";
            ShouldUpdate = true;
        }
    }

    public override string ToString()
    {
        return $"{Filename} {Timestamp}";
    }
}