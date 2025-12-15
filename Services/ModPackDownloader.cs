using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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
        var versions = await _api.GetVersionsAsync(projectId);

        string desired = (modloader ?? "").Trim();
        var aliasSet = new[] { "fabric", "fabric-loader", "fabric_loader", desired }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();

        var filtered = versions
            .Where(v => v.Loaders != null &&
                        v.Loaders.Any(l => aliasSet.Contains(l.Trim().ToLowerInvariant())))
            .ToList();

        if (!filtered.Any())
        {
            var available = versions
                .Where(v => v.Loaders != null)
                .SelectMany(v => v.Loaders)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Distinct();
            throw new Exception($"Keine Version für Modloader '{modloader}' gefunden! Gefundene Loader-Bezeichnungen: {string.Join(", ", available)}");
        }

        // sichere Auswahl der neuesten vorhandenen Version (lexikographisch nach VersionNumber)
        var latestVersion = filtered
            .OrderByDescending(v => v.VersionNumber ?? string.Empty)
            .First();

        if (latestVersion.Files == null || !latestVersion.Files.Any())
            throw new Exception("Die gefundene Version enthält keine Dateien.");

        var file = latestVersion.Files.First();
        var filename = file.Filename ?? $"{projectId}.zip";
        var outputPath = Path.Combine(outputDir, filename);

        Directory.CreateDirectory(outputDir);

        using var client = new HttpClient();
        var url = file.Url ?? throw new Exception("Datei-URL fehlt.");
        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Download fehlgeschlagen: {response.StatusCode}");

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(outputPath);
        await stream.CopyToAsync(fileStream);

        return outputPath;
    }
}
