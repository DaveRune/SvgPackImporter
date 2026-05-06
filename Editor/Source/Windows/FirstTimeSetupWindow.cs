using System;
using System.Collections.Generic;
using System.IO;
using KnightForge.IconImporter.Editor.Data;
using KnightForge.IconImporter.Editor.Utilities;
using KnightForge.IconImporter.Providers;
using KnightForge.IconImporter.Providers.BuiltIn;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Windows
{
    public sealed class FirstTimeSetupWindow : EditorWindow
    {
        private static readonly string[] StepTitles = { "Welcome", "ImageMagick", "Providers", "Done" };

        private static readonly Color StepActiveColor = new(0.25f, 0.6f, 1f);
        private static readonly Color StepDoneColor = new(0.35f, 0.75f, 0.35f);
        private static readonly Color OkColor = new(0.3f, 0.8f, 0.3f);
        private static readonly Color ErrorColor = new(0.9f, 0.3f, 0.3f);
        private static readonly Color SeparatorColor = new(0.5f, 0.5f, 0.5f, 0.3f);

        private readonly Dictionary<Type, string> _repoUrlCache = new();

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
        private GUIStyle _okMiniStyle;
        private GUIStyle _errorStyle;

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
                case 3: DrawCompleteStep(); break;
            }
        }

        public static void ShowSetupWindow()
        {
            var window = GetWindow<FirstTimeSetupWindow>(true, "IconImporter Setup", true);
            window.minSize = new Vector2(500, 400);
            window.maxSize = new Vector2(500, 400);
            window.ShowUtility();

            window.DetectImageMagickInstallation();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            GUILayout.Label("IconImporter  -  First Time Setup", _headerStyle);
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
            GUILayout.Label("Welcome to IconImporter!", EditorStyles.boldLabel);
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
            {
                DetectImageMagickInstallation();
            }

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
            GUILayout.Label("Built-in Icon Providers", EditorStyles.boldLabel);
            GUILayout.Label(
                "Add built-in providers to your project. Each creates a provider asset you configure " +
                "and use to download icons.",
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

            GUILayout.Space(6);
            EditorGUILayout.HelpBox("After adding a provider, select it in the Project window and click 'Download and Setup'.", MessageType.Info);

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(true, () => _currentStep = 1, "Next  ›", true, () => _currentStep = 3);
        }

        private void DrawProviderRow<T>() where T : RepoIconProvider
        {
            const string destinationFolder = "Assets/Icon Providers";
            var repoUrl = GetRepoUrl<T>();
            var repoName = repoUrl.Substring(repoUrl.LastIndexOf('/') + 1);
            var niceName = ObjectNames.NicifyVariableName(typeof(T).Name);
            var assetPath = $"{destinationFolder}/{niceName}.asset";
            var linkText = repoUrl.Replace("https://", "");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(repoName, EditorStyles.boldLabel, GUILayout.Width(110));

            if (GUILayout.Button(linkText, EditorStyles.linkLabel))
                Application.OpenURL(repoUrl);

            GUILayout.FlexibleSpace();

            if (AssetDatabase.LoadAssetAtPath<T>(assetPath))
                GUILayout.Label("✓ Added", _okMiniStyle, GUILayout.Width(60));
            else if (GUILayout.Button("Add to Project", GUILayout.Width(100), GUILayout.Height(22)))
                AddProvider<T>();

            EditorGUILayout.EndHorizontal();
        }

        private string GetRepoUrl<T>() where T : RepoIconProvider
        {
            var type = typeof(T);
            if (!_repoUrlCache.TryGetValue(type, out var url))
            {
                var instance = CreateInstance<T>();
                url = instance.DefaultRepoUrl;
                DestroyImmediate(instance);
                _repoUrlCache[type] = url;
            }
            return url;
        }

        private void DrawCompleteStep()
        {
            var settings = IconImporterSettings.Instance;

            GUILayout.Space(16);
            GUILayout.Label("Setup Complete!", EditorStyles.boldLabel);
            GUILayout.Space(8);

            if (settings.ImageMagickDetected)
                GUILayout.Label($"✓  ImageMagick: {settings.ImageMagickPath}", _okBoldStyle);
            else
                GUILayout.Label("ImageMagick not configured. Set the path later via Tools > IconImporter > Setup.", _bodyStyle);

            GUILayout.Space(10);
            GUILayout.Label(
                "To get started:\n" +
                "• Select a provider asset and click 'Download and Setup'\n" +
                "• Right-click in the Project window and choose Create > IconImporter > Icon Pack\n" +
                "• Assign your provider to the pack and click 'Manage Icons'",
                _bodyStyle);

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(true, () => _currentStep = 2, "Finish", true, Close);
        }

        private void AddProvider<T>() where T : IconProvider
        {
            const string destinationFolder = "Assets/Icon Providers";

            var providerName = ObjectNames.NicifyVariableName(typeof(T).Name);
            var assetPath = $"{destinationFolder}/{providerName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing)
            {
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder(destinationFolder))
                AssetDatabase.CreateFolder("Assets", "Icon Providers");

            var scriptableObject = CreateInstance<T>();
            AssetDatabase.CreateAsset(scriptableObject, assetPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(scriptableObject);
        }

        private void SaveSettings()
        {
            var settings = IconImporterSettings.Instance;
            var pathToSave = !string.IsNullOrEmpty(_manualPath) ? _manualPath : _detectedPath;

            if (!string.IsNullOrEmpty(pathToSave) && File.Exists(pathToSave))
            {
                settings.ImageMagickPath = pathToSave;
                settings.ImageMagickDetected = true;
            }

            settings.HasCompletedSetup = true;
            IconImporterSettings.Save();
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

            _okMiniStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = OkColor }
            };

            _errorStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = ErrorColor }
            };
        }
    }
}
