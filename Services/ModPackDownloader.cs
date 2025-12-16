using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SuperSMPLauncher.Models;

namespace SuperSMPLauncher.Services;

public class ModpackDownloader
{
    private readonly ModrinthApi _api = new();

    // Erweiterbare Alias-Liste für Fabric
    private static readonly string[] FabricAliases = new[] { "fabric", "fabric-loader", "fabric_loader" };

        public async Task<string> DownloadLatestForLoaderAsync(
        string projectId,
        string modloader,
        string outputDir)
    {
        var versions = await _api.GetVersionsAsync(projectId) as System.Collections.Generic.List<ModrinthVersion>;
        if (versions == null)
            throw new Exception("GetVersionsAsync returned unexpected type.");

        string desired = (modloader ?? "").Trim().ToLowerInvariant();
        var aliasList = new System.Collections.Generic.List<string> { "fabric", "fabric-loader", "fabric_loader" };
        if (!string.IsNullOrWhiteSpace(desired) && !aliasList.Contains(desired))
            aliasList.Add(desired);

        // Filter: prüfe sowohl Platforms als auch Loaders (falls vorhanden)
        var filtered = new System.Collections.Generic.List<ModrinthVersion>();
        foreach (var v in versions)
        {
            // Prüfe Platforms
            var platforms = v.Platforms;
            if (platforms != null)
            {
                foreach (var p in platforms)
                {
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    var pnorm = p.Trim().ToLowerInvariant();
                    if (aliasList.Contains(pnorm))
                    {
                        filtered.Add(v);
                        break;
                    }
                }

                if (filtered.Contains(v)) continue;
            }

            // Prüfe Loaders (falls API andere Property nutzt)
            var loaders = v.Loaders;
            if (loaders != null)
            {
                foreach (var l in loaders)
                {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    var lnorm = l.Trim().ToLowerInvariant();
                    if (aliasList.Contains(lnorm))
                    {
                        filtered.Add(v);
                        break;
                    }
                }
            }
        }

        if (filtered.Count == 0)
        {
            var available = new System.Collections.Generic.HashSet<string>();
            foreach (var v in versions)
            {
                if (v.Platforms != null)
                    foreach (var p in v.Platforms)
                        if (!string.IsNullOrWhiteSpace(p)) available.Add(p.Trim());
                if (v.Loaders != null)
                    foreach (var l in v.Loaders)
                        if (!string.IsNullOrWhiteSpace(l)) available.Add(l.Trim());
            }

            throw new Exception($"Keine Version für Modloader '{modloader}' gefunden! Gefundene Loader-Bezeichnungen: {string.Join(", ", available)}");
        }

        // Wähle lexikographisch größte VersionNumber (Fallback: erstes Element)
        ModrinthVersion latestVersion = filtered[0];
        for (int i = 1; i < filtered.Count; i++)
        {
            var a = filtered[i].VersionNumber ?? string.Empty;
            var b = latestVersion.VersionNumber ?? string.Empty;
            if (string.Compare(a, b, StringComparison.Ordinal) > 0)
                latestVersion = filtered[i];
        }

        if (latestVersion.Files == null || latestVersion.Files.Count == 0)
            throw new Exception("Die gefundene Version enthält keine Dateien.");

        var file = latestVersion.Files[0];
        var filename = file.Filename ?? $"{projectId}.zip";
        var outputPath = Path.Combine(outputDir, filename);
        Directory.CreateDirectory(outputDir);

        using var client = new HttpClient();
        var url = file.Url ?? throw new Exception("Datei-URL fehlt.");
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Download fehlgeschlagen: {response.StatusCode}");

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(outputPath);
        await stream.CopyToAsync(fileStream);

        return outputPath;
    }
}
