using System.Text.Json.Serialization;
using Nelderim.Utility;

namespace Nelderim.Model;

public class Patch
{
    public Patch(string filename)
    {
        Filename = filename;
        Timestamp = File.GetLastWriteTime(filename).ToString();
        if (File.Exists(filename))
        {
            using var fileStream = File.OpenRead(filename);
            Sha1 = Crypto.Sha1Hash(fileStream);
        }
    }

    [JsonPropertyName("filename")] public string Filename { get; set; }

    [JsonPropertyName("timestamp")] public string Timestamp { get; set; }

    [JsonPropertyName("sha1")] public string Sha1 { get; set; }
}