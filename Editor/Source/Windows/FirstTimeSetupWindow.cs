using System.IO;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public class FirstTimeSetupWindow : EditorWindow
    {
        private int _currentStep;
        private string _detectedPath = "";
        private string _manualPath = "";
        private bool _searchPerformed;
        private bool _pathVerified;

        private static readonly string[] StepTitles = { "Welcome", "ImageMagick", "Done" };

        public static void ShowSetupWindow()
        {
            var window = GetWindow<FirstTimeSetupWindow>(true, "Icon Importer Setup", true);
            window.minSize = new Vector2(500, 360);
            window.maxSize = new Vector2(500, 360);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawStepIndicator();
            DrawSeparator();

            switch (_currentStep)
            {
                case 0: DrawWelcomeStep(); break;
                case 1: DrawImageMagickStep(); break;
                case 2: DrawCompleteStep(); break;
            }
        }

        private void DrawHeader()
        {
            var style = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.Space(10);
            GUILayout.Label("Icon Importer  —  First Time Setup", style);
            GUILayout.Space(6);
        }

        private void DrawStepIndicator()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < StepTitles.Length; i++)
            {
                var style = new GUIStyle(EditorStyles.miniLabel);

                if (i == _currentStep)
                {
                    style.fontStyle = FontStyle.Bold;
                    style.normal.textColor = new Color(0.25f, 0.6f, 1f);
                }
                else if (i < _currentStep)
                {
                    style.normal.textColor = new Color(0.35f, 0.75f, 0.35f);
                }

                GUILayout.Label($"{i + 1}. {StepTitles[i]}", style);

                if (i < StepTitles.Length - 1)
                    GUILayout.Label("  ›  ", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawWelcomeStep()
        {
            var body = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };

            GUILayout.Space(16);
            GUILayout.Label("Welcome to Icon Importer!", EditorStyles.boldLabel);
            GUILayout.Space(8);
            GUILayout.Label(
                "This tool lets you import SVG icon packs into Unity, preview them, and batch-convert " +
                "them to PNG sprites ready for use in your UI.\n\n" +
                "This wizard will walk you through the one-time setup.",
                body);

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(showBack: false, nextLabel: "Next  ›", nextEnabled: true, onNext: () => _currentStep = 1);
        }

        private void DrawImageMagickStep()
        {
            var body = new GUIStyle(EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            GUILayout.Label("ImageMagick", EditorStyles.boldLabel);
            GUILayout.Label(
                "ImageMagick converts SVG icons to PNG sprites. It is free and open-source.",
                body);

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

            if (_searchPerformed)
            {
                if (_pathVerified)
                {
                    var ok = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }, fontStyle = FontStyle.Bold };
                    GUILayout.Label("✓  Found", ok);
                }
                else
                {
                    var err = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.3f, 0.3f) } };
                    GUILayout.Label("✗  Not found — install it, then search again", err);
                }
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
                string filter = Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "";
                string picked = EditorUtility.OpenFilePanel("Select ImageMagick executable", "", filter);
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

            bool hasPath = !string.IsNullOrEmpty(_manualPath);
            string label = hasPath ? "Save & Continue  ›" : "Skip  ›";
            DrawFooterButtons(
                showBack: true, onBack: () => _currentStep = 0,
                nextLabel: label, nextEnabled: true,
                onNext: () => { SaveSettings(); _currentStep = 2; });
        }

        private void DrawCompleteStep()
        {
            var settings = IconImporterSettings.Instance;
            var body = new GUIStyle(EditorStyles.wordWrappedLabel);

            GUILayout.Space(16);
            GUILayout.Label("Setup Complete!", EditorStyles.boldLabel);
            GUILayout.Space(8);

            if (settings.imageMagickDetected)
            {
                var ok = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }, fontStyle = FontStyle.Bold };
                GUILayout.Label($"✓  ImageMagick: {settings.imageMagickPath}", ok);
            }
            else
            {
                GUILayout.Label("ImageMagick not configured. You can set the path later via Tools > Icon Importer > Settings.", body);
            }

            GUILayout.Space(10);
            GUILayout.Label("Open the Icon Pack Manager to get started:\nTools  >  Icon Importer  >  Manage Icon Packs", body);

            GUILayout.FlexibleSpace();
            DrawSeparator();
            DrawFooterButtons(showBack: true, onBack: () => _currentStep = 1, nextLabel: "Finish", nextEnabled: true, onNext: Close);
        }

        private void SaveSettings()
        {
            var settings = IconImporterSettings.Instance;
            string pathToSave = !string.IsNullOrEmpty(_manualPath) ? _manualPath : _detectedPath;

            if (!string.IsNullOrEmpty(pathToSave) && File.Exists(pathToSave))
            {
                settings.imageMagickPath = pathToSave;
                settings.imageMagickDetected = true;
            }

            settings.hasCompletedSetup = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        private static void DrawFooterButtons(
            bool showBack, System.Action onBack = null,
            string nextLabel = "Next", bool nextEnabled = true, System.Action onNext = null)
        {
            GUILayout.BeginHorizontal();

            if (showBack)
            {
                if (GUILayout.Button("‹  Back", GUILayout.Width(80), GUILayout.Height(28)))
                    onBack?.Invoke();
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!nextEnabled);
            if (GUILayout.Button(nextLabel, GUILayout.Width(160), GUILayout.Height(28)))
                onNext?.Invoke();
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }
    }
}
