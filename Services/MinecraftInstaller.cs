using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SuperSMPLauncher.Services
{
    public class MinecraftInstaller
    {
        private readonly HttpClient _httpClient = new HttpClient();
        
        public async Task InstallMinecraftVersionAsync(string version, string minecraftPath)
        {
            try
            {
                // 1. Minecraft-Version Manifest laden
                var manifest = await GetVersionManifestAsync();
                
                // 2. Spezifische Version finden
                var versionInfo = await GetVersionInfoAsync(version, manifest);
                
                // 3. Client JAR herunterladen
                await DownloadClientJarAsync(versionInfo, minecraftPath);
                
                // 4. Libraries herunterladen
                await DownloadLibrariesAsync(versionInfo, minecraftPath);
                
                // 5. Assets herunterladen
                await DownloadAssetsAsync(versionInfo, minecraftPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Fehler beim Installieren von Minecraft {version}: {ex.Message}", ex);
            }
        }

        private async Task<JObject> GetVersionManifestAsync()
        {
            var url = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
            var response = await _httpClient.GetStringAsync(new Uri(url));
            return JObject.Parse(response);
        }

        private async Task<JObject> GetVersionInfoAsync(string version, JObject manifest)
        {
            var versions = manifest["versions"] as JArray;
            var versionUrl = versions?
                .FirstOrDefault(v => v["id"]?.ToString() == version)?["url"]?.ToString();
            
            if (versionUrl == null)
                throw new Exception($"Version {version} nicht gefunden");
            
            var response = await _httpClient.GetStringAsync(new Uri(versionUrl));
            return JObject.Parse(response);
        }

        private async Task DownloadClientJarAsync(JObject versionInfo, string minecraftPath)
        {
            var clientUrl = versionInfo["downloads"]?["client"]?["url"]?.ToString();
            if (clientUrl == null)
                throw new Exception("Client JAR nicht gefunden");
            
            var versionId = versionInfo["id"]?.ToString();
            if (string.IsNullOrEmpty(versionId))
                throw new Exception("Version ID nicht gefunden");
            
            var versionsPath = Path.Combine(minecraftPath, "versions", versionId);
            Directory.CreateDirectory(versionsPath);
            
            var clientPath = Path.Combine(versionsPath, $"{versionId}.jar");
            await DownloadFileAsync(clientUrl, clientPath);
        }

        private async Task DownloadLibrariesAsync(JObject versionInfo, string minecraftPath)
        {
            var libraries = versionInfo["libraries"] as JArray;
            if (libraries == null) return;
            
            var librariesPath = Path.Combine(minecraftPath, "libraries");
            Directory.CreateDirectory(librariesPath);
            
            foreach (var library in libraries)
            {
                var downloads = library["downloads"];
                var artifact = downloads?["artifact"];
                
                if (artifact != null)
                {
                    var url = artifact["url"]?.ToString();
                    var path = artifact["path"]?.ToString();
                    
                    if (url != null && path != null)
                    {
                        var fullPath = Path.Combine(librariesPath, path);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        await DownloadFileAsync(url, fullPath);
                    }
                }
            }
        }

        private async Task DownloadAssetsAsync(JObject versionInfo, string minecraftPath)
        {
            var assetsIndex = versionInfo["assetIndex"];
            if (assetsIndex == null) return;
            
            var assetsUrl = assetsIndex["url"]?.ToString();
            if (assetsUrl == null) return;
            
            // Assets-Index herunterladen
            var assetsPath = Path.Combine(minecraftPath, "assets");
            Directory.CreateDirectory(assetsPath);
            
            var assetsId = versionInfo["assets"]?.ToString();
            if (string.IsNullOrEmpty(assetsId))
                return;
                
            var indexPath = Path.Combine(assetsPath, "indexes", $"{assetsId}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(indexPath));
            
            await DownloadFileAsync(assetsUrl, indexPath);
        }

        private async Task DownloadFileAsync(string url, string filePath)
        {
            using var response = await _httpClient.GetAsync(new Uri(url));
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.Create);
            await stream.CopyToAsync(fileStream);
        }
    }
}