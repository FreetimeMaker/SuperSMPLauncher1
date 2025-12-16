using System.Text.Json.Serialization;

namespace SuperSMPLauncher.Models
{
    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}