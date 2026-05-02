using System.Collections;
using System.IO;
using System.Linq;
using KnightForge.IconImporter.Editor.Providers.Tabler;
using KnightForge.IconImporter.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Windows
{
    public sealed class IconPackManagerWindow : EditorWindow
    {
        private bool _isImporting;
        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            GUILayout.Label("Icon Pack Manager", EditorStyles.largeLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Existing Icon Packs:", EditorStyles.boldLabel);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            var packs = FindAllIconPacks();
            if (packs.Length == 0)
                GUILayout.Label("(No packs created yet)", EditorStyles.helpBox);
            else
                foreach (var pack in packs)
                    EditorGUILayout.LabelField($"• {pack.name} ({pack.icons.Count} icons)", EditorStyles.label);

            GUILayout.EndScrollView();

            EditorGUILayout.Space();
            GUILayout.Label("Actions:", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(_isImporting);

            if (GUILayout.Button("Create New Pack", GUILayout.Height(30)))
                OnCreateNewPack();

            if (GUILayout.Button("Import Provider", GUILayout.Height(30)))
                OnImportProvider();

            EditorGUI.EndDisabledGroup();

            if (_isImporting)
                EditorGUILayout.HelpBox("Importing... Please wait.", MessageType.Info);
        }

        public static void ShowWindow()
        {
            GetWindow<IconPackManagerWindow>("Icon Pack Manager");
        }

        private static void OnCreateNewPack()
        {
            var path = EditorUtility.SaveFilePanel("Create Icon Pack", "Assets/IconPacks", "NewIconPack", "asset");
            if (string.IsNullOrEmpty(path))
                return;

            path = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, path));

            var pack = CreateInstance<IconPack>();
            pack.provider = Providers.Tabler.TablerIconProvider.EnsureProvider();

            AssetDatabase.CreateAsset(pack, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created icon pack at '{path}'.");
            EditorGUIUtility.PingObject(pack);
        }

        private void OnImportProvider()
        {
            var provider = Providers.Tabler.TablerIconProvider.EnsureProvider();
            var version = provider ? provider.version : "latest";
            _isImporting = true;
            EditorCoroutineUtility.StartCoroutine(ImportTablerIconsWorkflow(version), this);
        }

        private IEnumerator ImportTablerIconsWorkflow(string version)
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var tablerPath = Path.Combine(projectPath, "~TablerIcons");

            yield return IconPackImporter.ImportTablerIcons(version, progress => { EditorUtility.DisplayProgressBar("Icon Importer", progress, 0.5f); });

            EditorUtility.DisplayProgressBar("Icon Importer", "Building manifest...", 0.75f);
            var manifest = TablerManifestBuilder.BuildManifest(tablerPath, version);

            EditorUtility.ClearProgressBar();
            _isImporting = false;

            if (manifest != null && manifest.icons.Count > 0)
                EditorUtility.DisplayDialog("Success", $"Imported {manifest.icons.Count} icons!", "OK");
            else
                EditorUtility.DisplayDialog("Error", "Failed to import icons.", "OK");
        }

        private static IconPack[] FindAllIconPacks()
        {
            var guids = AssetDatabase.FindAssets("t:IconPackSO");

            return guids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<IconPack>)
                .Where(pack => pack)
                .ToArray();
        }
    }
}