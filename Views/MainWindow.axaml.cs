using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using Avalonia.Input.Platform;
using Microsoft.Win32;
using SuperSMPLauncher.Services;
using SuperSMPLauncher.Utils;
using SuperSMPLauncher.Models;

namespace SuperSMPLauncher.Views
{
    public partial class MainWindow : Window
    {
        private readonly ModpackDownloader _downloader = new ModpackDownloader();
        private readonly ModrinthApi _api = new ModrinthApi();
        private readonly MinecraftLauncher _minecraftLauncher = new MinecraftLauncher();
        private readonly MinecraftInstaller _minecraftInstaller = new MinecraftInstaller();
        private bool _isDownloading = false;
        private bool _isLoadingVersions = false;
        private List<string> _availableMinecraftVersions = new List<string>();
        private string _systemMinecraftPath = null;
        private string _modpackPath = null;
        private string _selectedGameVersion = null;
        private string _selectedModloader = null;

        public MainWindow()
        {
            InitializeComponent();
            
            // Standardwerte setzen
            ShaderComboBox.SelectedIndex = 0;
            ModloaderComboBox.SelectedIndex = 0;
            MinecraftVersionComboBox.SelectedIndex = 0;
            LCFComboBox.SelectedIndex = 0;
            
            // Event-Handler
            DownloadButton.Click += OnDownloadButtonClick;
            StartMinecraftButton.Click += OnStartMinecraftButtonClick;
            
            // SUCHT INTENSIVER nach Minecraft
            FindSystemMinecraftComprehensive();
            
            // Lade Minecraft-Versionen beim Start
            LoadMinecraftVersionsAsync();
            
            // Prüfe ob Modpack bereits installiert ist
            CheckIfModpackInstalled();
        }

        private async void LoadMinecraftVersionsAsync()
        {
            if (_isLoadingVersions) return;
            
            _isLoadingVersions = true;
            LoadingVersionsPanel.IsVisible = true;
            LoadingPanel.IsVisible = true;
            MinecraftVersionComboBox.IsEnabled = false;
            StatusText.Text = "Lade verfügbare Minecraft-Versionen...";

            try
            {
                var versions = await _api.GetVersionsAsync("EDFggNY3");

                if (versions != null && versions.Length > 0)
                {
                    var allMinecraftVersions = new HashSet<string>();
                    
                    foreach (var version in versions)
                    {
                        if (version.GameVersions != null)
                        {
                            foreach (var gameVersion in version.GameVersions)
                            {
                                if (!string.IsNullOrWhiteSpace(gameVersion))
                                {
                                    allMinecraftVersions.Add(gameVersion.Trim());
                                }
                            }
                        }
                    }

                    _availableMinecraftVersions = allMinecraftVersions
                        .OrderByDescending(v => ParseVersion(v))
                        .ToList();

                    UpdateVersionDropdown();
                    
                    StatusText.Text = $"✅ {_availableMinecraftVersions.Count} Minecraft-Versionen verfügbar.";
                }
                else
                {
                    SetDefaultVersions();
                    StatusText.Text = "⚠️ Standardversionen geladen.";
                }
            }
            catch (Exception ex)
            {
                SetDefaultVersions();
                StatusText.Text = $"⚠️ Fehler: {ex.Message}. Standardversionen geladen.";
            }
            finally
            {
                _isLoadingVersions = false;
                LoadingVersionsPanel.IsVisible = false;
                LoadingPanel.IsVisible = false;
                MinecraftVersionComboBox.IsEnabled = true;
            }
        }

        private void UpdateVersionDropdown()
        {
            MinecraftVersionComboBox.Items.Clear();
            MinecraftVersionComboBox.Items.Add("(Neueste Version)");
            
            foreach (var version in _availableMinecraftVersions)
            {
                MinecraftVersionComboBox.Items.Add(version);
            }
            
            MinecraftVersionComboBox.SelectedIndex = 0;
        }

        private void SetDefaultVersions()
        {
            _availableMinecraftVersions = new List<string> 
            { 
                "1.20.1", "1.20", "1.19.4", "1.19.2", "1.18.2", "1.17.1", "1.16.5" 
            };
            
            UpdateVersionDropdown();
        }

        private System.Version ParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return new System.Version(0, 0, 0);
            
            var clean = versionString.Trim().TrimStart('v', 'V');
            var withoutBuild = clean.Split('+')[0];
            var withoutPreRelease = withoutBuild.Split('-')[0];
            
            if (System.Version.TryParse(withoutPreRelease, out var version))
                return version;
            
            return new System.Version(0, 0, 0);
        }

        private void FindSystemMinecraftComprehensive()
        {
            try
            {
                Console.WriteLine("=== INTENSIVE MINECRAFT-SUCHE ===");
                
                // Liste aller möglichen Minecraft-Pfade
                var allPossiblePaths = new List<string>();
                
                // 1. Standard .minecraft Pfade
                allPossiblePaths.AddRange(GetStandardMinecraftPaths());
                
                // 2. Windows-spezifische Pfade
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    allPossiblePaths.AddRange(GetWindowsMinecraftPaths());
                }
                
                // 3. Launcher-basierte Pfade
                allPossiblePaths.AddRange(GetLauncherBasedPaths());
                
                // 4. Benutzerdefinierte Pfade
                allPossiblePaths.AddRange(GetCustomMinecraftPaths());
                
                // Jeden Pfad überprüfen
                foreach (var path in allPossiblePaths.Distinct())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            // Prüfe ob es wirklich ein Minecraft-Ordner ist
                            if (IsValidMinecraftFolder(path))
                            {
                                _systemMinecraftPath = path;
                                Console.WriteLine($"✅ MINECRAFT GEFUNDEN: {path}");
                                return;
                            }
                        }
                    }
                    catch { /* Weiter mit nächstem Pfad */ }
                }
                
                Console.WriteLine("❌ Kein Minecraft-Ordner gefunden!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei Minecraft-Suche: {ex.Message}");
            }
        }

        private List<string> GetStandardMinecraftPaths()
        {
            var paths = new List<string>();
            
            // Standard AppData Pfad (häufigster)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            paths.Add(Path.Combine(appData, ".minecraft"));
            
            // Linux/Mac Pfade
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                paths.Add(Path.Combine(home, ".minecraft"));
                paths.Add(Path.Combine(home, "Library", "Application Support", "minecraft"));
            }
            
            return paths;
        }

        private List<string> GetWindowsMinecraftPaths()
        {
            var paths = new List<string>();
            
            try
            {
                // Windows spezifische Pfade
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                
                // Standard Windows Pfade
                paths.Add(Path.Combine(userProfile, "AppData", "Roaming", ".minecraft"));
                
                // Microsoft Store Version
                paths.Add(Path.Combine(localAppData, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraft"));
                
                // Andere mögliche Pfade
                paths.Add("C:\\Users\\" + Environment.UserName + "\\AppData\\Roaming\\.minecraft");
                paths.Add("D:\\Users\\" + Environment.UserName + "\\AppData\\Roaming\\.minecraft");
                paths.Add("C:\\Minecraft\\.minecraft");
                paths.Add("D:\\Minecraft\\.minecraft");
                
                // Durchsuche Registry
                var registryPaths = GetMinecraftPathsFromRegistry();
                paths.AddRange(registryPaths);
                
                // Suche nach Minecraft in allen Laufwerken
                try
                {
                    DriveInfo[] drives = DriveInfo.GetDrives();
                    foreach (DriveInfo drive in drives)
                    {
                        if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                        {
                            string potentialPath = Path.Combine(drive.Name, "Users", Environment.UserName, "AppData", "Roaming", ".minecraft");
                            paths.Add(potentialPath);
                            
                            potentialPath = Path.Combine(drive.Name, "Minecraft", ".minecraft");
                            paths.Add(potentialPath);
                        }
                    }
                }
                catch { /* Ignore drive errors */ }
            }
            catch { /* Ignore Windows specific errors */ }
            
            return paths;
        }

        private List<string> GetMinecraftPathsFromRegistry()
        {
            var paths = new List<string>();
            
            try
            {
                // Verschiedene Registry-Schlüssel durchsuchen
                var registryKeys = new[]
                {
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders",
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"
                };
                
                foreach (var keyPath in registryKeys)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            foreach (var valueName in key.GetValueNames())
                            {
                                var value = key.GetValue(valueName)?.ToString();
                                if (!string.IsNullOrEmpty(value) && 
                                    (value.Contains("minecraft", StringComparison.OrdinalIgnoreCase) || 
                                     value.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) ||
                                     valueName.Contains("AppData") || 
                                     valueName.Contains("LocalAppData")))
                                {
                                    if (value.Contains("Roaming") || value.Contains("AppData"))
                                    {
                                        string potentialPath = Path.Combine(value, ".minecraft");
                                        paths.Add(potentialPath);
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Suche nach Minecraft in Installations-Pfaden
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            if (subKeyName.Contains("Minecraft", StringComparison.OrdinalIgnoreCase))
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrEmpty(installLocation))
                                    {
                                        paths.Add(Path.Combine(installLocation, ".minecraft"));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Ignore registry errors */ }
            
            return paths;
        }

        private List<string> GetLauncherBasedPaths()
        {
            var paths = new List<string>();
            
            try
            {
                // Suche nach Minecraft Launcher
                var launcherPaths = new[]
                {
                    // Windows
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "MinecraftLauncher.exe"),
                    "C:\\Program Files (x86)\\Minecraft Launcher\\MinecraftLauncher.exe",
                    "C:\\Program Files\\Minecraft Launcher\\MinecraftLauncher.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Minecraft", "Minecraft.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Minecraft.lnk"),
                    
                    // Linux
                    "/usr/bin/minecraft-launcher",
                    "/usr/local/bin/minecraft-launcher",
                    
                    // Mac
                    "/Applications/Minecraft.app/Contents/MacOS/launcher",
                };
                
                foreach (var launcherPath in launcherPaths)
                {
                    try
                    {
                        if (File.Exists(launcherPath) || (launcherPath.EndsWith(".lnk") && File.Exists(launcherPath)))
                        {
                            // Versuche Pfad vom Launcher abzuleiten
                            string launcherDir = Path.GetDirectoryName(launcherPath);
                            if (!string.IsNullOrEmpty(launcherDir))
                            {
                                // Versuche verschiedene Pfade
                                paths.Add(Path.Combine(launcherDir, ".minecraft"));
                                paths.Add(Path.Combine(Path.GetDirectoryName(launcherDir), ".minecraft"));
                                
                                // Für Windows Store Version
                                if (launcherDir.Contains("Packages"))
                                {
                                    string potentialPath = Path.Combine(launcherDir, "LocalState", "games", "com.mojang", "minecraft");
                                    paths.Add(potentialPath);
                                }
                            }
                        }
                    }
                    catch { /* Continue */ }
                }
            }
            catch { /* Ignore launcher errors */ }
            
            return paths;
        }

        private List<string> GetCustomMinecraftPaths()
        {
            var paths = new List<string>();
            
            // Frage den Benutzer nach seinem Minecraft-Pfad (falls gespeichert)
            string configFile = "minecraft_path.txt";
            if (File.Exists(configFile))
            {
                string savedPath = File.ReadAllText(configFile).Trim();
                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                {
                    paths.Add(savedPath);
                }
            }
            
            // Prüfe auf MultiMC/PrismLauncher Installationen
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.Add(Path.Combine(home, "MultiMC", "instances"));
            paths.Add(Path.Combine(home, "PrismLauncher", "instances"));
            paths.Add(Path.Combine(home, ".local", "share", "multimc", "instances"));
            
            return paths;
        }

        private bool IsValidMinecraftFolder(string path)
        {
            try
            {
                // Prüfe auf wichtige Minecraft-Dateien/Ordner
                bool hasVersions = Directory.Exists(Path.Combine(path, "versions"));
                bool hasLauncherProfiles = File.Exists(Path.Combine(path, "launcher_profiles.json"));
                bool hasAssets = Directory.Exists(Path.Combine(path, "assets"));
                bool hasLibraries = Directory.Exists(Path.Combine(path, "libraries"));
                
                // Mindestens eines dieser Kriterien muss erfüllt sein
                return hasVersions || hasLauncherProfiles || hasAssets || hasLibraries;
            }
            catch
            {
                return false;
            }
        }

        private async void OnDownloadButtonClick(object sender, RoutedEventArgs e)
        {
            if (_isDownloading) return;
            
            _isDownloading = true;
            DownloadButton.IsEnabled = false;
            DownloadingPanel.IsVisible = true;
            UpdateButtonText();
            StatusText.Text = "Starte Download...";

            try
            {
                var shaderOption = GetShaderOption();
                var modloader = GetModloader();
                var LCfOption = GetLunarClientFeatures();
                
                var selectedContent = MinecraftVersionComboBox.SelectedItem?.ToString();
                string minecraftVersion;
                
                // Speichere die ausgewählten Optionen
                _selectedModloader = modloader;
                
                if (selectedContent == "(Neueste Version)" || string.IsNullOrEmpty(selectedContent))
                {
                    minecraftVersion = GetSelectedMinecraftVersion();
                    _selectedGameVersion = minecraftVersion;
                    StatusText.Text = $"⏳ Lade {shaderOption.ToLower()} für {modloader} ({LCfOption}) (Minecraft {minecraftVersion})...";
                }
                else
                {
                    minecraftVersion = selectedContent;
                    _selectedGameVersion = minecraftVersion;
                    StatusText.Text = $"⏳ Lade {shaderOption.ToLower()} für {modloader} ({LCfOption}) (Minecraft {minecraftVersion})...";
                }

                string zipPath;
                
                if (string.IsNullOrEmpty(minecraftVersion))
                {
                    zipPath = await _downloader.DownloadLatestForLoaderShaderAndLCfAsync(
                        projectId: "EDFggNY3",
                        modloader: modloader,
                        shaderOption: shaderOption,
                        LCfOption: LCfOption,
                        outputDir: "modpack"
                    );
                }
                else
                {
                    zipPath = await _downloader.DownloadLatestForLoaderMinecraftShaderAndLCfAsync(
                        projectId: "EDFggNY3",
                        modloader: modloader,
                        minecraftVersion: minecraftVersion,
                        shaderOption: shaderOption,
                        LCfOption: LCfOption,
                        outputDir: "modpack"
                    );
                }

                StatusText.Text = "⏳ Extrahiere Modpack...";
                
                // Modpack in versions Ordner extrahieren
                string versionsPath = "versions";
                string modpackName = $"SuperSMP_{DateTime.Now:yyyyMMdd_HHmmss}";
                _modpackPath = Path.Combine(versionsPath, modpackName);
                
                // Alten Ordner löschen falls vorhanden
                if (Directory.Exists(_modpackPath))
                {
                    try
                    {
                        Directory.Delete(_modpackPath, true);
                    }
                    catch { /* Ignore */ }
                }
                
                // Stelle sicher, dass versions Ordner existiert
                Directory.CreateDirectory(versionsPath);
                
                ZipExtractor.Extract(zipPath, _modpackPath);

                StatusText.Text = "⏳ Erstelle Installationsanleitung...";
                CreateInstallationGuide(_modpackPath);

                StatusText.Text = $"✅ {shaderOption} erfolgreich heruntergeladen!\n\n🚀 Klicke 'Minecraft Starten' um das Spiel zu launchen!";
                StartMinecraftButton.IsEnabled = true;
                
                // Zeige Minecraft Status an
                if (!string.IsNullOrEmpty(_systemMinecraftPath))
                {
                    StatusText.Text += $"\n✅ Minecraft gefunden in: {_systemMinecraftPath}";
                }
                else
                {
                    StatusText.Text += "\n⚠️ Minecraft-Pfad wird automatisch konfiguriert.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Fehler: {ex.Message}";
            }
            finally
            {
                _isDownloading = false;
                DownloadButton.IsEnabled = true;
                DownloadingPanel.IsVisible = false;
                UpdateButtonText();
            }
        }

        private void CreateInstallationGuide(string modpackPath)
        {
            try
            {
                string guidePath = Path.Combine(modpackPath, "INSTALLATION.txt");
                
                string guideContent = $@"SUPERSMP MODPACK - INSTALLATIONSANLEITUNG
====================================================

MODPACK INFORMATIONEN:
- Heruntergeladen: {DateTime.Now:dd.MM.yyyy HH:mm}
- Ort: {Path.GetFullPath(modpackPath)}
- Minecraft gefunden: {(!string.IsNullOrEmpty(_systemMinecraftPath) ? "JA" : "NEIN")}
{(string.IsNullOrEmpty(_systemMinecraftPath) ? "" : $"- Minecraft Pfad: {_systemMinecraftPath}")}

SO INSTALLIERST DU DAS MODPACK:
================================

OPTION A: MultiMC/PrismLauncher (EMPFEHLENSWERT)
------------------------------------------------
1. Lade MultiMC oder PrismLauncher herunter
2. Öffne den Launcher
3. Klicke 'Add Instance' → 'Import from zip'
4. Gehe zum Ordner: {Path.GetFullPath("modpack")}
5. Wähle die .mrpack oder .zip Datei
6. Klicke OK und starte Minecraft

OPTION B: Manuelle Installation in .minecraft
---------------------------------------------
1. Öffne deinen .minecraft Ordner:
   - WINDOWS: %APPDATA%\.minecraft
   - Drücke WIN+R, gib %appdata% ein, dann .minecraft
   
2. Kopiere den Inhalt dieses Ordners in deinen .minecraft Ordner:
   - Wenn es einen '.minecraft' Ordner gibt: Kopiere ALLE Dateien daraus
   - Wenn es 'mods' und 'config' Ordner gibt: Kopiere diese
   - Überschreibe vorhandene Dateien wenn nötig

3. Starte Minecraft Launcher
4. Wähle Fabric oder Forge (je nach Auswahl)
5. Klicke 'Play'

OPTION C: CurseForge/Overwolf
-----------------------------
1. Öffne CurseForge App
2. Klicke 'Create Custom Profile'
3. Importiere die Datei aus 'modpack' Ordner
4. Wähle Minecraft Version
5. Klicke 'Install'

SO VERBINDEST DU DICH MIT DEM SERVER:
=====================================
1. Starte Minecraft MIT dem Modpack
2. Gehe zu 'Multiplayer'
3. Klicke 'Add Server'
4. Gib ein:
   - Server Name: SuperSMP
   - Server Address: supersmp.fun
5. Klicke 'Join Server'

WICHTIGE DATEIEN IN DIESEM ORDNER:
----------------------------------";

                try
                {
                    var importantFiles = Directory.GetFiles(modpackPath, "*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".jar") || f.EndsWith(".mrpack") || f.EndsWith(".json") || 
                               f.Contains("modlist") || f.Contains("readme", StringComparison.OrdinalIgnoreCase))
                        .Take(15);
                    
                    foreach (var file in importantFiles)
                    {
                        string relativePath = file.Substring(modpackPath.Length + 1);
                        guideContent += $"\n- {relativePath}";
                    }
                }
                catch { /* Ignore file listing errors */ }
                
                guideContent += $@"

SERVER INFORMATIONEN:
- IP: supersmp.fun
- Port: 25565
- Website: https://github.com/FreetimeMaker/SuperSMPLauncher1

BEI PROBLEMEN:
-------------
1. Stelle sicher, dass Java 17+ installiert ist
2. Installiere Fabric/Forge für die richtige Minecraft Version
3. Lösche den 'mods' Ordner und versuche es erneut
4. Prüfe ob alle Dateien korrekt kopiert wurden";
                
                File.WriteAllText(guidePath, guideContent, Encoding.UTF8);
                
                // Erstelle auch eine einfache Batch-Datei für Windows
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    string batPath = Path.Combine(modpackPath, "INSTALL_WINDOWS.bat");
                    string batContent = $@"@echo off
echo ========================================
echo SUPERSMP MODPACK - WINDOWS INSTALLATION
echo ========================================
echo.
echo Modpack heruntergeladen in:
echo {Path.GetFullPath(modpackPath)}
echo.
echo So installierst du es:
echo.
echo 1. Öffne diesen Ordner im Explorer
echo    (Rechtsklick -> Öffnen im Explorer)
echo.
echo 2. Kopiere den Inhalt
echo    - Drücke STRG+A (Alles auswählen)
echo    - Drücke STRG+C (Kopieren)
echo.
echo 3. Öffne deinen .minecraft Ordner
echo    - Drücke WIN+R
echo    - Gib ein: %appdata%\.minecraft
echo    - Drücke Enter
echo.
echo 4. Füge die Dateien ein
echo    - Drücke STRG+V (Einfügen)
echo    - Bestätige Überschreiben wenn gefragt
echo.
echo 5. Starte Minecraft
echo    - Öffne Minecraft Launcher
echo    - Wähle Fabric/Forge
echo    - Klicke Play
echo.
echo 6. Verbinde mit Server
echo    - Gehe zu Multiplayer
echo    - Add Server: supersmp.fun
echo.
echo Viel Spaß auf SuperSMP!
echo.
pause";
                    
                    File.WriteAllText(batPath, batContent, Encoding.UTF8);
                }
                
                Console.WriteLine("Installationsanleitung erstellt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Erstellen der Anleitung: {ex.Message}");
            }
        }

        private async void OnStartMinecraftButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                StartMinecraftButton.IsEnabled = false;
                DownloadButton.IsEnabled = false;
                
                if (string.IsNullOrEmpty(_modpackPath) || !Directory.Exists(_modpackPath))
                {
                    StatusText.Text = "⚠️ Modpack nicht gefunden!\nBitte zuerst Modpack herunterladen.";
                    await Task.Delay(3000);
                    StartMinecraftButton.IsEnabled = true;
                    DownloadButton.IsEnabled = true;
                    return;
                }
                
                var gameVersion = _selectedGameVersion;
                if (string.IsNullOrWhiteSpace(gameVersion))
                {
                    gameVersion = GetSelectedMinecraftVersion();
                    _selectedGameVersion = gameVersion;
                }

                StatusText.Text = $"🚀 Starte Minecraft {gameVersion}...\n⏳ Bitte warten...";
                
                try
                {
                    // Stelle sicher, dass die gewählte Minecraft-Version installiert ist
                    var minecraftPath = _systemMinecraftPath ?? GetDefaultMinecraftPath();
                    if (!IsMinecraftVersionInstalled(gameVersion))
                    {
                        StatusText.Text = $"⏳ Installiere Minecraft {gameVersion}...";
                        await _minecraftInstaller.InstallMinecraftVersionAsync(gameVersion, minecraftPath);
                    }

                    // Starte Minecraft mit dem heruntergeladenen Modpack
                    await _minecraftLauncher.LaunchMinecraftAsync(
                        modpackPath: _modpackPath,
                        gameVersion: gameVersion,
                        modloader: _selectedModloader ?? "fabric"
                    );
                    
                    StatusText.Text = "✅ Minecraft gestartet!\n" +
                                     "🎮 Das Spiel sollte sich jetzt öffnen.\n" +
                                     "🔗 Der Server wird automatisch verbunden: supersmp.fun";
                    
                    // Halte die Nachricht 8 Sekunden
                    await Task.Delay(8000);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"❌ Fehler beim Starten: {ex.Message}\n\n" +
                                     "MANUELLE LÖSUNG:\n" +
                                     "1. Öffne Minecraft Launcher\n" +
                                     "2. Wähle das Profil 'SuperSMP'\n" +
                                     "3. Klicke 'Play'\n" +
                                     "4. Server: supersmp.fun";
                    
                    Console.WriteLine($"Fehler: {ex}");
                    
                    // Zeige Alternative
                    OpenModpackFolder();
                    
                    await Task.Delay(5000);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Unerwarteter Fehler: {ex.Message}";
                Console.WriteLine($"Fehler: {ex}");
                await Task.Delay(5000);
            }
            finally
            {
                StartMinecraftButton.IsEnabled = true;
                DownloadButton.IsEnabled = true;
            }
        }

        private void OpenModpackFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(_modpackPath) && Directory.Exists(_modpackPath))
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        Process.Start("explorer", $"\"{_modpackPath}\"");
                    }
                    else if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        Process.Start("xdg-open", _modpackPath);
                    }
                    else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
                    {
                        Process.Start("open", _modpackPath);
                    }
                    
                    Console.WriteLine($"Modpack Ordner geöffnet: {_modpackPath}");
                    
                    // Öffne Installationsanleitung nach kurzer Verzögerung
                    Task.Delay(1000).ContinueWith(_ => 
                    {
                        try
                        {
                            string guidePath = Path.Combine(_modpackPath, "INSTALLATION.txt");
                            if (File.Exists(guidePath))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = guidePath,
                                    UseShellExecute = true
                                });
                            }
                        }
                        catch { /* Ignore */ }
                    });
                }
                else
                {
                    // Fallback: Öffne versions Ordner
                    string versionsPath = "versions";
                    if (Directory.Exists(versionsPath))
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            Process.Start("explorer", versionsPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Öffnen des Modpack Ordners: {ex.Message}");
            }
        }

        private async Task CopyToClipboardAsync(string text)
        {
            try
            {
                // Vereinfachte Version ohne komplexen Dispatcher
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                    Console.WriteLine("IP in Zwischenablage kopiert.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei Zwischenablage: {ex.Message}");
            }
        }

        private void CheckIfModpackInstalled()
        {
            try
            {
                // Suche nach dem neuesten Modpack in versions Ordner
                string versionsPath = "versions";
                
                if (Directory.Exists(versionsPath))
                {
                    var modpackDirs = Directory.GetDirectories(versionsPath)
                        .OrderByDescending(d => Directory.GetCreationTime(d))
                        .ToList();
                    
                    if (modpackDirs.Count > 0)
                    {
                        _modpackPath = modpackDirs[0];
                        StartMinecraftButton.IsEnabled = true;
                        StatusText.Text = $"✅ Modpack gefunden: {Path.GetFileName(_modpackPath)}\nKlicke auf 'Minecraft Starten' für Anleitung.";
                        return;
                    }
                }
                
                StartMinecraftButton.IsEnabled = false;
                StatusText.Text = "Bitte zuerst Modpack herunterladen.";
            }
            catch
            {
                StatusText.Text = "Status: Bereit";
            }
        }

        private void UpdateButtonText()
        {
            DownloadButton.Content = _isDownloading ? "⏳ Lade herunter..." : "📥 Modpack herunterladen";
        }

        private string GetShaderOption()
        {
            try
            {
                var selectedItem = ShaderComboBox.SelectedItem;
                
                if (selectedItem is ComboBoxItem comboBoxItem)
                {
                    return comboBoxItem.Content?.ToString() ?? "Mit Shadern";
                }
                
                if (selectedItem is string str)
                {
                    return str;
                }
                
                return selectedItem?.ToString() ?? "Mit Shadern";
            }
            catch
            {
                return "Mit Shadern";
            }
        }

        private string GetLunarClientFeatures()
        {
            try
            {
                var selectedItem = LCFComboBox.SelectedItem;
                
                if (selectedItem is ComboBoxItem comboBoxItem)
                {
                    var content = comboBoxItem.Content?.ToString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content;
                    }
                }
                
                if (selectedItem is string str)
                {
                    return str;
                }
                
                return selectedItem?.ToString() ?? "With Lunar Client Features";
            }
            catch
            {
                return "With Lunar Client Features";
            }
        }

        private string GetModloader()
        {
            try
            {
                var selectedItem = ModloaderComboBox.SelectedItem;
                
                if (selectedItem is ComboBoxItem comboBoxItem)
                {
                    var content = comboBoxItem.Content?.ToString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        return content.ToLower();
                    }
                }
                
                if (selectedItem is string str)
                {
                    return str.ToLower();
                }
                
                return selectedItem?.ToString()?.ToLower() ?? "fabric";
            }
            catch
            {
                return "fabric";
            }
        }

        private string GetSelectedMinecraftVersion()
        {
            if (_availableMinecraftVersions != null && _availableMinecraftVersions.Count > 0)
            {
                return _availableMinecraftVersions[0];
            }

            return "1.20.1";
        }

        private bool IsMinecraftVersionInstalled(string version)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(version))
                    return false;

                string path = _systemMinecraftPath ?? GetDefaultMinecraftPath();
                string versionFolder = Path.Combine(path, "versions", version);
                string jarPath = Path.Combine(versionFolder, $"{version}.jar");
                return Directory.Exists(versionFolder) && File.Exists(jarPath);
            }
            catch
            {
                return false;
            }
        }

        private string GetDefaultMinecraftPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, ".minecraft");
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".minecraft");
            }
            else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", "minecraft");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minecraft");
        }
    }
}