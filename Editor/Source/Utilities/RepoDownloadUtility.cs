using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using KnightForge.SvgPackImporter.Providers;
using UnityEngine;
using UnityEngine.Networking;

namespace KnightForge.SvgPackImporter.Utilities
{
    /// Shared download and extraction helpers for repo-based icon providers.
    internal static class RepoDownloadUtility
    {
        internal static IEnumerator FetchReleaseUrl(RepoIconProvider provider, Action<string, string> callback)
        {
            var slug = GetGitHubSlug(provider.RepoUrl);
            if (string.IsNullOrEmpty(slug))
            {
                Debug.LogError($"Cannot parse GitHub slug from URL: '{provider.RepoUrl}'.");
                callback(null, null);
                yield break;
            }

            var apiUrl = provider.Version == "latest"
                ? $"https://api.github.com/repos/{slug}/releases/latest"
                : $"https://api.github.com/repos/{slug}/releases/tags/{provider.Version}";

            using var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("User-Agent", "Unity-SvgPackImporter");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GitHub API request failed: {request.error}");
                callback(null, null);
                yield break;
            }

            var release = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);
            if (release == null || string.IsNullOrEmpty(release.tag_name))
            {
                Debug.LogError("tag_name not found in release response.");
                callback(null, null);
                yield break;
            }

            callback($"https://github.com/{slug}/archive/refs/tags/{release.tag_name}.zip", release.tag_name);
        }

        internal static IEnumerator DownloadAndExtract(string downloadUrl, string destPath, RepoIconProvider provider, Action<bool> callback)
        {
            var tempZip = Path.Combine(Path.GetDirectoryName(destPath), $"{provider.name}_temp.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(tempZip));

            using var request = UnityWebRequest.Get(downloadUrl);
            request.downloadHandler = new DownloadHandlerFile(tempZip) { removeFileOnAbort = true };
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download failed: {request.error}");
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
                callback(false);
                yield break;
            }

            try
            {
                ExtractZip(tempZip, destPath, provider);

                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                callback(true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Extraction failed: {exception.Message}\n{exception.StackTrace}");
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
                callback(false);
            }
        }

        private static void ExtractZip(string zipPath, string destPath, RepoIconProvider provider)
        {
            Directory.CreateDirectory(destPath);
            var fileCount = 0;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                foreach (var (variantName, descriptor) in provider.VariantPaths)
                {
                    if (!entry.FullName.Contains($"/{descriptor.Path}") || !entry.FullName.EndsWith(".svg"))
                        continue;

                    var dest = Path.Combine(destPath, variantName, Path.GetFileName(entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    entry.ExtractToFile(dest, true);
                    fileCount++;
                    break;
                }

                if (provider.AliasesZipPath != null && entry.FullName.EndsWith(provider.AliasesZipPath))
                {
                    var dest = Path.Combine(destPath, Path.GetFileName(provider.AliasesZipPath));
                    entry.ExtractToFile(dest, true);
                    fileCount++;
                }
            }

            Debug.Log($"Extracted '{fileCount}' files to '{destPath}'.");
        }

        private static string GetGitHubSlug(string repoUrl)
        {
            const string Prefix = "https://github.com/";
            if (string.IsNullOrEmpty(repoUrl) || !repoUrl.StartsWith(Prefix, StringComparison.Ordinal))
                return null;
            return repoUrl[Prefix.Length..].TrimEnd('/');
        }

        [Serializable]
        private sealed class GitHubRelease
        {
            public string tag_name;
        }
    }
}
