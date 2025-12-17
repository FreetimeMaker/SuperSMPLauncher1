using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SuperSMPLauncher.Services;
using SuperSMPLauncher.Utils;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace SuperSMPLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ModpackDownloader _downloader = new ModpackDownloader();
    private readonly ModrinthApi _api = new ModrinthApi();

    public ObservableCollection<string> Modloaders { get; } =
        new() { "fabric", "forge", "neoforge" };

    public ObservableCollection<string> MinecraftVersions { get; } = new();
    
    public ObservableCollection<string> ShaderOptions { get; } = 
        new() { "Mit Shadern", "Ohne Shader" };

    [ObservableProperty]
    private string selectedModloader = "fabric";

    [ObservableProperty]
    private string selectedMinecraftVersion = string.Empty;
    
    [ObservableProperty]
    private string selectedShaderOption = "Mit Shadern";

    [ObservableProperty]
    private bool isLoadingVersions = false;

    [ObservableProperty]
    private bool isDownloading = false;

    [ObservableProperty]
    private string statusMessage = "Bereit";

    // EXPLIZITE COMMAND-PROPERTIES
    public ICommand LoadAvailableMinecraftVersionsAsyncCommand { get; }
    public ICommand DownloadModpackCommand { get; }

    public MainWindowViewModel()
    {
        // Commands manuell erstellen
        LoadAvailableMinecraftVersionsAsyncCommand = new AsyncRelayCommand(LoadAvailableMinecraftVersionsAsync);
        DownloadModpackCommand = new AsyncRelayCommand(DownloadModpackAsync);

        // Lade verfügbare Minecraft-Versionen beim Start
        LoadAvailableMinecraftVersionsAsync();
    }

    private async Task LoadAvailableMinecraftVersionsAsync()
    {
        if (IsLoadingVersions) return;

        IsLoadingVersions = true;
        StatusMessage = "Lade verfügbare Minecraft-Versionen...";

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

                var sortedVersions = allMinecraftVersions
                    .OrderByDescending(v => TryParseVersion(v))
                    .ToList();

                // UI-Thread sicher aktualisieren
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MinecraftVersions.Clear();
                    foreach (var version in sortedVersions)
                    {
                        MinecraftVersions.Add(version);
                    }

                    if (MinecraftVersions.Any())
                    {
                        SelectedMinecraftVersion = MinecraftVersions.First();
                    }
                });

                StatusMessage = $"✅ {MinecraftVersions.Count} Versionen verfügbar.";
            }
            else
            {
                SetDefaultMinecraftVersions();
                StatusMessage = "⚠️ Keine Versionen gefunden.";
            }
        }
        catch (Exception ex)
        {
            SetDefaultMinecraftVersions();
            StatusMessage = $"❌ Fehler: {ex.Message}";
        }
        finally
        {
            IsLoadingVersions = false;
        }
    }

    private void SetDefaultMinecraftVersions()
    {
        var defaultVersions = new[] { "1.20.1", "1.20", "1.19.4", "1.19.2", "1.18.2" };
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            MinecraftVersions.Clear();
            foreach (var version in defaultVersions)
            {
                MinecraftVersions.Add(version);
            }
            
            if (MinecraftVersions.Any())
            {
                SelectedMinecraftVersion = MinecraftVersions.First();
            }
        });
    }

    private async Task DownloadModpackAsync()
    {
        if (IsDownloading) return;

        IsDownloading = true;
        StatusMessage = "Starte Download...";

        try
        {
            string zip;
            
            if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion))
            {
                StatusMessage = $"⏳ Lade {SelectedShaderOption.ToLower()} für {SelectedModloader}...";
                zip = await _downloader.DownloadLatestForLoaderAndShaderAsync(
                    projectId: "EDFggNY3",
                    modloader: SelectedModloader,
                    shaderOption: SelectedShaderOption,
                    outputDir: "modpack"
                );
            }
            else
            {
                StatusMessage = $"⏳ Lade {SelectedShaderOption.ToLower()} für {SelectedModloader} (MC {SelectedMinecraftVersion})...";
                zip = await _downloader.DownloadLatestForLoaderMinecraftAndShaderAsync(
                    projectId: "EDFggNY3",
                    modloader: SelectedModloader,
                    minecraftVersion: SelectedMinecraftVersion,
                    shaderOption: SelectedShaderOption,
                    outputDir: "modpack"
                );
            }

            StatusMessage = "⏳ Extrahiere...";
            ZipExtractor.Extract(zip, "instance");

            StatusMessage = $"✅ {SelectedShaderOption} erfolgreich heruntergeladen!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private static System.Version TryParseVersion(string versionString)
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
}