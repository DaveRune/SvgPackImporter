using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace KnightForge.IconImporter.Editor
{
    public static class IconPackImporter
    {
        private const string GitHubReleasesUrl = "https://api.github.com/repos/tabler/tabler-icons/releases";
        private const string TablerIconsFolder = "~TablerIcons";

        public static IEnumerator ImportTablerIcons(string version, System.Action<string> progressCallback)
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string tablerPath = Path.Combine(projectPath, TablerIconsFolder);

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

            bool extractSuccess = false;
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

        private static IEnumerator FetchReleaseUrl(string version, System.Action<string, string> callback)
        {
            string url = version == "latest"
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
                string json = request.downloadHandler.text;

                int tagStart = json.IndexOf("\"tag_name\":\"");
                if (tagStart < 0)
                {
                    Debug.LogError("tag_name not found in release response.");
                    callback(null, null);
                    yield break;
                }

                int tagValStart = tagStart + 12;
                int tagValEnd = json.IndexOf("\"", tagValStart);
                string tag = json.Substring(tagValStart, tagValEnd - tagValStart);

                string downloadUrl = $"https://github.com/tabler/tabler-icons/archive/refs/tags/{tag}.zip";
                callback(downloadUrl, tag);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing release response: {ex.Message}");
                callback(null, null);
            }
        }

        private static IEnumerator DownloadAndExtract(string downloadUrl, string tablerPath, System.Action<bool> callback)
        {
            string zipPath = Path.Combine(Path.GetDirectoryName(tablerPath), "tabler-icons-temp.zip");

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
            int fileCount = 0;

            using var zipArchive = ZipFile.OpenRead(zipPath);
            foreach (var entry in zipArchive.Entries)
            {
                bool isOutlineIcon = entry.FullName.Contains("icons/outline/") && entry.FullName.EndsWith(".svg");
                bool isFilledIcon = entry.FullName.Contains("icons/filled/") && entry.FullName.EndsWith(".svg");
                bool isAliasFile = entry.FullName.EndsWith("aliases.json");

                if (isOutlineIcon)
                {
                    string destPath = Path.Combine(tablerPath, "outline", Path.GetFileName(entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, overwrite: true);
                    fileCount++;
                }
                else if (isFilledIcon)
                {
                    string destPath = Path.Combine(tablerPath, "filled", Path.GetFileName(entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, overwrite: true);
                    fileCount++;
                }
                else if (isAliasFile)
                {
                    string destPath = Path.Combine(tablerPath, "aliases.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, overwrite: true);
                    fileCount++;
                }
            }

            Debug.Log($"Extracted {fileCount} files to '{tablerPath}'.");
        }
    }
}
