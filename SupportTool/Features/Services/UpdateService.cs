using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using SupportTool.Features.Alerts.Helpers;

namespace SupportTool.Features.Services
{
    /// <summary>
    /// Service for checking and managing application updates from GitHub Releases
    /// </summary>
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string GitHubReleasesApiUrl = "https://api.github.com/repos/{owner}/{repo}/releases/latest";

        public static string GitHubOwner => ConfigLoader.Get<string>("GitHubOwner", "bSienkiewicz");
        public static string GitHubRepo => ConfigLoader.Get<string>("GitHubRepo", "MPINW-winui");

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SupportTool-UpdateChecker");
        }

        public static Version GetCurrentVersion()
        {
            try
            {
                var package = Package.Current;
                if (package != null)
                {
                    var version = package.Id.Version;
                    return new Version(version.Major, version.Minor, version.Build, version.Revision);
                }
            }
            catch
            {
            }

            try
            {
                var manifestPath = Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest");
                if (File.Exists(manifestPath))
                {
                    var doc = XDocument.Load(manifestPath);
                    var ns = XNamespace.Get("http://schemas.microsoft.com/appx/manifest/foundation/windows10");
                    var identity = doc.Descendants(ns + "Identity").FirstOrDefault();
                    var versionAttr = identity?.Attribute("Version");
                    if (versionAttr != null && Version.TryParse(versionAttr.Value, out var version))
                    {
                        return version;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version;
                if (assemblyVersion != null)
                {
                    return new Version(
                        assemblyVersion.Major,
                        assemblyVersion.Minor,
                        assemblyVersion.Build >= 0 ? assemblyVersion.Build : 0,
                        assemblyVersion.Revision >= 0 ? assemblyVersion.Revision : 0);
                }
            }
            catch
            {
            }

            return new Version(1, 0, 0, 0);
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
                    return null;
                }

                var apiUrl = GitHubReleasesApiUrl
                    .Replace("{owner}", GitHubOwner)
                    .Replace("{repo}", GitHubRepo);

                var response = await _httpClient.GetStringAsync(apiUrl);
                Debug.WriteLine($"GitHub API Response: {response}");
                
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (release == null)
                {
                    Debug.WriteLine("Release is null after deserialization");
                    return null;
                }

                if (string.IsNullOrEmpty(release.TagName))
                {
                    Debug.WriteLine($"TagName is null or empty. Release object: Name={release.Name}, Prerelease={release.Prerelease}");
                    return null;
                }

                var tagVersion = release.TagName.TrimStart('v', 'V');
                Debug.WriteLine($"GitHub release tag: {release.TagName}, parsed: {tagVersion}");
                
                if (!Version.TryParse(tagVersion, out var latestVersion))
                {
                    Debug.WriteLine($"Failed to parse version from tag: {tagVersion}");
                    return null;
                }

                Version currentVersion = GetCurrentVersion();
                Debug.WriteLine($"Current version: {currentVersion}, Latest version: {latestVersion}");

                var normalizedCurrent = new Version(
                    currentVersion.Major,
                    currentVersion.Minor,
                    currentVersion.Build >= 0 ? currentVersion.Build : 0,
                    currentVersion.Revision >= 0 ? currentVersion.Revision : 0);
                
                var normalizedLatest = new Version(
                    latestVersion.Major,
                    latestVersion.Minor,
                    latestVersion.Build >= 0 ? latestVersion.Build : 0,
                    latestVersion.Revision >= 0 ? latestVersion.Revision : 0);

                if (normalizedLatest > normalizedCurrent)
                {
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
            return $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/SupportTool_x64.appinstaller";
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
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
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
        
        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
        
        public long Size { get; set; }
    }
}
