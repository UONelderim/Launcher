
using System.Text.Json.Serialization;

namespace NelderimLauncher.Models;

public class Patch
{
    [JsonPropertyName("filename")]
    public string File { get; set; }
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; }
    
    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; }
}