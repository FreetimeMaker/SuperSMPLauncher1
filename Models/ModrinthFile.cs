using System.Text.Json.Serialization;

namespace SuperSMPLauncher.Models;

public class ModrinthFile
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}