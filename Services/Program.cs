using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

public class ModrinthVersion
{
    public string? VersionNumber { get; set; }
    public string[]? Loaders { get; set; }
    public ModrinthFile[]? Files { get; set; }
}
public class ModrinthFile
{
    public string? Url { get; set; }
    public string? Filename { get; set; }
}

class Program
{
    private static readonly HttpClient http = new();

    static async Task Main()
    {
        const string slug = "supersmp-pack";   // <-- dein Projekt‑Slug
        var versions = await GetVersionsAsync(slug);

        if (versions == null || versions.Count == 0)
        {
            Console.WriteLine($"Keine Versionen für Projekt '{slug}' gefunden.");
            return;
        }

        Console.WriteLine($"Projekt '{slug}' liefert {versions.Count} Version(en):");
        foreach (var v in versions)
        {
            var loaders = v.Loaders != null ? string.Join(", ", v.Loaders) : "keine Loader‑Angabe";
            Console.WriteLine($"  {v.VersionNumber} – Loader: {loaders}");
        }

        // Zeige an, ob ein Fabric‑Eintrag existiert
        bool hasFabric = versions.Any(v => v.Loaders?.Contains("fabric") == true);
        Console.WriteLine($"\nFabric‑Build vorhanden? {(hasFabric ? "JA" : "NEIN")}");
    }

    private static async Task<System.Collections.Generic.List<ModrinthVersion>?> GetVersionsAsync(string slug)
    {
        var url = $"https://api.modrinth.com/v2/project/{slug}/versions";
        try
        {
            var json = await http.GetStringAsync(url);
            return JsonSerializer.Deserialize<System.Collections.Generic.List<ModrinthVersion>>(json);
        }
        catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}