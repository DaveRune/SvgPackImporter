using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnightForge.SvgPackImporter.Data;
using KnightForge.SvgPackImporter.Providers;
using KnightForge.SvgPackImporter.Providers.BuiltIn;
using KnightForge.SvgPackImporter.Utilities;
using UnityEditor;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Windows
{
    /// Wizard window that guides users through the one-time setup of SvgPackImporter.
    internal sealed class FirstTimeSetupWindow : EditorWindow
    {
        private static readonly string[] StepTitles = { "Welcome", "ImageMagick", "Providers", "Icon Pack", "Done" };

        private static readonly Color StepActiveColor = new(0.25f, 0.6f, 1f);
        private static readonly Color StepDoneColor = new(0.35f, 0.75f, 0.35f);
        private static readonly Color OkColor = new(0.3f, 0.8f, 0.3f);
        private static readonly Color WarningColor = new(0.9f, 0.75f, 0.2f);
        private static readonly Color ErrorColor = new(0.9f, 0.3f, 0.3f);
        private static readonly Color SeparatorColor = new(0.5f, 0.5f, 0.5f, 0.3f);

        private readonly Dictionary<Type, string> _repoUrlCache = new();
        private readonly Dictionary<Type, ProviderRowState> _providerRowStates = new();

        private int _currentStep;
        private string _detectedPath = "";
        private string _manualPath = "";
        private bool _pathVerified;
        private bool _searchPerformed;

        private GUIStyle _headerStyle;
        private GUIStyle _stepActiveStyle;
        private GUIStyle _stepDoneStyle;
        private GUIStyle _stepPendingStyle;
        private GUIStyle _body12Style;
        private GUIStyle _bodyStyle;
        private GUIStyle _okBoldStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _doneButtonStyle;
        private GUIStyle _statusGreenStyle;
        private GUIStyle _statusYellowStyle;
        private GUIStyle _statusRedStyle;

        private enum ProviderDownloadStateType
        {
            Ready,
            Downloading,
            Done,
            Failed
        }

        private enum ProviderSetupStateType
        {
            None,
            AssetOnly,
            Downloaded
        }

        private sealed class ProviderRowState
        {
            public ProviderDownloadStateType state;
            public string buttonLabel = "Download and Setup";
        }

        private void OnProjectChange()
        {
            var toRemove = new List<Type>();
            foreach (var (type, state) in _providerRowStates)
                if (state.state != ProviderDownloadStateType.Downloading)
                    toRemove.Add(type);

            foreach (var type in toRemove)
                _providerRowStates.Remove(type);

            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();
            DrawStepIndicator();
            DrawSeparator();

            switch (_currentStep)
            {
                case 0: DrawWelcomeStep(); break;
                case 1: DrawImageMagickStep(); break;
                case 2: DrawProvidersStep(); break;
                case 3: DrawIconPackStep(); break;
                case 4: DrawCompleteStep(); break;
            }
        }

        public static void ShowSetupWindow()
        {
            var window = GetWindow<FirstTimeSetupWindow>(true, "SvgPackImporter Setup", true);
            window.minSize = new Vector2(560, 420);
            window.maxSize = new Vector2(560, 420);
            window.ShowUtility();

            window.DetectImageMagickInstallation();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            GUILayout.Label("SvgPackImporter  -  First Time Setup", _headerStyle);
            GUILayout.Space(6);
        }

        private void DrawStepIndicator()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (var i = 0; i < StepTitles.Length; i++)
            {
                GUIStyle style;
                if (i == _currentStep) style = _stepActiveStyle;
                else if (i < _currentStep) style = _stepDoneStyle;
                else style = _stepPendingStyle;

                GUILayout.Label($"{i + 1}. {StepTitles[i]}", style);

                if (i < StepTitles.Length - 1)
                    GUILayout.Label("  ›  ", _stepPendingStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawWelcomeStep()
        {
            GUILayout.Space(16);
            GUILayout.Label("Welcome to SvgPackImporter!", EditorStyles.boldLabel);
            GUILayout.Space(8);
            GUILayout.Label(
                "This tool lets you import SVG icon packs into Unity, preview them, and batch-convert " +
                "them to PNG sprites ready for use in your UI.\n\n" +
                "This wizard will walk you through the one-time setup.",
                _body12Style);

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(false, nextLabel: "Next  ›", nextEnabled: true, onNext: () => _currentStep = 1);
        }

        private void DrawImageMagickStep()
        {
            GUILayout.Space(10);
            GUILayout.Label("ImageMagick", EditorStyles.boldLabel);
            GUILayout.Label(
                "ImageMagick converts SVG icons to PNG sprites. It is free and open-source.",
                _bodyStyle);

            GUILayout.Space(6);

            if (GUILayout.Button("Download ImageMagick  (opens browser)", GUILayout.Height(26)))
                Application.OpenURL("https://imagemagick.org/script/download.php");

            GUILayout.Space(10);
            DrawSeparator();
            GUILayout.Space(6);

            GUILayout.Label("Auto-Detection", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Search for Installation", GUILayout.Width(160), GUILayout.Height(26)))
                DetectImageMagickInstallation();

            if (_searchPerformed)
            {
                if (_pathVerified)
                    GUILayout.Label("✓  Found", _okBoldStyle);
                else
                    GUILayout.Label("✗  Not found - install it, then search again", _errorStyle);
            }

            GUILayout.EndHorizontal();

            if (_searchPerformed && _pathVerified)
                EditorGUILayout.HelpBox(_detectedPath, MessageType.None);

            GUILayout.Space(8);
            GUILayout.Label("Manual Path:", EditorStyles.label);
            GUILayout.BeginHorizontal();
            _manualPath = EditorGUILayout.TextField(_manualPath);

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var filter = Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "";
                var picked = EditorUtility.OpenFilePanel("Select ImageMagick executable", "", filter);
                if (!string.IsNullOrEmpty(picked))
                {
                    _manualPath = picked;
                    _pathVerified = true;
                    _searchPerformed = true;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            DrawSeparator();

            var hasPath = !string.IsNullOrEmpty(_manualPath);
            var label = hasPath ? "Save & Continue  ›" : "Skip  ›";
            DrawFooterButtons(
                true, () => _currentStep = 0,
                label, true,
                () =>
                {
                    SaveSettings();
                    _currentStep = 2;
                });
        }

        private void DetectImageMagickInstallation()
        {
            _searchPerformed = true;
            _pathVerified = false;

            if (ImageMagickConverter.TryDetectImageMagick(out var found))
            {
                _detectedPath = found;
                _manualPath = found;
                _pathVerified = true;
            }
            else
            {
                _detectedPath = "";
            }
        }

        private void DrawProvidersStep()
        {
            GUILayout.Space(10);
            GUILayout.Label("Optional", EditorStyles.boldLabel);
            GUILayout.Space(10);
            GUILayout.Label("Built-in Icon Providers", EditorStyles.boldLabel);
            GUILayout.Label(
                "Download built-in providers to your project. Each downloads SVG files from GitHub " +
                "and creates a provider asset for use with your icon packs.",
                _bodyStyle);

            GUILayout.Space(10);
            DrawSeparator();
            GUILayout.Space(6);

            DrawProviderRow<TablerIconProvider>();
            DrawProviderRow<FeatherIconProvider>();
            DrawProviderRow<HeroIconProvider>();
            DrawProviderRow<IconoirIconProvider>();
            DrawProviderRow<CssGgIconProvider>();
            DrawProviderRow<SimpleIconsProvider>();

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(true, () => _currentStep = 1, "Next  ›", true, () => _currentStep = 3);
        }

        private void DrawProviderRow<T>() where T : RepoIconProvider
        {
            const string ProviderFolder = "Assets/Icon Providers";
            var repoUrl = GetRepoUrl<T>();
            var repoName = repoUrl.Substring(repoUrl.LastIndexOf('/') + 1);
            var niceName = ObjectNames.NicifyVariableName(typeof(T).Name);
            var assetPath = $"{ProviderFolder}/{niceName}.asset";
            var linkText = repoUrl.Replace("https://", "");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(repoName, EditorStyles.boldLabel, GUILayout.Width(110));

            if (GUILayout.Button(linkText, EditorStyles.linkLabel))
                Application.OpenURL(repoUrl);

            GUILayout.FlexibleSpace();

            var rowState = GetOrCreateRowState<T>();

            switch (rowState.state)
            {
                case ProviderDownloadStateType.Ready:
                    if (GUILayout.Button("Download and Setup", GUILayout.Width(145), GUILayout.Height(22)))
                        StartProviderDownload<T>(assetPath);
                    break;

                case ProviderDownloadStateType.Downloading:
                    EditorGUI.BeginDisabledGroup(true);
                    GUILayout.Button(rowState.buttonLabel, GUILayout.Width(145), GUILayout.Height(22));
                    EditorGUI.EndDisabledGroup();
                    break;

                case ProviderDownloadStateType.Done:
                    if (GUILayout.Button("Done", _doneButtonStyle, GUILayout.Width(145), GUILayout.Height(22)))
                    {
                        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
                        if (guids.Length == 0) break;
                        var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
                        if (!asset) break;
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }

                    break;

                case ProviderDownloadStateType.Failed:
                    if (GUILayout.Button("Failed — Retry", GUILayout.Width(145), GUILayout.Height(22)))
                        StartProviderDownload<T>(assetPath);
                    break;
            }

            EditorGUILayout.EndHorizontal();
        }

        private ProviderRowState GetOrCreateRowState<T>() where T : RepoIconProvider
        {
            var type = typeof(T);
            if (_providerRowStates.TryGetValue(type, out var state))
                return state;

            state = new ProviderRowState();
            if (AssetDatabase.FindAssets($"t:{type.Name}").Length > 0)
                state.state = ProviderDownloadStateType.Done;

            _providerRowStates[type] = state;
            return state;
        }

        private void StartProviderDownload<T>(string assetPath) where T : RepoIconProvider
        {
            const string ProviderFolder = "Assets/Icon Providers";
            var rowState = GetOrCreateRowState<T>();

            var provider = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (!provider)
            {
                if (!AssetDatabase.IsValidFolder(ProviderFolder))
                    AssetDatabase.CreateFolder("Assets", "Icon Providers");

                provider = CreateInstance<T>();
                AssetDatabase.CreateAsset(provider, assetPath);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(provider);
                Selection.activeObject = provider;
            }

            rowState.state = ProviderDownloadStateType.Downloading;
            rowState.buttonLabel = "Fetching...";
            EditorCoroutineUtility.StartCoroutine(DownloadProviderWorkflow(provider), this);
        }

        private IEnumerator DownloadProviderWorkflow<T>(T provider) where T : RepoIconProvider
        {
            var rowState = GetOrCreateRowState<T>();
            var rootPath = provider.GetRootPath();

            var existingManifest = provider.LoadManifest();
            if (existingManifest?.icons?.Count > 0)
            {
                Debug.Log($"SVGs for '{provider.name}' are already downloaded ({existingManifest.version}).");
                rowState.state = ProviderDownloadStateType.Done;
                Repaint();
                yield break;
            }

            if (Directory.Exists(rootPath) && Directory.GetFiles(rootPath, "*.svg", SearchOption.AllDirectories).Length > 0)
            {
                rowState.buttonLabel = "Building...";
                Repaint();

                EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.95f);
                var rebuiltManifest = provider.BuildManifest();
                EditorUtility.ClearProgressBar();

                if (rebuiltManifest?.icons?.Count > 0)
                {
                    EditorUtility.SetDirty(provider);
                    AssetDatabase.SaveAssets();
                }

                rowState.state = rebuiltManifest?.icons?.Count > 0
                    ? ProviderDownloadStateType.Done
                    : ProviderDownloadStateType.Failed;
                Repaint();
                yield break;
            }

            string downloadUrl = null;
            string releaseTag = null;
            yield return RepoDownloadUtility.FetchReleaseUrl(provider, (url, tag) =>
            {
                downloadUrl = url;
                releaseTag = tag;
            });

            if (string.IsNullOrEmpty(downloadUrl))
            {
                rowState.state = ProviderDownloadStateType.Failed;
                Repaint();
                yield break;
            }

            rowState.buttonLabel = $"Downloading {releaseTag}...";
            Repaint();

            var success = false;
            yield return RepoDownloadUtility.DownloadAndExtract(downloadUrl, rootPath, provider, s => success = s);

            if (!success)
            {
                rowState.state = ProviderDownloadStateType.Failed;
                Repaint();
                yield break;
            }

            rowState.buttonLabel = "Building...";
            Repaint();

            EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.95f);
            var manifest = provider.BuildManifest(releaseTag);
            EditorUtility.ClearProgressBar();

            if (manifest?.icons != null)
            {
                EditorUtility.SetDirty(provider);
                AssetDatabase.SaveAssets();
                rowState.state = ProviderDownloadStateType.Done;
            }
            else
            {
                rowState.state = ProviderDownloadStateType.Failed;
            }

            Repaint();
        }

        private void DrawIconPackStep()
        {
            const string IconPacksFolder = "Assets/Icon Packs";
            const string DefaultIconPackPath = IconPacksFolder + "/Icon Pack.asset";

            GUILayout.Space(10);
            GUILayout.Label("Icon Pack", EditorStyles.boldLabel);
            GUILayout.Label(
                "Create an Icon Pack asset to hold your imported icons.\n" +
                "You can create more any time via Right-click > Create > SvgPackImporter > Icon Pack.",
                _bodyStyle);

            GUILayout.Space(10);
            DrawSeparator();
            GUILayout.Space(6);

            GUILayout.Label("Create Icon Pack", EditorStyles.boldLabel);

            IconPack existingPack = null;
            var packGuids = AssetDatabase.FindAssets("t:IconPack");
            if (packGuids.Length > 0)
                existingPack = AssetDatabase.LoadAssetAtPath<IconPack>(AssetDatabase.GUIDToAssetPath(packGuids[0]));

            var completedSteps = existingPack ? 1 : 0;

            if (completedSteps == 0)
            {
                if (GUILayout.Button("Create", GUILayout.Height(28)))
                {
                    if (!AssetDatabase.IsValidFolder(IconPacksFolder))
                        AssetDatabase.CreateFolder("Assets", "Icon Packs");

                    var pack = CreateInstance<IconPack>();
                    AssetDatabase.CreateAsset(pack, DefaultIconPackPath);
                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(pack);
                    Selection.activeObject = pack;
                }
            }
            else
            {
                if (GUILayout.Button("Done", _doneButtonStyle, GUILayout.Height(28)))
                {
                    EditorGUIUtility.PingObject(existingPack);
                    Selection.activeObject = existingPack;
                }

                completedSteps++;
            }

            if (completedSteps == 2)
            {
                GUILayout.Space(10);
                GUILayout.Label("Select Icon Provider(s)", EditorStyles.boldLabel);

                if (existingPack!.Providers.Count == 0 || !existingPack!.Providers.First())
                {
                    GUILayout.Label("Click 'Add Provider' in the Icon Pack and choose a provider.", _bodyStyle);
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    if (GUILayout.Button("Done", _doneButtonStyle, GUILayout.Height(28)))
                    {
                        EditorGUIUtility.PingObject(existingPack);
                        Selection.activeObject = existingPack;
                    }

                    EditorGUI.EndDisabledGroup();

                    completedSteps++;
                }
            }

            if (completedSteps == 3)
            {
                GUILayout.Space(10);
                GUILayout.Label("Add Icons", EditorStyles.boldLabel);
                var isStep3Complete = existingPack!.Icons.Count != 0;

                if (!isStep3Complete)
                {
                    GUILayout.Label("Click 'Manage Icons' in the Icon Pack, select some icons and click 'Update'.", _bodyStyle);
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    if (GUILayout.Button("Done", _doneButtonStyle, GUILayout.Height(28)))
                    {
                        EditorGUIUtility.PingObject(existingPack);
                        Selection.activeObject = existingPack;
                    }

                    EditorGUI.EndDisabledGroup();
                    
                    completedSteps++;
                }
            }

            if (completedSteps == 4)
            {
                GUILayout.Space(10);
                GUILayout.Label("Now you can click and drag your icons into Sprite and Texture2D fields.", _bodyStyle);
            }

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(true, () => _currentStep = 2, "Next  ›", true, () => _currentStep = 4);
        }

        private void DrawCompleteStep()
        {
            var settings = SvgPackImporterSettings.Instance;

            GUILayout.Space(16);
            GUILayout.Label("Setup Complete!", EditorStyles.boldLabel);
            GUILayout.Space(8);
            GUILayout.Label("Click any item below to take action.", _bodyStyle);
            GUILayout.Space(16);

            if (settings.ImageMagickDetected)
                DrawStatusButton("ImageMagick Installed", _statusGreenStyle, () => _currentStep = 1);
            else
                DrawStatusButton("ImageMagick Missing", _statusRedStyle, () => _currentStep = 1);

            DrawFlowArrow();

            IconProvider downloadedProvider = null;
            IconProvider anyProvider = null;
            foreach (var guid in AssetDatabase.FindAssets("t:IconProvider"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<IconProvider>(AssetDatabase.GUIDToAssetPath(guid));
                if (!candidate) continue;
                anyProvider ??= candidate;
                if (!downloadedProvider && candidate.LoadManifest() != null)
                    downloadedProvider = candidate;
            }

            var providerState = downloadedProvider ? ProviderSetupStateType.Downloaded
                : anyProvider ? ProviderSetupStateType.AssetOnly
                : ProviderSetupStateType.None;

            switch (providerState)
            {
                case ProviderSetupStateType.Downloaded:
                    DrawStatusButton("Icon Provider Ready", _statusGreenStyle, () =>
                    {
                        EditorGUIUtility.PingObject(downloadedProvider);
                        Selection.activeObject = downloadedProvider;
                    });
                    break;
                case ProviderSetupStateType.AssetOnly:
                    DrawStatusButton("Icon Provider has no icons", _statusYellowStyle, () =>
                    {
                        EditorGUIUtility.PingObject(anyProvider);
                        Selection.activeObject = anyProvider;
                    });
                    break;
                case ProviderSetupStateType.None:
                    DrawStatusButton("No Icon Provider Created", _statusRedStyle, () => _currentStep = 2);
                    break;
            }

            DrawFlowArrow();

            var iconPackGuids = AssetDatabase.FindAssets("t:IconPack");
            if (iconPackGuids.Length > 0)
            {
                var pack = AssetDatabase.LoadAssetAtPath<IconPack>(AssetDatabase.GUIDToAssetPath(iconPackGuids[0]));
                DrawStatusButton("Icon Pack Created", _statusGreenStyle, () =>
                {
                    if (!pack) return;
                    EditorGUIUtility.PingObject(pack);
                    Selection.activeObject = pack;
                });
            }
            else
            {
                DrawStatusButton("No Icon Pack Created", _statusRedStyle, () => _currentStep = 3);
            }

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(true, () => _currentStep = 3, "Finish", true, Close);
        }

        private static void DrawStatusButton(string label, GUIStyle style, Action onClick)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, style, GUILayout.Height(28), GUILayout.Width(260)))
                onClick();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void DrawFlowArrow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("↓", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }


        private string GetRepoUrl<T>() where T : RepoIconProvider
        {
            var type = typeof(T);

            if (_repoUrlCache.TryGetValue(type, out var url))
                return url;
            
            var instance = CreateInstance<T>();
            url = instance.DefaultRepoUrl;
            DestroyImmediate(instance);
            _repoUrlCache[type] = url;

            return url;
        }

        private void SaveSettings()
        {
            var settings = SvgPackImporterSettings.Instance;
            var pathToSave = !string.IsNullOrEmpty(_manualPath) ? _manualPath : _detectedPath;

            if (!string.IsNullOrEmpty(pathToSave) && File.Exists(pathToSave))
            {
                settings.ImageMagickPath = pathToSave;
                settings.ImageMagickDetected = true;
            }

            settings.HasCompletedSetup = true;
            SvgPackImporterSettings.Save();
            ImageMagickConverter.InvalidateDetectionCache();
        }

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        private static void DrawFooterButtons(bool showBack, Action onBack = null, string nextLabel = "Next", bool nextEnabled = true, Action onNext = null)
        {
            GUILayout.BeginHorizontal();

            if (showBack)
                if (GUILayout.Button("‹  Back", GUILayout.Width(80), GUILayout.Height(28)))
                    onBack?.Invoke();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!nextEnabled);
            var buttonWidth = Mathf.Max(80, nextLabel.Length * 10);
            if (GUILayout.Button(nextLabel, GUILayout.Width(buttonWidth), GUILayout.Height(28)))
                onNext?.Invoke();
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stepActiveStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = StepActiveColor }
            };

            _stepDoneStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = StepDoneColor }
            };

            _stepPendingStyle = new GUIStyle(EditorStyles.miniLabel);

            _body12Style = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };
            _bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel);

            _okBoldStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = OkColor },
                fontStyle = FontStyle.Bold
            };

            _errorStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = ErrorColor }
            };

            _doneButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = OkColor },
                hover = { textColor = OkColor },
                active = { textColor = OkColor }
            };

            _statusGreenStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = OkColor },
                hover = { textColor = OkColor },
                active = { textColor = OkColor }
            };

            _statusYellowStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = WarningColor },
                hover = { textColor = WarningColor },
                active = { textColor = WarningColor }
            };

            _statusRedStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = ErrorColor },
                hover = { textColor = ErrorColor },
                active = { textColor = ErrorColor }
            };
        }
    }
}