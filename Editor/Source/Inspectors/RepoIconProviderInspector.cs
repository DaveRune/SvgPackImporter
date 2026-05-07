using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using KnightForge.SvgPackImporter.Editor.Utilities;
using KnightForge.SvgPackImporter.Providers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace KnightForge.SvgPackImporter.Editor.Inspectors
{
    [CustomEditor(typeof(RepoIconProvider), true)]
    public sealed class RepoIconProviderInspector : IconProviderInspector
    {
        private bool _isCheckingUpdate;
        private bool _isDownloading;
        private string _progressMessage;
        private string _updateCheckResult;

        protected override void OnEnable()
        {
            base.OnEnable();
            _progressMessage = "";
            _updateCheckResult = "";
        }

        protected override void DrawProviderHeader()
        {
            EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
        }

        protected override void DrawCoreFields()
        {
            DrawSvgRootFolder();
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_version"), new GUIContent("Version"));
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_repoUrl"), new GUIContent("Repository URL"));
            var repoProvider = (RepoIconProvider)target;
            if (GUILayout.Button("Browse", GUILayout.Width(60), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                if (!string.IsNullOrEmpty(repoProvider.RepoUrl))
                    Application.OpenURL(repoProvider.RepoUrl);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Variants", EditorStyles.boldLabel);

            if (repoProvider.Variants.Count == 0)
                EditorGUILayout.LabelField("(none - provider has not been set up)", EditorStyles.helpBox);
            else
                foreach (var variant in repoProvider.Variants)
                    EditorGUILayout.LabelField($"  • {variant}");
        }

        protected override void DrawManifestActions(IconProvider provider)
        {
        }

        protected override void DrawAdditionalContent(IconProvider provider)
        {
            var repoProvider = (RepoIconProvider)provider;

            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(_isDownloading || _isCheckingUpdate);
            if (GUILayout.Button(_isDownloading ? "Working..." : "Download and Setup", GUILayout.Height(30)))
                OnDownloadClicked(repoProvider);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "Downloads the icon pack from GitHub and extracts SVG files to an \"IconImporter\" folder in the project root. " +
                "Run this when setting up or upgrading to a new version.",
                MessageType.Info);

            if (!string.IsNullOrEmpty(_progressMessage))
                EditorGUILayout.HelpBox(_progressMessage, MessageType.None);

            if (manifest != null)
            {
                EditorGUILayout.Space(4);
                EditorGUI.BeginDisabledGroup(_isCheckingUpdate || _isDownloading);
                if (GUILayout.Button(_isCheckingUpdate ? "Checking..." : "Check for Updates", GUILayout.Height(26)))
                    OnCheckForUpdatesClicked(repoProvider);
                EditorGUI.EndDisabledGroup();

                if (!string.IsNullOrEmpty(_updateCheckResult))
                    EditorGUILayout.HelpBox(_updateCheckResult, MessageType.None);
            }

            if (Directory.Exists(repoProvider.GetRootPath()))
            {
                EditorGUILayout.Space(4);
                EditorGUI.BeginDisabledGroup(_isDownloading || _isCheckingUpdate);
                if (GUILayout.Button("Rebuild Manifest from Disk", GUILayout.Height(26)))
                {
                    repoProvider.BuildManifest();
                    LoadManifestStatus(repoProvider);
                    Repaint();
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        protected override void DrawRemoveButton(IconProvider provider)
        {
            EditorGUI.BeginDisabledGroup(_isDownloading || _isCheckingUpdate);
            base.DrawRemoveButton(provider);
            EditorGUI.EndDisabledGroup();
        }

        protected override string GetNoFilesMessage() => "Not downloaded yet.";

        protected override string GetRemoveConfirmMessage(string rootPath) =>
            $"This will permanently delete all downloaded SVG files and supporting data from:\n\n{rootPath}\n\n" +
            "Any previously generated PNG textures will persist, but you will not be able to " +
            "import or update icons from this source. You can re-download at any time using the " +
            "Download and Setup button.\n\nThis cannot be undone.";

        private void OnDownloadClicked(RepoIconProvider provider)
        {
            _isDownloading = true;
            _progressMessage = "";
            _updateCheckResult = "";
            EditorCoroutineUtility.StartCoroutine(DownloadWorkflow(provider), this);
        }

        private IEnumerator DownloadWorkflow(RepoIconProvider provider)
        {
            _progressMessage = "Fetching release info...";
            Repaint();

            string downloadUrl = null;
            string releaseTag = null;
            yield return FetchReleaseUrl(provider, (url, tag) =>
            {
                downloadUrl = url;
                releaseTag = tag;
            });

            if (string.IsNullOrEmpty(downloadUrl))
            {
                _progressMessage = "Failed to fetch release info. Check the console and your connection.";
                _isDownloading = false;
                Repaint();
                yield break;
            }

            _progressMessage = $"Downloading {releaseTag}...";
            Repaint();

            var destPath = provider.GetRootPath();
            var success = false;
            yield return DownloadAndExtract(downloadUrl, destPath, provider, s => success = s);

            if (!success)
            {
                _progressMessage = "Download or extraction failed. Check the console.";
                _isDownloading = false;
                Repaint();
                yield break;
            }

            _progressMessage = "Building manifest...";
            Repaint();

            EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.95f);
            var manifest = provider.BuildManifest(releaseTag);
            EditorUtility.ClearProgressBar();

            _isDownloading = false;

            if (manifest?.icons != null)
            {
                EditorUtility.SetDirty(provider);
                AssetDatabase.SaveAssets();
                LoadManifestStatus(provider);
                _progressMessage = $"Ready - {manifest.icons.Count:N0} icons ({manifest.version}).";
            }
            else
            {
                _progressMessage = "Setup failed - check the console for errors.";
            }

            Repaint();
        }

        private static IEnumerator FetchReleaseUrl(RepoIconProvider provider, Action<string, string> callback)
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
            request.SetRequestHeader("User-Agent", "Unity-IconImporter");
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

        [Serializable]
        private class GitHubRelease
        {
            public string tag_name;
        }

        private static IEnumerator DownloadAndExtract(string downloadUrl, string destPath, RepoIconProvider provider, Action<bool> callback)
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
            catch (Exception ex)
            {
                Debug.LogError($"Extraction failed: {ex.Message}\n{ex.StackTrace}");
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
                    // Anchor on a leading '/' so a descriptor path like "icons/" only matches a real
                    // folder boundary and not unrelated entries like "more-icons/".
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

            Debug.Log($"Extracted {fileCount} files to '{destPath}'.");
        }

        private void OnCheckForUpdatesClicked(RepoIconProvider provider)
        {
            _isCheckingUpdate = true;
            _updateCheckResult = "Checking GitHub...";
            Repaint();
            EditorCoroutineUtility.StartCoroutine(CheckUpdatesWorkflow(provider), this);
        }

        private IEnumerator CheckUpdatesWorkflow(RepoIconProvider provider)
        {
            string latestTag = null;
            yield return FetchReleaseUrl(provider, (url, tag) => latestTag = tag);

            _isCheckingUpdate = false;
            var installedVersion = provider.LoadManifest()?.version ?? "unknown";

            if (string.IsNullOrEmpty(latestTag))
                _updateCheckResult = "Could not reach GitHub. Check your connection.";
            else if (latestTag == installedVersion)
                _updateCheckResult = $"Up to date ({installedVersion}).";
            else
                _updateCheckResult = $"Update available: {latestTag}  (installed: {installedVersion})";

            Repaint();
        }

        private static string GetGitHubSlug(string repoUrl)
        {
            const string prefix = "https://github.com/";
            if (string.IsNullOrEmpty(repoUrl) || !repoUrl.StartsWith(prefix, StringComparison.Ordinal))
                return null;
            return repoUrl[prefix.Length..].TrimEnd('/');
        }
    }
}
