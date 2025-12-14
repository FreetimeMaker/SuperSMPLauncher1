using System;
using SuperSMPLauncher.Models;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SuperSMPLauncher.Services;

public class ModpackDownloader
{
    private readonly ModrinthApi _api = new();

    public async Task<string> DownloadLatestForLoaderAsync(
        string projectId,
        string modloader,
        string outputDir)
    {
        var versions = await _api.GetVersionsAsync(projectId);

        // Filter nach Modloader
        var filtered = versions
            .Where(v => v.Loaders.Contains(modloader.ToLower()))
            .ToList();

        if (filtered.Count == 0)
            throw new Exception($"Keine Version für Modloader '{modloader}' gefunden!");

        // Neueste Version auswählen
        var latest = filtered
            .OrderByDescending(v => v.VersionNumber)
            .First();

        var file = latest.Files.First();
        var outputPath = Path.Combine(outputDir, file.Filename);

        using var client = new HttpClient();
        using var stream = await client.GetStreamAsync(file.Url);
        using var fileStream = File.Create(outputPath);

        await stream.CopyToAsync(fileStream);

        return outputPath;
    }
}