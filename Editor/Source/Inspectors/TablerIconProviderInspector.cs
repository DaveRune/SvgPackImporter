using System.Collections;
using System.IO;
using KnightForge.IconImporter.Editor.Providers.Tabler;
using KnightForge.IconImporter.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Inspectors
{
    [CustomEditor(typeof(TablerIconProvider))]
    public sealed class TablerIconProviderInspector : UnityEditor.Editor
    {
        private int _installedCount;
        private string _installedVersion;
        private bool _isCheckingUpdate;
        private bool _isDownloading;
        private string _progressMessage;
        private bool _statusLoaded;
        private string _updateCheckResult;

        private void OnEnable()
        {
            _statusLoaded = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var so = (TablerIconProvider)target;

            if (!_statusLoaded)
                LoadInstalledStatus(so);

            EditorGUILayout.LabelField("Tabler Icon Provider", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("SVG Root Folder", so.svgRootFolder);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("version"), new GUIContent("Version"));

            EditorGUILayout.Space(8);
            DrawInstalledStatus(so);

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Downloads Tabler Icons from GitHub and extracts SVG files to the SVG Root Folder. " +
                "Run this once when setting up the project, or when upgrading to a new Tabler version.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(_isDownloading || _isCheckingUpdate);
            if (GUILayout.Button(_isDownloading ? "Working..." : "Download and Setup", GUILayout.Height(30)))
                OnDownloadClicked(so);
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_progressMessage))
                EditorGUILayout.HelpBox(_progressMessage, MessageType.None);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(so);
        }

        private void DrawInstalledStatus(TablerIconProvider so)
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            if (_installedCount > 0)
            {
                var okStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.3f, 0.75f, 0.3f) },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField($"✓  {_installedVersion} — {_installedCount:N0} icons available", okStyle);

                if (!string.IsNullOrEmpty(_updateCheckResult))
                    EditorGUILayout.HelpBox(_updateCheckResult, MessageType.None);

                EditorGUI.BeginDisabledGroup(_isCheckingUpdate || _isDownloading);
                if (GUILayout.Button(_isCheckingUpdate ? "Checking..." : "Check for Updates", GUILayout.Height(26)))
                    OnCheckForUpdatesClicked(so);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.LabelField("Icons not yet downloaded.", EditorStyles.helpBox);
            }
        }

        private void LoadInstalledStatus(TablerIconProvider so)
        {
            _statusLoaded = true;
            _installedVersion = "";
            _installedCount = 0;

            var tablerPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), so.svgRootFolder);
            var manifestPath = Path.Combine(tablerPath, "manifest.json");

            if (!File.Exists(manifestPath))
                return;

            try
            {
                var manifest = JsonUtility.FromJson<IconManifest>(File.ReadAllText(manifestPath));
                if (manifest == null) return;
                _installedVersion = manifest.version;
                _installedCount = manifest.icons?.Count ?? 0;
            }
            catch
            {
                // manifest unreadable — treat as not installed
            }
        }

        private void OnDownloadClicked(TablerIconProvider provider)
        {
            _isDownloading = true;
            _progressMessage = "";
            _updateCheckResult = "";
            EditorCoroutineUtility.StartCoroutine(DownloadWorkflow(provider), this);
        }

        private IEnumerator DownloadWorkflow(TablerIconProvider provider)
        {
            var tablerPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, provider.svgRootFolder);

            yield return IconPackImporter.ImportTablerIcons(provider.version, progress =>
            {
                _progressMessage = progress;
                Repaint();
            });

            _progressMessage = "Building manifest...";
            Repaint();

            EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.95f);
            var manifest = TablerManifestBuilder.BuildManifest(tablerPath, provider.version);
            EditorUtility.ClearProgressBar();

            _isDownloading = false;

            if (manifest != null && manifest.icons != null)
            {
                _installedVersion = manifest.version;
                _installedCount = manifest.icons.Count;
                _progressMessage = $"Ready — {_installedCount:N0} icons. ({_installedVersion})";
            }
            else
            {
                _progressMessage = "Setup failed — check the console for errors.";
            }

            Repaint();
        }

        private void OnCheckForUpdatesClicked(TablerIconProvider provider)
        {
            _isCheckingUpdate = true;
            _updateCheckResult = "Checking GitHub...";
            Repaint();
            EditorCoroutineUtility.StartCoroutine(CheckForUpdatesWorkflow(), this);
        }

        private IEnumerator CheckForUpdatesWorkflow()
        {
            string latestTag = null;
            yield return IconPackImporter.FetchLatestVersion(tag => latestTag = tag);

            _isCheckingUpdate = false;

            if (string.IsNullOrEmpty(latestTag))
                _updateCheckResult = "Could not reach GitHub. Check your connection.";
            else if (latestTag == _installedVersion)
                _updateCheckResult = $"Up to date ({_installedVersion}).";
            else
                _updateCheckResult = $"Update available: {latestTag}  (installed: {_installedVersion})";

            Repaint();
        }
    }
}