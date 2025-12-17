using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using SuperSMPLauncher.Services;
using SuperSMPLauncher.Utils;
using SuperSMPLauncher.Models;

namespace SuperSMPLauncher.Views
{
    public partial class MainWindow : Window
    {
        private readonly ModpackDownloader _downloader = new ModpackDownloader();
        private readonly ModrinthApi _api = new ModrinthApi();
        private bool _isDownloading = false;
        private bool _isLoadingVersions = false;
        private List<string> _availableMinecraftVersions = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            
            // Standardwerte setzen
            ShaderComboBox.SelectedIndex = 0;
            ModloaderComboBox.SelectedIndex = 0;
            MinecraftVersionComboBox.SelectedIndex = 0;
            
            // Event-Handler
            DownloadButton.Click += OnDownloadButtonClick;
            ReloadVersionsButton.Click += OnReloadVersionsButtonClick;
            CustomVersionCheckBox.Checked += OnCustomVersionCheckedChanged;
            CustomVersionCheckBox.Unchecked += OnCustomVersionCheckedChanged;
            
            // Lade Minecraft-Versionen beim Start
            LoadMinecraftVersionsAsync();
        }

        private async void LoadMinecraftVersionsAsync()
        {
            if (_isLoadingVersions) return;
            
            _isLoadingVersions = true;
            LoadingVersionsPanel.IsVisible = true;
            LoadingPanel.IsVisible = true;
            MinecraftVersionComboBox.IsEnabled = false;
            ReloadVersionsButton.IsEnabled = false;
            StatusText.Text = "Lade verfügbare Minecraft-Versionen...";

            try
            {
                // Alle Versionen vom Modrinth API laden
                var versions = await _api.GetVersionsAsync("EDFggNY3");

                if (versions != null && versions.Length > 0)
                {
                    // Alle einzigartigen Minecraft-Versionen sammeln
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

                    // Versionen sortieren (neu zu alt)
                    _availableMinecraftVersions = allMinecraftVersions
                        .OrderByDescending(v => ParseVersion(v))
                        .ToList();

                    // Dropdown aktualisieren
                    await UpdateVersionDropdown();
                    
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
                ReloadVersionsButton.IsEnabled = true;
            }
        }

        private async Task UpdateVersionDropdown()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MinecraftVersionComboBox.Items.Clear();
                
                // "Neueste Version" als erste Option
                MinecraftVersionComboBox.Items.Add(new ComboBoxItem { 
                    Content = "(Neueste Version)" 
                });
                
                // Alle verfügbaren Versionen hinzufügen
                foreach (var version in _availableMinecraftVersions)
                {
                    MinecraftVersionComboBox.Items.Add(new ComboBoxItem { 
                        Content = version 
                    });
                }
                
                // Erste Version auswählen
                MinecraftVersionComboBox.SelectedIndex = 0;
            });
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

        private void OnCustomVersionCheckedChanged(object sender, RoutedEventArgs e)
        {
            bool useCustom = CustomVersionCheckBox.IsChecked ?? false;
            
            MinecraftVersionComboBox.IsVisible = !useCustom;
            CustomVersionTextBox.IsVisible = useCustom;
            
            if (useCustom)
            {
                CustomVersionTextBox.Focus();
            }
        }

        private async void OnReloadVersionsButtonClick(object sender, RoutedEventArgs e)
        {
            await LoadMinecraftVersionsAsync();
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
                // Werte aus der UI lesen
                var shaderOption = (ShaderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mit Shadern";
                var modloader = (ModloaderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "fabric";
                
                string minecraftVersion;
                
                // Minecraft-Version bestimmen
                if (CustomVersionCheckBox.IsChecked ?? false)
                {
                    // Benutzerdefinierte Version
                    minecraftVersion = CustomVersionTextBox.Text?.Trim();
                }
                else
                {
                    // Aus Dropdown
                    var selectedItem = MinecraftVersionComboBox.SelectedItem as ComboBoxItem;
                    var selectedContent = selectedItem?.Content?.ToString();
                    
                    if (selectedContent == "(Neueste Version)" || string.IsNullOrEmpty(selectedContent))
                    {
                        minecraftVersion = string.Empty;
                    }
                    else
                    {
                        minecraftVersion = selectedContent;
                    }
                }

                StatusText.Text = $"⏳ Lade {shaderOption.ToLower()} für {modloader}...";
                
                if (!string.IsNullOrEmpty(minecraftVersion))
                {
                    StatusText.Text += $" (Minecraft {minecraftVersion})";
                }

                string zipPath;
                
                // Download durchführen
                if (string.IsNullOrEmpty(minecraftVersion))
                {
                    zipPath = await _downloader.DownloadLatestForLoaderAndShaderAsync(
                        projectId: "EDFggNY3",
                        modloader: modloader,
                        shaderOption: shaderOption,
                        outputDir: "modpack"
                    );
                }
                else
                {
                    zipPath = await _downloader.DownloadLatestForLoaderMinecraftAndShaderAsync(
                        projectId: "EDFggNY3",
                        modloader: modloader,
                        minecraftVersion: minecraftVersion,
                        shaderOption: shaderOption,
                        outputDir: "modpack"
                    );
                }

                // Extraktion
                StatusText.Text = "⏳ Extrahiere Modpack...";
                ZipExtractor.Extract(zipPath, "instance");

                StatusText.Text = $"✅ {shaderOption} erfolgreich heruntergeladen und extrahiert!";
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

        private void UpdateButtonText()
        {
            DownloadButton.Content = _isDownloading ? "⏳ Lade herunter..." : "📥 Modpack herunterladen";
        }
    }
}