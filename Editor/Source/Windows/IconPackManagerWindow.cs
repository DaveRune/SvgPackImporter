using System.Collections;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace KnightForge.IconImporter.Editor
{
    public class IconPackManagerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _isImporting = false;
        private EditorCoroutine _importCoroutine;

        public static void ShowWindow()
        {
            GetWindow<IconPackManagerWindow>("Icon Pack Manager");
        }

        private void OnGUI()
        {
            GUILayout.Label("Icon Pack Manager", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Existing Icon Packs:", EditorStyles.boldLabel);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            var packs = FindAllIconPacks();
            if (packs.Length == 0)
            {
                GUILayout.Label("(No packs created yet)", EditorStyles.helpBox);
            }
            else
            {
                foreach (var pack in packs)
                {
                    EditorGUILayout.LabelField($"• {pack.name} ({pack.icons.Count} icons)", EditorStyles.label);
                }
            }

            GUILayout.EndScrollView();

            EditorGUILayout.Space();

            GUILayout.Label("Actions:", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(_isImporting);

            if (GUILayout.Button("Create New Pack", GUILayout.Height(30)))
            {
                OnCreateNewPack();
            }

            if (GUILayout.Button("Import Provider", GUILayout.Height(30)))
            {
                Debug.Log("Button Clicked: Importing Tabler Icons...");
                OnImportProvider();
            }

            if (GUILayout.Button("Settings", GUILayout.Height(30)))
            {
                OnSettings();
            }

            EditorGUI.EndDisabledGroup();

            if (_isImporting)
            {
                EditorGUILayout.HelpBox("Importing... Please wait.", MessageType.Info);
            }
        }

        private void OnCreateNewPack()
        {
            string path = EditorUtility.SaveFilePanel("Create Icon Pack", "Assets/IconPacks", "NewIconPack", "asset");
            if (string.IsNullOrEmpty(path))
                return;

            path = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, path));

            var pack = ScriptableObject.CreateInstance<IconPackSO>();
            pack.packName = Path.GetFileNameWithoutExtension(path);
            pack.provider = "Tabler";
            pack.variant = "outline";

            AssetDatabase.CreateAsset(pack, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created icon pack: {path}");
            EditorGUIUtility.PingObject(pack);
        }

        private void OnImportProvider()
        {
            Debug.Log("Importing Tabler Icons...");
            string version = "latest";

            _isImporting = true;
            _importCoroutine = EditorCoroutineUtility.StartCoroutine(ImportTablerIconsWorkflow(version), this);
        }

        private IEnumerator ImportTablerIconsWorkflow(string version)
        {
            Debug.Log("Starting import workflow..."); // See this
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string tablerPath = Path.Combine(projectPath, "~TablerIcons");

            yield return IconPackImporter.ImportTablerIcons(version, progress =>
            {
                Debug.Log($"Import Progress: {progress}"); // Never see this
                EditorUtility.DisplayProgressBar("Icon Importer", progress, 0.5f);
            });

            EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.75f);
            var manifest = ManifestBuilder.BuildManifest(tablerPath, version);

            EditorUtility.ClearProgressBar();
            _isImporting = false;

            if (manifest != null && manifest.icons.Count > 0)
            {
                EditorUtility.DisplayDialog("Success", $"Imported {manifest.icons.Count} icons!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to import icons", "OK");
            }
        }

        private void OnSettings()
        {
            EditorUtility.DisplayDialog("Settings", "ImageMagick Detection - Coming Soon", "OK");
        }

        private IconPackSO[] FindAllIconPacks()
        {
            string[] guids = AssetDatabase.FindAssets("t:IconPackSO");
            var packs = new List<IconPackSO>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pack = AssetDatabase.LoadAssetAtPath<IconPackSO>(path);
                if (pack != null)
                {
                    packs.Add(pack);
                }
            }

            return packs.ToArray();
        }
    }

    public static class EditorCoroutineUtility
    {
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner)
        {
            return new EditorCoroutine(routine, owner);
        }
    }

    public class EditorCoroutine
    {
        private Stack<IEnumerator> _stack;

        public EditorCoroutine(IEnumerator routine, object owner)
        {
            _stack = new Stack<IEnumerator>();
            _stack.Push(routine);
            EditorApplication.update += Update;
        }

        private void Update()
        {
            if (_stack.Count == 0)
            {
                EditorApplication.update -= Update;
                return;
            }

            var top = _stack.Peek();

            // Wait for any in-progress async operation (e.g. UnityWebRequest) before advancing
            if (top.Current is AsyncOperation asyncOp && !asyncOp.isDone)
                return;

            if (!top.MoveNext())
            {
                _stack.Pop();
                if (_stack.Count == 0)
                    EditorApplication.update -= Update;
                return;
            }

            // If the yielded value is a nested coroutine, push it so it runs to completion first
            if (top.Current is IEnumerator nested)
                _stack.Push(nested);
        }
    }
}
