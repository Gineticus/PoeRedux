using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace PoeRedux.Services;

public class GitHubUpdateChecker
{
    private const string GitHubApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";
    private static readonly HttpClient _httpClient = new()
    {
        DefaultRequestHeaders = 
        {
            { "User-Agent", "PoeRedux" }
        }
    };

    public record UpdateInfo(bool IsUpdateAvailable, string? LatestVersion, string? CurrentVersion, string? DownloadUrl);

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }
    }

    /// <summary>
    /// Checks if a newer version is available on GitHub
    /// </summary>
    /// <param name="owner">GitHub repository owner</param>
    /// <param name="repo">GitHub repository name</param>
    /// <returns>UpdateInfo with details about available update</returns>
    public static async Task<UpdateInfo?> CheckForUpdatesAsync(string owner, string repo)
    {
        try
        {
            var currentVersion = GetCurrentVersion();
            var apiUrl = string.Format(GitHubApiUrl, owner, repo);

            var response = await _httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release == null || release.PreRelease)
            {
                return null;
            }

            var latestVersion = ParseVersion(release.TagName);
            var current = ParseVersion(currentVersion);

            if (latestVersion > current)
            {
                return new UpdateInfo(
                    IsUpdateAvailable: true,
                    LatestVersion: release.TagName,
                    CurrentVersion: currentVersion,
                    DownloadUrl: release.HtmlUrl
                );
            }

            return new UpdateInfo(
                IsUpdateAvailable: false,
                LatestVersion: release.TagName,
                CurrentVersion: currentVersion,
                DownloadUrl: null
            );
        }
        catch
        {
            return null;
        }
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
    }

    private static Version ParseVersion(string versionString)
    {
        var cleanVersion = versionString.TrimStart('v', 'V');
        
        if (Version.TryParse(cleanVersion, out var version))
        {
            return version;
        }

        return new Version(0, 0, 0);
    }
}
