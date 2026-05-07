using System.Collections;
using System.IO;
using KnightForge.SvgPackImporter.Providers;
using KnightForge.SvgPackImporter.Utilities;
using UnityEditor;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Inspectors
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

            if (manifest == null)
            {
                EditorGUILayout.Space(8);
                EditorGUI.BeginDisabledGroup(_isDownloading || _isCheckingUpdate);
                if (GUILayout.Button(_isDownloading ? "Working..." : "Download and Setup", GUILayout.Height(30)))
                    OnDownloadClicked(repoProvider);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.HelpBox(
                    "Downloads the icon pack from GitHub and extracts SVG files to an \"IconProviders\" folder in the project root. " +
                    "Run this when setting up or upgrading to a new version.",
                    MessageType.Info);
            }

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
            var existingManifest = provider.LoadManifest();
            if (existingManifest?.icons?.Count > 0)
            {
                Debug.Log($"SVGs for '{provider.name}' are already downloaded ({existingManifest.version}).");
                LoadManifestStatus(provider);
                _isDownloading = false;
                Repaint();
                yield break;
            }

            var rootPath = provider.GetRootPath();
            if (Directory.Exists(rootPath) && Directory.GetFiles(rootPath, "*.svg", SearchOption.AllDirectories).Length > 0)
            {
                _progressMessage = "Building manifest from existing files...";
                Repaint();

                EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.95f);
                var rebuiltManifest = provider.BuildManifest();
                EditorUtility.ClearProgressBar();

                _isDownloading = false;

                if (rebuiltManifest?.icons?.Count > 0)
                {
                    EditorUtility.SetDirty(provider);
                    AssetDatabase.SaveAssets();
                    LoadManifestStatus(provider);
                    _progressMessage = $"Ready - {rebuiltManifest.icons.Count:N0} icons ({rebuiltManifest.version}).";
                }
                else
                {
                    _progressMessage = "No SVG files found on disk.";
                }

                Repaint();
                yield break;
            }

            _progressMessage = "Fetching release info...";
            Repaint();

            string downloadUrl = null;
            string releaseTag = null;
            yield return RepoDownloadUtility.FetchReleaseUrl(provider, (url, tag) =>
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

            var success = false;
            yield return RepoDownloadUtility.DownloadAndExtract(downloadUrl, rootPath, provider, s => success = s);

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
            yield return RepoDownloadUtility.FetchReleaseUrl(provider, (url, tag) => latestTag = tag);

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
    }
}
