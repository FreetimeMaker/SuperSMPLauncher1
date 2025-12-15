using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SuperSMPLauncher.Models;

namespace SuperSMPLauncher.Services;

public class ModrinthApi
{
    private readonly HttpClient _client = new();

    public async Task<List<ModrinthVersion>> GetVersionsAsync(string projectId)
    {
        var url = $"https://api.modrinth.com/v2/project/supersmp-pack/version";
        var json = await _client.GetStringAsync(url);

        return JsonSerializer.Deserialize<List<ModrinthVersion>>(json)!;
    }
}