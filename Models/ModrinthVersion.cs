using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SuperSMPLauncher.Models;

public class ModrinthVersion
{
    public string Id { get; set; }
    public string VersionNumber { get; set; }
    public List<ModrinthFile> Files { get; set; }

    // WICHTIG: Modloader-Info
    public List<string> Loaders { get; set; }
}
