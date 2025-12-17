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

        private static readonly Dictionary<string, HashSet<string>> LoaderAliases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["fabric"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "fabric", "fabric-loader", "fabric_loader" 
            },
            ["forge"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "forge", "forge-loader" 
            },
            ["neoforge"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "neoforge", "neo-forge", "neoforge-loader" 
            }
        };

        // NEUE METHODE: Filtert nach Loader und Shader-Option
        public async Task<string> DownloadLatestForLoaderAndShaderAsync(
            string projectId, 
            string modloader, 
            string shaderOption,
            string outputDir)
        {
            // Parameter validieren
            ValidateParameters(projectId, outputDir);
            
            Console.WriteLine($"📥 Lade Versionen für '{projectId}'...");
            var allVersions = await _api.GetVersionsAsync(projectId);
            
            if (allVersions == null || !allVersions.Any())
                throw new Exception($"Keine Versionen für '{projectId}' gefunden.");

            // Passende Loader-Aliase bestimmen
            HashSet<string> aliasList = GetLoaderAliases(modloader);

            // Filtere Versionen nach Loader und Shader-Option
            var filteredVersions = FilterVersionsByLoaderAndShader(allVersions, aliasList, shaderOption);

            Console.WriteLine($"📊 Statistik: {allVersions.Length} Versionen total, {filteredVersions.Count} passend");

            if (filteredVersions.Count == 0)
            {
                throw CreateNoMatchingVersionError(allVersions, modloader, null, shaderOption);
            }

            // Neueste Version finden
            var latestVersion = FindLatestVersion(filteredVersions);
            
            Console.WriteLine($"\n🎯 Gefundene neueste Version ({shaderOption}):");
            Console.WriteLine($"   • Version: {latestVersion.VersionNumber}");
            Console.WriteLine($"   • Loader: {string.Join(", ", latestVersion.Loaders)}");
            Console.WriteLine($"   • Minecraft: {string.Join(", ", latestVersion.GameVersions)}");

            // Herunterladen
            return await DownloadVersionFile(latestVersion, projectId, outputDir);
        }

        // NEUE METHODE: Filtert nach Loader, Minecraft-Version und Shader-Option
        public async Task<string> DownloadLatestForLoaderMinecraftAndShaderAsync(
            string projectId, 
            string modloader, 
            string minecraftVersion,
            string shaderOption,
            string outputDir)
        {
            // Parameter validieren
            ValidateParameters(projectId, outputDir);
            
            Console.WriteLine($"📥 Lade Versionen für '{projectId}'...");
            var allVersions = await _api.GetVersionsAsync(projectId);
            
            if (allVersions == null || !allVersions.Any())
                throw new Exception($"Keine Versionen für '{projectId}' gefunden.");

            // Passende Loader-Aliase bestimmen
            HashSet<string> aliasList = GetLoaderAliases(modloader);

            // Filtere Versionen nach Loader, Minecraft-Version und Shader-Option
            var filteredVersions = FilterVersionsByLoaderMinecraftAndShader(
                allVersions, aliasList, minecraftVersion, shaderOption);

            Console.WriteLine($"📊 Statistik: {allVersions.Length} Versionen total, {filteredVersions.Count} passend");

            if (filteredVersions.Count == 0)
            {
                throw CreateNoMatchingVersionError(allVersions, modloader, minecraftVersion, shaderOption);
            }

            // Neueste Version finden
            var latestVersion = FindLatestVersion(filteredVersions);
            
            Console.WriteLine($"\n🎯 Gefundene neueste Version ({shaderOption}):");
            Console.WriteLine($"   • Version: {latestVersion.VersionNumber}");
            Console.WriteLine($"   • Loader: {string.Join(", ", latestVersion.Loaders)}");
            Console.WriteLine($"   • Minecraft: {string.Join(", ", latestVersion.GameVersions)}");

            // Herunterladen
            return await DownloadVersionFile(latestVersion, projectId, outputDir);
        }

        // ALTE METHODEN FÜR ABWÄRTSKOMPATIBILITÄT
        public async Task<string> DownloadLatestForLoaderAsync(
            string projectId, 
            string modloader, 
            string outputDir)
        {
            return await DownloadLatestForLoaderAndShaderAsync(
                projectId, 
                modloader, 
                "Mit Shadern", // Standard: Mit Shadern
                outputDir
            );
        }

        public async Task<string> DownloadLatestForLoaderAndMinecraftAsync(
            string projectId, 
            string modloader, 
            string minecraftVersion,
            string outputDir)
        {
            return await DownloadLatestForLoaderMinecraftAndShaderAsync(
                projectId, 
                modloader, 
                minecraftVersion,
                "Mit Shadern", // Standard: Mit Shadern
                outputDir
            );
        }

        #region Hilfsmethoden

        private void ValidateParameters(string projectId, string outputDir)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("Project ID darf nicht leer sein.", nameof(projectId));
            
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ArgumentException("Output directory darf nicht leer sein.", nameof(outputDir));
        }

        private HashSet<string> GetLoaderAliases(string desiredLoader)
        {
            if (LoaderAliases.TryGetValue(desiredLoader, out var aliases))
            {
                return new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { desiredLoader };
            }
        }

        private List<ModrinthVersion> FilterVersionsByLoaderAndShader(
            ModrinthVersion[] allVersions, 
            HashSet<string> aliasList, 
            string shaderOption)
        {
            var filtered = new List<ModrinthVersion>();
            
            Console.WriteLine($"\n🎯 Filtere nach: Loader + Shader='{shaderOption}'");
            Console.WriteLine($"   Regel: 'ns' im Namen = Ohne Shader, sonst Mit Shadern");
            
            foreach (var version in allVersions)
            {
                // Prüfe Loader
                if (!HasMatchingLoader(version, aliasList))
                    continue;

                // Prüfe Shader-Option mit "ns"-Logik
                bool matchesShader = MatchesShaderOption(version, shaderOption);
                
                if (matchesShader)
                {
                    filtered.Add(version);
                    Console.WriteLine($"   ✓ {version.VersionNumber}: Passt zu '{shaderOption}'");
                }
                else
                {
                    Console.WriteLine($"   ✗ {version.VersionNumber}: Passt NICHT zu '{shaderOption}'");
                }
            }

            return filtered;
        }

        private List<ModrinthVersion> FilterVersionsByLoaderMinecraftAndShader(
            ModrinthVersion[] allVersions, 
            HashSet<string> aliasList, 
            string minecraftVersion,
            string shaderOption)
        {
            var filtered = new List<ModrinthVersion>();
            
            Console.WriteLine($"\n🎯 Filtere nach: Loader + MC {minecraftVersion} + Shader='{shaderOption}'");
            Console.WriteLine($"   Regel: 'ns' im Namen = Ohne Shader, sonst Mit Shadern");
            
            foreach (var version in allVersions)
            {
                // Prüfe Loader
                if (!HasMatchingLoader(version, aliasList))
                    continue;

                // Prüfe Minecraft-Version
                if (!HasMinecraftVersion(version, minecraftVersion))
                    continue;

                // Prüfe Shader-Option
                bool matchesShader = MatchesShaderOption(version, shaderOption);
                
                if (matchesShader)
                {
                    filtered.Add(version);
                    Console.WriteLine($"   ✓ {version.VersionNumber}: Passt zu '{shaderOption}' (MC {minecraftVersion})");
                }
                else
                {
                    Console.WriteLine($"   ✗ {version.VersionNumber}: Passt NICHT zu '{shaderOption}' (MC {minecraftVersion})");
                }
            }

            return filtered;
        }

        private bool HasMatchingLoader(ModrinthVersion version, HashSet<string> aliasList)
        {
            if (version.Loaders == null || !version.Loaders.Any())
                return false;

            foreach (var loader in version.Loaders)
            {
                if (!string.IsNullOrWhiteSpace(loader) && 
                    aliasList.Contains(loader.Trim().ToLowerInvariant()))
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasMinecraftVersion(ModrinthVersion version, string minecraftVersion)
        {
            if (version.GameVersions == null || !version.GameVersions.Any())
                return false;

            foreach (var gameVersion in version.GameVersions)
            {
                if (string.Equals(gameVersion, minecraftVersion, StringComparison.OrdinalIgnoreCase))
                    return true;
                
                if (gameVersion.StartsWith(minecraftVersion + ".", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool MatchesShaderOption(ModrinthVersion version, string shaderOption)
        {
            // EINFACHE "ns"-LOGIK:
            // - Enthält "ns" im Versionsnamen = Ohne Shader
            // - Enthält KEIN "ns" = Mit Shadern (Standard)
            
            if (string.IsNullOrWhiteSpace(version.VersionNumber))
                return shaderOption == "Mit Shadern"; // Fallback
            
            var versionLower = version.VersionNumber.ToLowerInvariant();
            
            // Prüfe ob "ns" im Namen enthalten ist
            bool hasNsInName = versionLower.Contains("ns");
            
            if (shaderOption == "Mit Shadern")
            {
                // Für "Mit Shadern": DARF NICHT "ns" enthalten
                return !hasNsInName;
            }
            else // "Ohne Shader"
            {
                // Für "Ohne Shader": MUSS "ns" enthalten
                return hasNsInName;
            }
        }

        private Exception CreateNoMatchingVersionError(
            ModrinthVersion[] allVersions, 
            string modloader, 
            string minecraftVersion,
            string shaderOption)
        {
            // Sammle alle verfügbaren Kombinationen
            var availableInfo = new List<string>();
            
            foreach (var v in allVersions)
            {
                if (v.Loaders != null && v.GameVersions != null)
                {
                    var loaders = string.Join(", ", v.Loaders);
                    var mcVersions = string.Join(", ", v.GameVersions);
                    var shaderHint = DetectShaderHint(v);
                    
                    availableInfo.Add($"• {v.VersionNumber}: {loaders} | MC: {mcVersions} | {shaderHint}");
                }
            }

            var errorMessage = new System.Text.StringBuilder();
            errorMessage.AppendLine($"❌ Keine passende Version gefunden!");
            errorMessage.AppendLine($"   Gesucht: Loader='{modloader}', Shader='{shaderOption}'");
            
            if (!string.IsNullOrWhiteSpace(minecraftVersion))
            {
                errorMessage.AppendLine($"          Minecraft='{minecraftVersion}'");
            }
            
            errorMessage.AppendLine($"\n📋 Verfügbare Versionen im Projekt (mit Shader-Erkennung):");
            errorMessage.AppendLine($"   Regel: 'ns' im Namen = Ohne Shader, sonst Mit Shadern");
            
            // Zeige die 10 neuesten Versionen
            var recentVersions = availableInfo.Take(10).ToList();
            foreach (var info in recentVersions)
            {
                errorMessage.AppendLine($"   {info}");
            }
            
            if (availableInfo.Count > 10)
            {
                errorMessage.AppendLine($"   • ... und {availableInfo.Count - 10} weitere");
            }
            
            errorMessage.AppendLine($"\n💡 Tipp: Versionen mit 'ns' im Namen sind ohne Shader.");

            return new Exception(errorMessage.ToString());
        }

        private string DetectShaderHint(ModrinthVersion version)
        {
            // Einfache "ns"-Erkennung
            if (!string.IsNullOrWhiteSpace(version.VersionNumber))
            {
                var versionLower = version.VersionNumber.ToLowerInvariant();
                
                if (versionLower.Contains("ns"))
                    return "⚡ Ohne Shader (enthält 'ns')";
                else
                    return "✨ Mit Shadern (kein 'ns')";
            }
            
            return "❓ Unklar";
        }

        private ModrinthVersion FindLatestVersion(List<ModrinthVersion> versions)
        {
            ModrinthVersion latestVersion = null;
            
            foreach (var version in versions)
            {
                if (latestVersion == null)
                {
                    latestVersion = version;
                    continue;
                }

                var currentVersion = TryParseVersion(version.VersionNumber);
                var bestVersion = TryParseVersion(latestVersion.VersionNumber);
                
                if (currentVersion > bestVersion)
                {
                    latestVersion = version;
                }
                else if (currentVersion == bestVersion)
                {
                    if (version.DatePublished > latestVersion.DatePublished)
                    {
                        latestVersion = version;
                    }
                }
            }

            return latestVersion ?? throw new Exception("Konnte neueste Version nicht bestimmen.");
        }

        private async Task<string> DownloadVersionFile(ModrinthVersion version, string projectId, string outputDir)
        {
            if (version.Files == null || !version.Files.Any())
                throw new Exception($"Version {version.VersionNumber} hat keine Dateien.");

            // Primäre Datei finden
            var primaryFile = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files[0];

            if (primaryFile == null || string.IsNullOrWhiteSpace(primaryFile.Url))
                throw new Exception("Keine Download-URL gefunden.");

            var filename = string.IsNullOrWhiteSpace(primaryFile.Filename) 
                ? $"{projectId}-{version.VersionNumber}.mrpack" 
                : primaryFile.Filename;

            var outputPath = Path.Combine(outputDir, filename);
            Directory.CreateDirectory(outputDir);

            Console.WriteLine($"\n📥 Lade herunter: {primaryFile.Filename}");
            Console.WriteLine($"   • Größe: {FormatFileSize(primaryFile.Size)}");
            Console.WriteLine($"   • Ziel: {outputPath}");

            using var response = await _httpClient.GetAsync(primaryFile.Url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);

            Console.WriteLine($"\n✅ Download erfolgreich abgeschlossen!");
            return outputPath;
        }

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

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion
    }
}