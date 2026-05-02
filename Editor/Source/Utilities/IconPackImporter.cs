using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace KnightForge.IconImporter.Editor.Utilities
{
    public static class IconPackImporter
    {
        private const string GitHubReleasesUrl = "https://api.github.com/repos/tabler/tabler-icons/releases";
        private const string TablerIconsFolder = "~TablerIcons";

        public static IEnumerator ImportTablerIcons(string version, Action<string> progressCallback)
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var tablerPath = Path.Combine(projectPath, TablerIconsFolder);

            progressCallback?.Invoke("Fetching release info...");

            string downloadUrl = null;
            string releaseTag = null;

            yield return FetchReleaseUrl(version, (url, tag) =>
            {
                downloadUrl = url;
                releaseTag = tag;
            });

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Debug.LogError("Failed to fetch download URL from GitHub.");
                yield break;
            }

            progressCallback?.Invoke($"Downloading Tabler Icons {releaseTag}...");

            if (!Directory.Exists(tablerPath))
                Directory.CreateDirectory(tablerPath);

            var extractSuccess = false;
            yield return DownloadAndExtract(downloadUrl, tablerPath, success => extractSuccess = success);

            if (!extractSuccess)
            {
                Debug.LogError("Failed to download or extract Tabler Icons.");
                yield break;
            }

            progressCallback?.Invoke("Download complete!");
            EditorUtility.ClearProgressBar();
            Debug.Log($"Tabler Icons {releaseTag} downloaded and extracted.");
        }

        public static IEnumerator FetchLatestVersion(Action<string> callback)
        {
            string latestTag = null;
            yield return FetchReleaseUrl("latest", (url, tag) => latestTag = tag);
            callback?.Invoke(latestTag);
        }

        private static IEnumerator FetchReleaseUrl(string version, Action<string, string> callback)
        {
            var url = version == "latest"
                ? $"{GitHubReleasesUrl}/latest"
                : $"{GitHubReleasesUrl}/tags/{version}";

            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "Unity-IconImporter");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GitHub API request failed: {request.error}");
                callback(null, null);
                yield break;
            }

            try
            {
                var json = request.downloadHandler.text;

                var tagStart = json.IndexOf("\"tag_name\":\"");
                if (tagStart < 0)
                {
                    Debug.LogError("tag_name not found in release response.");
                    callback(null, null);
                    yield break;
                }

                var tagValStart = tagStart + 12;
                var tagValEnd = json.IndexOf("\"", tagValStart);
                var tag = json.Substring(tagValStart, tagValEnd - tagValStart);

                var downloadUrl = $"https://github.com/tabler/tabler-icons/archive/refs/tags/{tag}.zip";
                callback(downloadUrl, tag);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing release response: {ex.Message}");
                callback(null, null);
            }
        }

        private static IEnumerator DownloadAndExtract(string downloadUrl, string tablerPath, Action<bool> callback)
        {
            var zipPath = Path.Combine(Path.GetDirectoryName(tablerPath), "tabler-icons-temp.zip");

            using var request = UnityWebRequest.Get(downloadUrl);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download failed: {request.error}");
                callback(false);
                yield break;
            }

            try
            {
                File.WriteAllBytes(zipPath, request.downloadHandler.data);
                ExtractZip(zipPath, tablerPath);

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                callback(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Extraction failed: {ex.Message}\n{ex.StackTrace}");
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                callback(false);
            }
        }

        private static void ExtractZip(string zipPath, string tablerPath)
        {
            var fileCount = 0;

            using var zipArchive = ZipFile.OpenRead(zipPath);
            foreach (var entry in zipArchive.Entries)
            {
                var isOutlineIcon = entry.FullName.Contains("icons/outline/") && entry.FullName.EndsWith(".svg");
                var isFilledIcon = entry.FullName.Contains("icons/filled/") && entry.FullName.EndsWith(".svg");
                var isAliasFile = entry.FullName.EndsWith("aliases.json");

                if (isOutlineIcon)
                {
                    var destPath = Path.Combine(tablerPath, "outline", Path.GetFileName(entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, true);
                    fileCount++;
                }
                else if (isFilledIcon)
                {
                    var destPath = Path.Combine(tablerPath, "filled", Path.GetFileName(entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, true);
                    fileCount++;
                }
                else if (isAliasFile)
                {
                    var destPath = Path.Combine(tablerPath, "aliases.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, true);
                    fileCount++;
                }
            }

            Debug.Log($"Extracted {fileCount} files to '{tablerPath}'.");
        }
    }
}