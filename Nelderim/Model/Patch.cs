using System.Text.Json.Serialization;
using Nelderim.Utility;
using static System.IO.File;

namespace Nelderim.Model;

public class Patch
{
    public Patch(string file)
    {
        File = file;
        Timestamp = GetLastWriteTime(file).ToString();
        Sha1 = Crypto.Sha1Hash(OpenRead(file));
    }

    [JsonPropertyName("filename")] public string File { get; set; }

    [JsonPropertyName("timestamp")] public string Timestamp { get; set; }

    [JsonPropertyName("sha1")] public string Sha1 { get; set; }
}