using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SuperSMPLauncher.Services;
using SuperSMPLauncher.Utils;

namespace SuperSMPLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<string> Modloaders { get; } =
        new() { "fabric", "forge", "neoforge" };

    [ObservableProperty]
    private string selectedModloader = "fabric";

    [RelayCommand]
    private async Task DownloadModpack()
    {
        var downloader = new ModpackDownloader();

        string zip = await downloader.DownloadLatestForLoaderAsync(
            projectId: "EDFggNY3",
            modloader: SelectedModloader,
            outputDir: "modpack"
        );

        ZipExtractor.Extract(zip, "instance");
    }
}