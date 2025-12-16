using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using SuperSMPLauncher.Models;

namespace SuperSMPLauncher.Services
{
    public class ModrinthApi
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ModrinthApi()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "SuperSMPLauncher/1.0.0 (jamie@deine-email.de)"
            );
            _httpClient.BaseAddress = new Uri("https://api.modrinth.com/v2/");
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
        }

        public async Task<ModrinthVersion[]> GetVersionsAsync(string projectIdOrSlug)
        {
            try
            {
                var response = await _httpClient.GetAsync($"project/{projectIdOrSlug}/version");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                
                // Debug-Ausgabe
                Console.WriteLine($"DEBUG: API-Antwort erhalten für {projectIdOrSlug}");
                
                var versions = JsonSerializer.Deserialize<ModrinthVersion[]>(json, _jsonOptions) ?? Array.Empty<ModrinthVersion>();
                
                // Debug: Zeige erste Version
                if (versions.Length > 0)
                {
                    var firstVersion = versions[0];
                    Console.WriteLine($"DEBUG: Erste Version: {firstVersion.VersionNumber}");
                    Console.WriteLine($"DEBUG: Loaders: {string.Join(", ", firstVersion.Loaders)}");
                    Console.WriteLine($"DEBUG: DatePublished: {firstVersion.DatePublished}");
                    
                    if (firstVersion.Files != null)
                    {
                        Console.WriteLine($"DEBUG: Anzahl Files: {firstVersion.Files.Count}");
                        var primaryFile = firstVersion.Files.FirstOrDefault(f => f.Primary);
                        if (primaryFile != null)
                        {
                            Console.WriteLine($"DEBUG: Primary file: {primaryFile.Filename}");
                            Console.WriteLine($"DEBUG: Primary file size: {primaryFile.Size}");
                        }
                    }
                }
                
                return versions;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"FEHLER: API-Anfrage: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"FEHLER: JSON-Parsing: {ex.Message}");
                throw;
            }
        }
    }
}