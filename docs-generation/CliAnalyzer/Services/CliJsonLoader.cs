using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CliAnalyzer.Models;

namespace CliAnalyzer.Services;

public class CliJsonLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public async Task<CliResponse> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CLI JSON file not found: {filePath}");
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var response = JsonSerializer.Deserialize<CliResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize CLI JSON");
            return response;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON format: {ex.Message}", ex);
        }
    }

    public async Task<CliResponse> LoadFromUrlAsync(string url)
    {
        using var client = new HttpClient();
        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CliResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize CLI JSON");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to fetch CLI JSON: {ex.Message}", ex);
        }
    }
}
