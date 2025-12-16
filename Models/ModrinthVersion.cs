using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SuperSMPLauncher.Models;

public class ModrinthVersion
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("version_number")]
    public string? VersionNumber { get; set; }

    [JsonPropertyName("files")]
    public List<ModrinthFile>? Files { get; set; }

    [JsonPropertyName("platforms")]
    public List<string>? Platforms { get; set; }

    [JsonPropertyName("loaders")]
    public List<string>? Loaders { get; set; }
}