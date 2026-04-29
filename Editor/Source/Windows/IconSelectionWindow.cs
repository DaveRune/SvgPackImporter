using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public class IconSelectionWindow : EditorWindow
    {
        private IconPackSO _targetPack;
        private IconManifest _manifest;
        private List<IconEntry> _filteredIcons = new();
        private Vector2 _gridScroll;
        private Vector2 _selectedScroll;
        private string _searchText = "";
        private string _selectedVariant = "outline";
        private int _currentPage = 0;
        private const int IconsPerPage = 100;
        private const int IconGridColumns = 5;
        private GUIStyle _iconButtonStyle;
        private GUIStyle _selectedIconButtonStyle;

        public static void ShowWindow(IconPackSO pack)
        {
            var window = GetWindow<IconSelectionWindow>("Icon Selection");
            window._targetPack = pack;
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            LoadManifest();
            RefreshFiltered();
        }

        private void LoadManifest()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string manifestPath = Path.Combine(projectPath, "~TablerIcons", "manifest.json");

            if (File.Exists(manifestPath))
            {
                string json = File.ReadAllText(manifestPath);
                _manifest = JsonUtility.FromJson<IconManifest>(json);
            }
            else
            {
                EditorUtility.DisplayDialog("No Manifest", "No icon manifest found. Please import Tabler Icons first.", "OK");
            }
        }

        private void OnGUI()
        {
            if (_manifest == null)
            {
                EditorGUILayout.HelpBox("No icon manifest loaded. Import Tabler Icons first.", MessageType.Info);
                return;
            }

            DrawProviderSelector();
            DrawSearchBar();
            DrawSelectedPreview();
            DrawIconGrid();
            DrawControls();
        }

        private void DrawProviderSelector()
        {
            EditorGUILayout.LabelField("Provider & Variant", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tabler Icons (read-only)");

            EditorGUILayout.BeginHorizontal();
            bool isOutline = GUILayout.Toggle(_selectedVariant == "outline", "Outline", GUILayout.Width(80));
            bool isFilled = GUILayout.Toggle(_selectedVariant == "filled", "Filled", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (isOutline && _selectedVariant != "outline")
            {
                _selectedVariant = "outline";
                _currentPage = 0;
                RefreshFiltered();
            }
            if (isFilled && _selectedVariant != "filled")
            {
                _selectedVariant = "filled";
                _currentPage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.Space(10);
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);
            string newSearch = EditorGUILayout.TextField("Search icons...", _searchText);

            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                _currentPage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.LabelField($"Showing {_filteredIcons.Count} of {_manifest.icons.Count} icons");
            EditorGUILayout.Space(10);
        }

        private void DrawSelectedPreview()
        {
            EditorGUILayout.LabelField($"Selected Icons ({_targetPack.selectedIconNames.Count})", EditorStyles.boldLabel);

            _selectedScroll = EditorGUILayout.BeginScrollView(_selectedScroll, GUILayout.Height(50));
            EditorGUILayout.BeginHorizontal();
            foreach (var iconName in _targetPack.selectedIconNames)
            {
                if (GUILayout.Button(iconName, GUILayout.Width(80), GUILayout.Height(30)))
                {
                    _targetPack.selectedIconNames.Remove(iconName);
                    RefreshFiltered();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
        }

        private void DrawIconGrid()
        {
            EditorGUILayout.LabelField("Available Icons", EditorStyles.boldLabel);

            int startIdx = _currentPage * IconsPerPage;
            int endIdx = Mathf.Min(startIdx + IconsPerPage, _filteredIcons.Count);
            int pageCount = Mathf.Max(1, (_filteredIcons.Count + IconsPerPage - 1) / IconsPerPage);

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(250));

            EditorGUILayout.BeginHorizontal();
            int columnCount = 0;

            for (int i = startIdx; i < endIdx; i++)
            {
                var icon = _filteredIcons[i];
                bool isSelected = _targetPack.selectedIconNames.Contains(icon.name);

                string buttonText = icon.name;
                Color bgColor = isSelected ? new Color(0.2f, 0.5f, 0.8f) : GUI.backgroundColor;
                GUI.backgroundColor = bgColor;

                if (GUILayout.Button(buttonText, GUILayout.Width(100), GUILayout.Height(40)))
                {
                    if (isSelected)
                    {
                        _targetPack.selectedIconNames.Remove(icon.name);
                    }
                    else
                    {
                        _targetPack.selectedIconNames.Add(icon.name);
                    }
                    GUI.changed = true;
                }

                GUI.backgroundColor = Color.white;
                columnCount++;

                if (columnCount >= IconGridColumns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    columnCount = 0;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Page {_currentPage + 1} of {pageCount}");
            if (GUILayout.Button("< Previous", GUILayout.Width(100)))
            {
                _currentPage = Mathf.Max(0, _currentPage - 1);
            }
            if (GUILayout.Button("Next >", GUILayout.Width(100)))
            {
                _currentPage = Mathf.Min(pageCount - 1, _currentPage + 1);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }

        private void DrawControls()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Import", GUILayout.Height(30)))
            {
                if (_targetPack.selectedIconNames.Count == 0)
                {
                    EditorUtility.DisplayDialog("No Icons Selected", "Please select icons before importing.", "OK");
                }
                else
                {
                    OnImportIcons();
                }
            }

            if (GUILayout.Button("Clear Selection", GUILayout.Height(30)))
            {
                _targetPack.selectedIconNames.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnImportIcons()
        {
            var settings = IconImporterSettings.Instance;

            if (!settings.imageMagickDetected)
            {
                bool installNow = EditorUtility.DisplayDialog(
                    "ImageMagick Not Found",
                    "ImageMagick is required to convert SVGs to PNGs. Install it, then restart Unity.",
                    "Open Website", "Cancel");

                if (installNow)
                {
                    System.Diagnostics.Process.Start("https://imagemagick.org/script/download.php");
                }
                return;
            }

            string outputFolder = Path.Combine("Assets/IconPacks", $"{_targetPack.packName}~Sprites");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            Debug.Log($"Converting {_targetPack.selectedIconNames.Count} icons to {outputFolder}");

            EditorUtility.DisplayProgressBar("Converting Icons", "Starting conversion...", 0);

            var convertRoutine = ImageMagickConverter.ConvertSvgsToPngs(
                new List<string>(_targetPack.selectedIconNames),
                _selectedVariant,
                _targetPack.iconSize,
                _targetPack.strokeWidth,
                _targetPack.iconColor,
                outputFolder,
                progress => EditorUtility.DisplayProgressBar("Converting Icons", progress, 0.5f));

            EditorCoroutineUtility.StartCoroutine(CompleteConversionRoutine(convertRoutine, outputFolder), this);
        }

        private System.Collections.IEnumerator CompleteConversionRoutine(System.Collections.IEnumerator convertRoutine, string outputFolder)
        {
            while (convertRoutine.MoveNext())
            {
                yield return convertRoutine.Current;
            }

            EditorUtility.ClearProgressBar();

            _targetPack.icons.Clear();
            foreach (var iconName in _targetPack.selectedIconNames)
            {
                string spritePath = Path.Combine(outputFolder, $"{iconName}.png");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                {
                    _targetPack.icons.Add(new IconPackSO.PackedIcon
                    {
                        iconName = iconName,
                        sprite = sprite
                    });
                }
            }

            EditorUtility.SetDirty(_targetPack);
            AssetDatabase.SaveAssets();

            Debug.Log($"Conversion complete! {_targetPack.selectedIconNames.Count} icons imported to {_targetPack.packName}");
        }

        private static class EditorCoroutineUtility
        {
            public static EditorCoroutine StartCoroutine(System.Collections.IEnumerator routine, object owner)
            {
                return new EditorCoroutine(routine, owner);
            }
        }

        private class EditorCoroutine
        {
            private System.Collections.IEnumerator _routine;

            public EditorCoroutine(System.Collections.IEnumerator routine, object owner)
            {
                _routine = routine;
                EditorApplication.update += Update;
            }

            private void Update()
            {
                if (_routine != null && !_routine.MoveNext())
                {
                    EditorApplication.update -= Update;
                    _routine = null;
                }
            }
        }

        private void RefreshFiltered()
        {
            _filteredIcons.Clear();

            foreach (var icon in _manifest.icons)
            {
                if (icon.variant != _selectedVariant)
                    continue;

                if (string.IsNullOrEmpty(_searchText))
                {
                    _filteredIcons.Add(icon);
                }
                else
                {
                    bool matches = icon.name.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase);
                    if (!matches && icon.aliases != null)
                    {
                        matches = icon.aliases.Any(a => a.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase));
                    }

                    if (matches)
                    {
                        _filteredIcons.Add(icon);
                    }
                }
            }
        }
    }
}
