using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SuperSMPLauncher.Models;

namespace SuperSMPLauncher.Services
{
    public class ModpackDownloader
    {
        private readonly ModrinthApi _api = new ModrinthApi();
        private static readonly HttpClient _httpClient = new HttpClient();

        private static readonly HashSet<string> FabricAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            "fabric", "fabric-loader", "fabric_loader" 
        };

        public async Task<string> DownloadLatestForLoaderAsync(
            string projectId, 
            string modloader, 
            string outputDir)
        {
            // 1. VALIDIERUNG
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID darf nicht leer sein.", nameof(projectId));
            
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ArgumentException("Output directory darf nicht leer sein.", nameof(outputDir));

            // 2. VERSIONEN VON API HOLEN
            Console.WriteLine($"Lade Versionen für '{projectId}'...");
            var versions = await _api.GetVersionsAsync(projectId);
            
            if (versions == null || !versions.Any())
                throw new Exception($"Keine Versionen für '{projectId}' gefunden.");

            // 3. LOADER FILTERN
            string desired = (modloader ?? "").Trim().ToLowerInvariant();
            var aliasList = new HashSet<string>(FabricAliases, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(desired) && !aliasList.Contains(desired))
                aliasList.Add(desired);

            // Filtere Versionen nach Loader
            var filtered = new List<ModrinthVersion>();
            foreach (var v in versions)
            {
                if (v.Loaders != null && v.Loaders.Any(l => 
                    !string.IsNullOrWhiteSpace(l) && 
                    aliasList.Contains(l.Trim().ToLowerInvariant())))
                {
                    filtered.Add(v);
                }
            }

            Console.WriteLine($"Gefunden: {versions.Length} Versionen total, {filtered.Count} mit Loader '{modloader}'");

            if (filtered.Count == 0)
            {
                // Sammle alle verfügbaren Loader
                var allLoaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var v in versions)
                {
                    if (v.Loaders != null)
                    {
                        foreach (var l in v.Loaders)
                        {
                            if (!string.IsNullOrWhiteSpace(l))
                                allLoaders.Add(l.Trim());
                        }
                    }
                }

                throw new Exception(
                    $"Keine Version für Modloader '{modloader}' gefunden!\n" +
                    $"Verfügbare Loader: {string.Join(", ", allLoaders.OrderBy(l => l))}"
                );
            }

            // 4. NEUESTE VERSION FINDEN (OHNE LINQ-PROBLEME)
            ModrinthVersion latestVersion = null;
            foreach (var version in filtered)
            {
                if (latestVersion == null)
                {
                    latestVersion = version;
                    continue;
                }

                var currentVersion = TryParseVersion(version.VersionNumber);
                var bestVersion = TryParseVersion(latestVersion.VersionNumber);
                
                // Vergleiche Versionen
                if (currentVersion > bestVersion)
                {
                    latestVersion = version;
                }
                else if (currentVersion == bestVersion)
                {
                    // Bei gleicher Version, neuestes Datum nehmen
                    if (version.DatePublished > latestVersion.DatePublished)
                    {
                        latestVersion = version;
                    }
                }
            }

            if (latestVersion == null)
                throw new Exception("Konnte neueste Version nicht bestimmen.");

            Console.WriteLine($"Neueste Version: {latestVersion.VersionNumber} (vom {latestVersion.DatePublished:dd.MM.yyyy})");

            // 5. DATEIEN PRÜFEN
            if (latestVersion.Files == null || !latestVersion.Files.Any())
                throw new Exception($"Version {latestVersion.VersionNumber} hat keine Dateien.");

            // Primäre Datei finden
            ModrinthFile primaryFile = null;
            foreach (var file in latestVersion.Files)
            {
                if (file.Primary)
                {
                    primaryFile = file;
                    break;
                }
            }
            
            // Fallback: erste Datei
            if (primaryFile == null)
                primaryFile = latestVersion.Files[0];

            if (primaryFile == null || string.IsNullOrWhiteSpace(primaryFile.Url))
                throw new Exception("Keine Download-URL gefunden.");

            Console.WriteLine($"Lade herunter: {primaryFile.Filename} ({primaryFile.Size / 1024 / 1024} MB)");

            // 6. HERUNTERLADEN
            var filename = string.IsNullOrWhiteSpace(primaryFile.Filename) 
                ? $"{projectId}-{latestVersion.VersionNumber}.mrpack" 
                : primaryFile.Filename;

            var outputPath = Path.Combine(outputDir, filename);
            Directory.CreateDirectory(outputDir);

            using var response = await _httpClient.GetAsync(primaryFile.Url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);

            Console.WriteLine($"✅ Download abgeschlossen: {outputPath}");
            return outputPath;
        }

        // Hilfsmethode für Version-Parsing
        private static Version TryParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return new Version(0, 0, 0);
            
            var clean = versionString.Trim().TrimStart('v', 'V');
            var withoutBuild = clean.Split('+')[0];
            var withoutPreRelease = withoutBuild.Split('-')[0];
            
            if (Version.TryParse(withoutPreRelease, out var version))
                return version;
            
            return new Version(0, 0, 0);
        }
    }
}