
using System.Text.Json.Serialization;

namespace NelderimLauncher.Models;

public class Patch
{
    public Patch(string file, string timestamp, string sha1)
    {
        File = file;
        Timestamp = timestamp;
        Sha1 = sha1;
    }

    [JsonPropertyName("filename")]
    public string File { get; set; }
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; }
    
    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; }
}