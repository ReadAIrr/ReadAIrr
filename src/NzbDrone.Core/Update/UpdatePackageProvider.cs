using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Update
{
    public interface IUpdatePackageProvider
    {
        UpdatePackage GetLatestUpdate(string branch, Version currentVersion);
        List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null);
    }

    public class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonProperty("prerelease")]
        public bool Prerelease { get; set; }

        [JsonProperty("draft")]
        public bool Draft { get; set; }

        [JsonProperty("assets")]
        public List<GitHubAsset> Assets { get; set; }
    }

    public class GitHubAsset
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }
    }

    public class UpdatePackageProvider : IUpdatePackageProvider
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/ReadAIrr/Readairr/releases";

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public UpdatePackageProvider(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public UpdatePackage GetLatestUpdate(string branch, Version currentVersion)
        {
            try
            {
                var releases = GetGitHubReleases();
                var latestRelease = GetFirstStableRelease(releases);

                if (latestRelease == null)
                {
                    _logger.Debug("No stable releases found on GitHub");
                    return null;
                }

                var releaseVersion = ParseVersionFromTag(latestRelease.TagName);
                if (releaseVersion == null || releaseVersion <= currentVersion)
                {
                    _logger.Debug("Latest GitHub release {0} is not newer than current version {1}", releaseVersion, currentVersion);
                    return null;
                }

                return CreateUpdatePackage(latestRelease, branch);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get latest update from GitHub releases");
                return null;
            }
        }

        public List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null)
        {
            try
            {
                var releases = GetGitHubReleases();
                var updatePackages = new List<UpdatePackage>();

                foreach (var release in releases)
                {
                    if (release.Draft)
                    {
                        continue;
                    }

                    var releaseVersion = ParseVersionFromTag(release.TagName);
                    if (releaseVersion == null)
                    {
                        continue;
                    }

                    if (IsRelevantUpdate(releaseVersion, currentVersion, previousVersion))
                    {
                        updatePackages.Add(CreateUpdatePackage(release, branch));
                    }
                }

                _logger.Debug("Found {0} relevant updates from GitHub releases", updatePackages.Count);
                return updatePackages;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get recent updates from GitHub releases");
                return new List<UpdatePackage>();
            }
        }

        private List<GitHubRelease> GetGitHubReleases()
        {
            var request = new HttpRequest(GitHubApiUrl);
            request.Headers.Add("User-Agent", "ReadAIrr/11.0.0");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            var response = _httpClient.Get<List<GitHubRelease>>(request);
            return response.Resource ?? new List<GitHubRelease>();
        }

        private GitHubRelease GetFirstStableRelease(List<GitHubRelease> releases)
        {
            foreach (var release in releases)
            {
                if (!release.Draft && !release.Prerelease)
                {
                    return release;
                }
            }

            return null;
        }

        private bool IsRelevantUpdate(Version releaseVersion, Version currentVersion, Version previousVersion)
        {
            if (previousVersion != null && releaseVersion > previousVersion && releaseVersion <= currentVersion)
            {
                return true;
            }

            if (releaseVersion > currentVersion)
            {
                return true;
            }

            return false;
        }

        private UpdatePackage CreateUpdatePackage(GitHubRelease release, string branch)
        {
            var version = ParseVersionFromTag(release.TagName);
            var changes = ParseReleaseNotes(release.Body);
            var downloadUrl = GetDownloadUrl(release);

            return new UpdatePackage
            {
                Version = version,
                ReleaseDate = release.PublishedAt,
                Branch = branch,
                Changes = changes,
                Url = downloadUrl,
                FileName = GetFileNameFromUrl(downloadUrl)
            };
        }

        private Version ParseVersionFromTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                return null;
            }

            var versionString = tagName.StartsWith("v") ? tagName.Substring(1) : tagName;

            if (Version.TryParse(versionString, out var version))
            {
                return version;
            }

            _logger.Debug("Could not parse version from tag: {0}", tagName);
            return null;
        }

        private UpdateChanges ParseReleaseNotes(string releaseBody)
        {
            var changes = new UpdateChanges();

            if (string.IsNullOrEmpty(releaseBody))
            {
                return changes;
            }

            var lines = releaseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var currentCategory = "";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("#") || string.IsNullOrEmpty(trimmedLine))
                {
                    continue;
                }

                currentCategory = DetermineCategoryFromLine(trimmedLine, currentCategory);

                var item = ExtractItemFromLine(trimmedLine);
                if (!string.IsNullOrEmpty(item))
                {
                    AddItemToChanges(changes, item, currentCategory);
                }
            }

            return changes;
        }

        private string DetermineCategoryFromLine(string line, string currentCategory)
        {
            var lowerLine = line.ToLower();

            if (lowerLine.Contains("new") || lowerLine.Contains("added") || lowerLine.Contains("feature"))
            {
                return "new";
            }

            if (lowerLine.Contains("fix") || lowerLine.Contains("bug"))
            {
                return "fixed";
            }

            return currentCategory;
        }

        private string ExtractItemFromLine(string line)
        {
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                return line.Substring(2).Trim();
            }

            if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                return Regex.Replace(line, @"^\d+\.\s", "").Trim();
            }

            return string.Empty;
        }

        private void AddItemToChanges(UpdateChanges changes, string item, string category)
        {
            if (category == "fixed")
            {
                changes.Fixed.Add(item);
            }
            else
            {
                changes.New.Add(item);
            }
        }

        private string GetDownloadUrl(GitHubRelease release)
        {
            if (release.Assets == null || release.Assets.Count == 0)
            {
                return string.Empty;
            }

            var osString = OsInfo.Os.ToString().ToLowerInvariant();
            var archString = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            var platformAsset = FindPlatformSpecificAsset(release.Assets, osString, archString);
            if (platformAsset != null)
            {
                return platformAsset.BrowserDownloadUrl;
            }

            var genericAsset = FindGenericAsset(release.Assets, osString);
            if (genericAsset != null)
            {
                return genericAsset.BrowserDownloadUrl;
            }

            return release.Assets[0].BrowserDownloadUrl;
        }

        private GitHubAsset FindPlatformSpecificAsset(List<GitHubAsset> assets, string osString, string archString)
        {
            foreach (var asset in assets)
            {
                var assetName = asset.Name.ToLowerInvariant();
                if (assetName.Contains(osString) && assetName.Contains(archString))
                {
                    return asset;
                }
            }

            return null;
        }

        private GitHubAsset FindGenericAsset(List<GitHubAsset> assets, string osString)
        {
            foreach (var asset in assets)
            {
                if (asset.Name.ToLowerInvariant().Contains(osString))
                {
                    return asset;
                }
            }

            return null;
        }

        private string GetFileNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            try
            {
                var uri = new Uri(url);
                return System.IO.Path.GetFileName(uri.LocalPath);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
