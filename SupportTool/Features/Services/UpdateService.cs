using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace SupportTool.Features.Services
{
    /// <summary>
    /// Service for checking and managing application updates from GitHub Releases
    /// </summary>
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string GitHubReleasesApiUrl = "https://api.github.com/repos/{owner}/{repo}/releases/latest";
        
        // TODO: Update these with your GitHub repository details
        private const string GitHubOwner = "bSienkiewicz";
        private const string GitHubRepo = "MPINW-winui";

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SupportTool-UpdateChecker");
        }

        /// <summary>
        /// Gets the current application version
        /// </summary>
        public static Version GetCurrentVersion()
        {
            var package = Package.Current;
            var version = package.Id.Version;
            return new Version(version.Major, version.Minor, version.Build, version.Revision);
        }

        /// <summary>
        /// Checks for updates from GitHub Releases
        /// </summary>
        /// <returns>UpdateInfo if update is available, null otherwise</returns>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(GitHubOwner) || string.IsNullOrEmpty(GitHubRepo))
                {
                    // GitHub info not configured, skip update check
                    return null;
                }

                var apiUrl = GitHubReleasesApiUrl
                    .Replace("{owner}", GitHubOwner)
                    .Replace("{repo}", GitHubRepo);

                var response = await _httpClient.GetStringAsync(apiUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    return null;
                }

                // Parse version from tag (e.g., "v1.0.1" or "1.0.1")
                var tagVersion = release.TagName.TrimStart('v', 'V');
                if (!Version.TryParse(tagVersion, out var latestVersion))
                {
                    return null;
                }

                var currentVersion = GetCurrentVersion();

                if (latestVersion > currentVersion)
                {
                    // Find the .msix asset
                    var msixAsset = release.Assets?.FirstOrDefault(a => 
                        a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase));

                    if (msixAsset != null)
                    {
                        return new UpdateInfo
                        {
                            Version = latestVersion,
                            CurrentVersion = currentVersion,
                            DownloadUrl = msixAsset.BrowserDownloadUrl,
                            ReleaseNotes = release.Body,
                            ReleaseName = release.Name ?? release.TagName
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Opens the download URL in the default browser
        /// </summary>
        public static void OpenDownloadUrl(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Gets the AppInstaller URL for automatic updates
        /// </summary>
        public static string GetAppInstallerUrl()
        {
            // TODO: Update this with your GitHub Releases AppInstaller URL
            // Format: https://github.com/{owner}/{repo}/releases/download/{tag}/SupportTool.appinstaller
            return $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/SupportTool.appinstaller";
        }
    }

    /// <summary>
    /// Information about an available update
    /// </summary>
    public class UpdateInfo
    {
        public Version Version { get; set; } = new Version(1, 0, 0, 0);
        public Version CurrentVersion { get; set; } = new Version(1, 0, 0, 0);
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;

        public string VersionString => $"v{Version.Major}.{Version.Minor}.{Version.Build}";
        public string CurrentVersionString => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";
    }

    /// <summary>
    /// GitHub Release API response model
    /// </summary>
    internal class GitHubRelease
    {
        public string? TagName { get; set; }
        public string? Name { get; set; }
        public string? Body { get; set; }
        public bool Prerelease { get; set; }
        public GitHubAsset[]? Assets { get; set; }
    }

    /// <summary>
    /// GitHub Release Asset model
    /// </summary>
    internal class GitHubAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
    }
}
