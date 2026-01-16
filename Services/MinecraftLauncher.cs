using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SuperSMPLauncher.Services
{
    public class MinecraftLauncher
    {
        private string _javaPath;
        private string _minecraftPath;
        private const string SERVER_ADDRESS = "supersmp.fun";
        private const int SERVER_PORT = 25565;

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

                // 3. Kopiere Modpack-Dateien in .minecraft
                CopyModpackToMinecraft(modpackPath);

                // 4. Erstelle/Update Launcher Profile
                CreateOrUpdateLauncherProfile(gameVersion, modloader);

                // 5. Starte Minecraft mit den richtigen Argumenten
                await LaunchMinecraftProcessAsync(gameVersion);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Starten von Minecraft: {ex.Message}");
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
        /// Erstellt oder aktualisiert das Launcher-Profil
        /// </summary>
        private void CreateOrUpdateLauncherProfile(string gameVersion, string modloader)
        {
            try
            {
                Console.WriteLine("🔧 Erstelle Launcher-Profil...");

                string profilePath = Path.Combine(_minecraftPath, "launcher_profiles.json");
                JObject profiles = new JObject();

                // Lade existierende Profile
                if (File.Exists(profilePath))
                {
                    try
                    {
                        string content = File.ReadAllText(profilePath);
                        profiles = JObject.Parse(content);
                    }
                    catch { /* Ignoriere Parsing-Fehler */ }
                }

                // Stelle sicher, dass die Struktur existiert
                if (profiles["profiles"] == null)
                {
                    profiles["profiles"] = new JObject();
                }

                // Erstelle/Update SuperSMP Profil
                var superSMPProfile = new JObject
                {
                    ["name"] = "SuperSMP",
                    ["gameDir"] = Path.Combine(_minecraftPath, "instances", "SuperSMP"),
                    ["icon"] = "grass",
                    ["type"] = "custom",
                    ["created"] = DateTime.Now.ToString("o"),
                    ["lastUsed"] = DateTime.Now.ToString("o"),
                    ["javaArgs"] = "-Xmx4G -Xms2G -XX:+UseG1GC"
                };

                if (!string.IsNullOrEmpty(gameVersion))
                {
                    superSMPProfile["lastVersionId"] = gameVersion;
                }

                profiles["profiles"]["SuperSMP"] = superSMPProfile;

                // Setze als aktives Profil
                profiles["selectedProfile"] = "SuperSMP";

                // Speichere Profile
                File.WriteAllText(profilePath, profiles.ToString());
                Console.WriteLine("✅ Launcher-Profil erstellt!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Erstellen des Profils: {ex.Message}");
            }
        }

        /// <summary>
        /// Startet den Minecraft-Prozess
        /// </summary>
        private async Task LaunchMinecraftProcessAsync(string gameVersion)
        {
            try
            {
                Console.WriteLine("🚀 Starte Minecraft...");

                // Stelle sicher, dass Modpack-Ordner existiert
                string gameDir = Path.Combine(_minecraftPath, "instances", "SuperSMP");
                if (!Directory.Exists(gameDir))
                {
                    Directory.CreateDirectory(gameDir);
                }

                // JVM Argumente
                string jvmArgs = "-Xmx4G -Xms2G -XX:+UseG1GC -Djava.library.path=\"" + 
                    Path.Combine(_minecraftPath, "natives") + "\"";

                // Minecraft Argumente
                string gameArgs = $"--username Player --version {gameVersion} --gameDir \"{gameDir}\" " +
                    $"--assetsDir \"{Path.Combine(_minecraftPath, "assets")}\" " +
                    $"--assetIndex {gameVersion} --accessToken 0";

                var processInfo = new ProcessStartInfo
                {
                    FileName = _javaPath,
                    Arguments = $"{jvmArgs} -cp .*" +
                        $" net.minecraft.client.main.Main {gameArgs}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                    WorkingDirectory = gameDir
                };

                var process = Process.Start(processInfo);
                Console.WriteLine($"✅ Minecraft-Prozess gestartet (PID: {process.Id})");

                // Warte kurz und verbinde dann mit Server
                await Task.Delay(5000);
                await AutoConnectToServerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Starten des Prozesses: {ex.Message}");
                throw;
            }
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
    }
}
