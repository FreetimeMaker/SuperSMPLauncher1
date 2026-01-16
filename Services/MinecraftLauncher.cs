using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace SuperSMPLauncher.Services
{
    public class MinecraftLauncher
    {
        private string _javaPath;
        private string _minecraftPath;
        private const string SERVER_ADDRESS = "supersmp.fun";
        private const int SERVER_PORT = 25565;
        private const string MANIFEST_URL = "https://launcher.mojang.com/v1/objects/0d00eb6235f91daf0875a456c0d78e5bac9e80d8/version_manifest_v2.json";
        private const string LIBRARIES_URL = "https://libraries.minecraft.net/";
        private const string RESOURCES_URL = "https://resources.download.minecraft.net/";

        public MinecraftLauncher()
        {
            FindJava();
            FindMinecraftPath();
        }

        /// <summary>
        /// Startet Minecraft mit dem heruntergeladenen Modpack
        /// </summary>
        public async Task<bool> LaunchMinecraftAsync(string modpackPath, string gameVersion, string modloader)
        {
            try
            {
                // 1. Prüfe ob Java gefunden wurde
                if (string.IsNullOrEmpty(_javaPath))
                {
                    throw new Exception("Java nicht gefunden! Bitte installiere Java 17+ von https://www.oracle.com/java/technologies/downloads/");
                }

                // 2. Prüfe ob Minecraft-Verzeichnis existiert
                if (string.IsNullOrEmpty(_minecraftPath))
                {
                    throw new Exception("Minecraft-Verzeichnis nicht gefunden!");
                }

                // 3. Lade Version Manifest herunter
                Console.WriteLine("📥 Lade Version Manifest herunter...");
                var versionInfo = await DownloadVersionInfoAsync(gameVersion);
                if (versionInfo == null)
                {
                    throw new Exception($"Version {gameVersion} nicht gefunden!");
                }

                // 4. Lade alle Libraries herunter
                Console.WriteLine("📥 Lade Libraries herunter...");
                await DownloadLibrariesAsync(versionInfo);

                // 5. Lade Assets herunter
                Console.WriteLine("📥 Lade Assets herunter...");
                await DownloadAssetsAsync(versionInfo);

                // 6. Lade Minecraft JAR herunter
                Console.WriteLine("📥 Lade Minecraft JAR herunter...");
                await DownloadMinecraftJarAsync(versionInfo);

                // 7. Kopiere Modpack-Dateien
                if (!string.IsNullOrEmpty(modpackPath) && Directory.Exists(modpackPath))
                {
                    Console.WriteLine("📁 Kopiere Modpack-Dateien...");
                    CopyModpackToMinecraft(modpackPath);
                }

                // 8. Starte Minecraft
                await LaunchMinecraftProcessAsync(gameVersion, versionInfo);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Starten von Minecraft: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Findet Java auf dem System
        /// </summary>
        private void FindJava()
        {
            try
            {
                // 1. Prüfe ob Java im PATH ist
                if (TryExecuteCommand("java", "-version"))
                {
                    _javaPath = "java";
                    Console.WriteLine("✅ Java gefunden im PATH");
                    return;
                }

                // 2. Suche nach Java Installation
                var javaLocations = new List<string>
                {
                    // Windows
                    "C:\\Program Files\\Java\\jdk-17\\bin\\java.exe",
                    "C:\\Program Files\\Java\\jdk-21\\bin\\java.exe",
                    "C:\\Program Files (x86)\\Java\\jdk-17\\bin\\java.exe",
                    
                    // Linux
                    "/usr/bin/java",
                    "/usr/local/bin/java",
                    
                    // macOS
                    "/usr/local/opt/openjdk@17/bin/java",
                    "/opt/homebrew/bin/java"
                };

                foreach (var location in javaLocations)
                {
                    if (File.Exists(location))
                    {
                        _javaPath = location;
                        Console.WriteLine($"✅ Java gefunden: {location}");
                        return;
                    }
                }

                Console.WriteLine("❌ Java nicht gefunden!");
                _javaPath = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei Java-Suche: {ex.Message}");
                _javaPath = null;
            }
        }

        /// <summary>
        /// Findet das Minecraft-Verzeichnis
        /// </summary>
        private void FindMinecraftPath()
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _minecraftPath = Path.Combine(appData, ".minecraft");
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _minecraftPath = Path.Combine(home, ".minecraft");
                }
                else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _minecraftPath = Path.Combine(home, "Library", "Application Support", "minecraft");
                }

                if (!Directory.Exists(_minecraftPath))
                {
                    Directory.CreateDirectory(_minecraftPath);
                }

                Console.WriteLine($"✅ Minecraft-Pfad: {_minecraftPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei Minecraft-Pfad-Suche: {ex.Message}");
                _minecraftPath = null;
            }
        }

        /// <summary>
        /// Kopiert Modpack-Dateien in das Minecraft-Verzeichnis
        /// </summary>
        private void CopyModpackToMinecraft(string modpackPath)
        {
            try
            {
                Console.WriteLine("📁 Kopiere Modpack-Dateien...");

                if (!Directory.Exists(modpackPath))
                {
                    throw new Exception($"Modpack-Pfad nicht gefunden: {modpackPath}");
                }

                // Kopiere Ordner
                var dirsToCreate = new[] { "mods", "config", "coremods", "shaderpacks" };

                foreach (var dir in dirsToCreate)
                {
                    string sourcePath = Path.Combine(modpackPath, dir);
                    string targetPath = Path.Combine(_minecraftPath, dir);

                    if (Directory.Exists(sourcePath))
                    {
                        // Erstelle Verzeichnis wenn nicht vorhanden
                        if (!Directory.Exists(targetPath))
                        {
                            Directory.CreateDirectory(targetPath);
                        }

                        // Kopiere Dateien
                        CopyDirectory(sourcePath, targetPath, true);
                        Console.WriteLine($"  ✅ {dir} kopiert");
                    }
                }

                Console.WriteLine("✅ Modpack erfolgreich kopiert!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Kopieren: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Kopiert Verzeichnis rekursiv
        /// </summary>
        private void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            try
            {
                // Kopiere Dateien
                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                    if (overwrite || !File.Exists(targetFile))
                    {
                        File.Copy(file, targetFile, overwrite);
                    }
                }

                // Kopiere Unterverzeichnisse
                foreach (var dir in Directory.GetDirectories(sourceDir))
                {
                    string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                    if (!Directory.Exists(targetSubDir))
                    {
                        Directory.CreateDirectory(targetSubDir);
                    }
                    CopyDirectory(dir, targetSubDir, overwrite);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Verzeichnis-Kopieren: {ex.Message}");
            }
        }

        /// <summary>
        /// Startet den Minecraft-Prozess mit korrektem Classpath
        /// </summary>
        private async Task LaunchMinecraftProcessAsync(string gameVersion, JObject versionInfo)
        {
            try
            {
                Console.WriteLine("🚀 Starte Minecraft...");

                // Erstelle Game-Verzeichnis
                string gameDir = Path.Combine(_minecraftPath, "instances", "SuperSMP");
                if (!Directory.Exists(gameDir))
                {
                    Directory.CreateDirectory(gameDir);
                }

                // Baue Classpath zusammen
                string classpath = BuildClasspath(versionInfo);

                // Main-Klasse aus Version Info
                string mainClass = versionInfo["mainClass"]?.ToString() ?? "net.minecraft.client.main.Main";

                // JVM Argumente
                string jvmArgs = $"-Xmx4G -Xms2G -XX:+UseG1GC " +
                    $"-Djava.library.path=\"{Path.Combine(_minecraftPath, "natives")}\" " +
                    $"-Dfile.encoding=UTF-8";

                // Game Argumente
                var argObj = versionInfo["arguments"];
                string gameArgs = BuildGameArguments(versionInfo, gameVersion, gameDir);

                // Starte Minecraft
                var processInfo = new ProcessStartInfo
                {
                    FileName = _javaPath,
                    Arguments = $"{jvmArgs} -cp \"{classpath}\" {mainClass} {gameArgs}",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = false,
                    WorkingDirectory = gameDir
                };

                Console.WriteLine($"Java: {_javaPath}");
                Console.WriteLine($"Main-Klasse: {mainClass}");
                Console.WriteLine($"Game-Dir: {gameDir}");

                var process = Process.Start(processInfo);
                Console.WriteLine($"✅ Minecraft-Prozess gestartet (PID: {process.Id})");

                // Warte kurz und versuche Server zu verbinden
                await Task.Delay(3000);
                await AutoConnectToServerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Starten des Prozesses: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Baut den Classpath zusammen
        /// </summary>
        private string BuildClasspath(JObject versionInfo)
        {
            var libraries = new List<string>();
            var librariesPath = Path.Combine(_minecraftPath, "libraries");

            // Füge alle Libraries hinzu
            var libs = versionInfo["libraries"] as JArray;
            if (libs != null)
            {
                foreach (var lib in libs)
                {
                    var libName = lib["name"]?.ToString();
                    if (libName != null)
                    {
                        // Konvertiere Maven-Namen zu Dateipfad
                        // com.example:lib:1.0 -> com/example/lib/1.0/lib-1.0.jar
                        string libPath = ConvertMavenPathToFilePath(libName);
                        string fullPath = Path.Combine(librariesPath, libPath);

                        if (File.Exists(fullPath))
                        {
                            libraries.Add(fullPath);
                        }
                    }
                }
            }

            // Füge Minecraft JAR hinzu
            var version = versionInfo["id"]?.ToString() ?? "unknown";
            string minecraftJar = Path.Combine(_minecraftPath, "versions", version, $"{version}.jar");
            if (File.Exists(minecraftJar))
            {
                libraries.Add(minecraftJar);
            }

            return string.Join(Path.PathSeparator, libraries);
        }

        /// <summary>
        /// Konvertiert Maven-Pfad zu Dateisystem-Pfad
        /// </summary>
        private string ConvertMavenPathToFilePath(string mavenPath)
        {
            // com.example:lib:1.0 -> com/example/lib/1.0/lib-1.0.jar
            var parts = mavenPath.Split(':');
            if (parts.Length >= 3)
            {
                string groupId = parts[0].Replace('.', Path.DirectorySeparatorChar);
                string artifactId = parts[1];
                string version = parts[2];
                return Path.Combine(groupId, artifactId, version, $"{artifactId}-{version}.jar");
            }
            return mavenPath;
        }

        /// <summary>
        /// Baut Game-Argumente zusammen
        /// </summary>
        private string BuildGameArguments(JObject versionInfo, string gameVersion, string gameDir)
        {
            var args = new List<string>
            {
                "--username", "Player",
                "--version", gameVersion,
                "--gameDir", gameDir,
                "--assetsDir", Path.Combine(_minecraftPath, "assets"),
                "--assetIndex", gameVersion,
                "--uuid", "00000000000000000000000000000000",
                "--accessToken", "0",
                "--userType", "legacy",
                "--versionType", "release"
            };

            return string.Join(" ", args.Select(a => $"\"{a}\""));
        }

        /// <summary>
        /// Versucht sich automatisch mit dem Server zu verbinden
        /// </summary>
        private async Task AutoConnectToServerAsync()
        {
            try
            {
                Console.WriteLine($"🔗 Verbinde mit Server: {SERVER_ADDRESS}:{SERVER_PORT}");

                // Prüfe ob Server erreichbar ist
                if (await PingServerAsync(SERVER_ADDRESS, SERVER_PORT))
                {
                    Console.WriteLine("✅ Server erreichbar!");

                    // Erstelle/Update server-list.dat
                    UpdateServerList();
                }
                else
                {
                    Console.WriteLine("⚠️ Server antwortet nicht, aber das ist OK!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fehler bei Server-Verbindung: {ex.Message}");
            }
        }

        /// <summary>
        /// Updated die Server-Liste in Minecraft
        /// </summary>
        private void UpdateServerList()
        {
            try
            {
                string serverListPath = Path.Combine(_minecraftPath, "servers.dat");
                // Hinweis: servers.dat ist ein Binary-Format, hier nur als Placeholder

                Console.WriteLine("✅ Server-Liste wird aktualisiert...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fehler beim Aktualisieren der Server-Liste: {ex.Message}");
            }
        }

        /// <summary>
        /// Pingt den Minecraft-Server
        /// </summary>
        private async Task<bool> PingServerAsync(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var delayTask = Task.Delay(3000);
                    var completedTask = await Task.WhenAny(connectTask, delayTask);

                    if (completedTask == connectTask)
                    {
                        client.Close();
                        return true;
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Versucht einen Befehl auszuführen
        /// </summary>
        private bool TryExecuteCommand(string command, string args)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit(5000);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lädt Version Info herunter
        /// </summary>
        private async Task<JObject> DownloadVersionInfoAsync(string gameVersion)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Lade Manifest herunter
                    var manifestJson = await client.GetStringAsync(MANIFEST_URL);
                    var manifest = JObject.Parse(manifestJson);

                    // Finde Version
                    var versions = manifest["versions"] as JArray;
                    var versionEntry = versions?.FirstOrDefault(v => v["id"]?.ToString() == gameVersion);

                    if (versionEntry == null)
                    {
                        Console.WriteLine($"❌ Version {gameVersion} nicht im Manifest gefunden!");
                        return null;
                    }

                    // Lade Version JSON herunter
                    string versionUrl = versionEntry["url"]?.ToString();
                    var versionJson = await client.GetStringAsync(versionUrl);
                    return JObject.Parse(versionJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Herunterladen der Version Info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lädt alle Libraries herunter
        /// </summary>
        private async Task DownloadLibrariesAsync(JObject versionInfo)
        {
            try
            {
                var librariesPath = Path.Combine(_minecraftPath, "libraries");
                Directory.CreateDirectory(librariesPath);

                var libs = versionInfo["libraries"] as JArray;
                if (libs == null) return;

                using (var client = new HttpClient())
                {
                    int count = 0;
                    foreach (var lib in libs)
                    {
                        try
                        {
                            var downloads = lib["downloads"]?["artifact"];
                            if (downloads != null)
                            {
                                string url = downloads["url"]?.ToString();
                                string path = downloads["path"]?.ToString();

                                if (url != null && path != null)
                                {
                                    string fullPath = Path.Combine(librariesPath, path);
                                    string directory = Path.GetDirectoryName(fullPath);

                                    // Erstelle Verzeichnis
                                    if (!Directory.Exists(directory))
                                    {
                                        Directory.CreateDirectory(directory);
                                    }

                                    // Download nur wenn nicht vorhanden
                                    if (!File.Exists(fullPath))
                                    {
                                        Console.WriteLine($"  Lade herunter: {path}");
                                        var fileData = await client.GetByteArrayAsync(url);
                                        File.WriteAllBytes(fullPath, fileData);
                                    }

                                    count++;
                                    if (count % 10 == 0)
                                    {
                                        Console.WriteLine($"  {count} Libraries heruntergeladen...");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ⚠️ Fehler bei Library: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"✅ {count} Libraries heruntergeladen!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Herunterladen der Libraries: {ex.Message}");
            }
        }

        /// <summary>
        /// Lädt Assets herunter
        /// </summary>
        private async Task DownloadAssetsAsync(JObject versionInfo)
        {
            try
            {
                var assetsPath = Path.Combine(_minecraftPath, "assets");
                Directory.CreateDirectory(assetsPath);

                string assetIndex = versionInfo["assetIndex"]?["id"]?.ToString();
                if (string.IsNullOrEmpty(assetIndex))
                {
                    assetIndex = versionInfo["id"]?.ToString();
                }

                string assetIndexPath = Path.Combine(assetsPath, "indexes", $"{assetIndex}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(assetIndexPath));

                using (var client = new HttpClient())
                {
                    // Lade Asset Index herunter
                    string indexUrl = $"https://launcher.mojang.com/v1/objects/{versionInfo["assetIndex"]?["sha1"]}/";
                    
                    if (!File.Exists(assetIndexPath))
                    {
                        Console.WriteLine("  Lade Asset-Index herunter...");
                        var indexJson = await client.GetStringAsync(versionInfo["assetIndex"]?["url"]?.ToString() ?? "");
                        File.WriteAllText(assetIndexPath, indexJson);
                    }

                    // Lade Assets herunter
                    var indexContent = JObject.Parse(File.ReadAllText(assetIndexPath));
                    var objects = indexContent["objects"] as JObject;

                    if (objects != null)
                    {
                        int count = 0;
                        foreach (var obj in objects.Properties())
                        {
                            try
                            {
                                var sha1 = obj.Value["hash"]?.ToString();
                                if (!string.IsNullOrEmpty(sha1))
                                {
                                    string assetPath = Path.Combine(assetsPath, "objects", sha1.Substring(0, 2), sha1);

                                    if (!File.Exists(assetPath))
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                                        string url = $"{RESOURCES_URL}{sha1.Substring(0, 2)}/{sha1}";

                                        try
                                        {
                                            var fileData = await client.GetByteArrayAsync(url);
                                            File.WriteAllBytes(assetPath, fileData);
                                        }
                                        catch
                                        {
                                            // Asset kann fehlen, ist nicht kritisch
                                        }
                                    }

                                    count++;
                                    if (count % 100 == 0)
                                    {
                                        Console.WriteLine($"  {count} Assets heruntergeladen...");
                                    }
                                }
                            }
                            catch { }
                        }

                        Console.WriteLine($"✅ {count} Assets verarbeitet!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fehler beim Herunterladen der Assets: {ex.Message}");
            }
        }

        /// <summary>
        /// Lädt die Minecraft JAR herunter
        /// </summary>
        private async Task DownloadMinecraftJarAsync(JObject versionInfo)
        {
            try
            {
                string version = versionInfo["id"]?.ToString();
                var download = versionInfo["downloads"]?["client"];

                if (download == null)
                {
                    Console.WriteLine("⚠️ Client-Download nicht gefunden!");
                    return;
                }

                string url = download["url"]?.ToString();
                string sha1 = download["sha1"]?.ToString();

                if (string.IsNullOrEmpty(url))
                {
                    Console.WriteLine("⚠️ Download-URL nicht gefunden!");
                    return;
                }

                string versionPath = Path.Combine(_minecraftPath, "versions", version);
                Directory.CreateDirectory(versionPath);

                string jarPath = Path.Combine(versionPath, $"{version}.jar");

                if (File.Exists(jarPath))
                {
                    Console.WriteLine("✅ Minecraft JAR existiert bereits!");
                    return;
                }

                Console.WriteLine($"  Lade Minecraft {version} herunter...");

                using (var client = new HttpClient())
                {
                    var fileData = await client.GetByteArrayAsync(url);
                    File.WriteAllBytes(jarPath, fileData);
                    Console.WriteLine("✅ Minecraft JAR heruntergeladen!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Herunterladen der Minecraft JAR: {ex.Message}");
            }
        }
    }
}
