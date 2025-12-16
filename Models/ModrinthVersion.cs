using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SuperSMPLauncher.Models
{
    public class ModrinthVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("version_number")]
        public string VersionNumber { get; set; } = string.Empty;

        [JsonPropertyName("loaders")]
        public List<string> Loaders { get; set; } = new List<string>();

        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; } = new List<string>();

        [JsonPropertyName("date_published")]
        public DateTime DatePublished { get; set; }

        [JsonPropertyName("files")]
        public List<ModrinthFile> Files { get; set; } = new List<ModrinthFile>();
    }
}