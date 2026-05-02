using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightForge.IconImporter.Editor.Providers;
using KnightForge.IconImporter.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Windows
{
    public sealed class IconImportWindow : EditorWindow
    {
        private const int IconPreviewSize = 50;
        private const int IconsPerPage = 50;
        private const int IconGridColumns = 10;
        private const int PreviewBorderWidth = 2;

        // Throttle concurrent ImageMagick processes to keep one core free for the editor.
        private static readonly SemaphoreSlim GenerationThrottle = new(
            Mathf.Max(1, Environment.ProcessorCount - 1),
            Mathf.Max(1, Environment.ProcessorCount - 1));

        private static readonly Color UpdateColor = new(0.25f, 0.65f, 0.25f); // green — imported, included in next update
        private static readonly Color DangerColor = new(0.75f, 0.25f, 0.25f); // red   — imported, marked for deletion
        private static readonly Color PendingAddColor = new(0.2f, 0.45f, 0.75f); // blue  — not yet imported, marked for adding

        // Browse grid state
        private readonly List<IconEntry> _filteredBrowse = new();

        // Included grid state
        private readonly List<IconEntry> _filteredIncluded = new();
        private readonly HashSet<string> _pendingAdditions = new();

        private readonly HashSet<string> _pendingDeletions = new();

        // Preview cache — keys with null value are in-flight; non-null are ready.
        private readonly Dictionary<string, Texture2D> _previewCache = new();

        // PNGs written by background tasks that are ready to load as Texture2D on the main thread.
        private readonly ConcurrentQueue<string> _readyToLoad = new();
        private HashSet<string> _activeVariants = new();
        private int _browsePage;
        private Vector2 _browseScroll;
        private GUIStyle _centeredLabelStyle;

        // Styles (lazily initialised in EnsureStyles — must not be created in field initialisers)
        private GUIStyle _iconCellStyle;
        private int _includedPage;
        private Vector2 _includedScroll;
        private int _inFlightCount;
        private IconManifest _manifest;
        private EditorCoroutine _previewCoroutine;
        private string _previewTempFolder;
        private IIconProvider _provider;

        private string _searchText = "";

        private IconPack _targetPack;
        private double _updateCompleteTime = -1;

        private void OnEnable()
        {
            InitialiseProvider();
            RefreshFiltered();
        }

        private void OnDisable()
        {
            _previewCoroutine?.Stop();

            foreach (var texture in _previewCache.Values.Where(texture => texture))
                DestroyImmediate(texture);

            _previewCache.Clear();
        }

        private void OnGUI()
        {
            if (!_targetPack)
            {
                EditorGUILayout.HelpBox("No icon pack loaded. Close and reopen from the pack inspector.", MessageType.Warning);
                return;
            }

            if (_provider == null)
            {
                EditorGUILayout.HelpBox("No icon provider assigned. Assign a provider in the pack inspector.", MessageType.Warning);
                return;
            }

            if (_manifest == null)
            {
                EditorGUILayout.HelpBox("No icon manifest loaded. Import icons first.", MessageType.Info);
                return;
            }

            DrawProviderVariantBar();
            DrawSearchBar();
            DrawIncludedGrid();
            DrawBrowseGrid();
            DrawControls();
        }

        public static void ShowWindow(IconPack pack)
        {
            var window = GetWindow<IconImportWindow>("Icon Import");
            window._targetPack = pack;
            window.minSize = new Vector2(522, 750);
            window.maxSize = new Vector2(522, 2000);
            window.InitialiseProvider();
            window.RefreshFiltered();
        }

        private void EnsureStyles()
        {
            if (_iconCellStyle != null)
                return;

            _iconCellStyle = new GUIStyle(GUI.skin.button)
            {
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(2, 2, 2, 2)
            };

            _centeredLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void InitialiseProvider()
        {
            _provider = _targetPack?.provider ? IconProviderFactory.Create(_targetPack.provider) : null;

            if (_provider != null)
            {
                if (_targetPack && _targetPack.activeVariants.Any())
                    _activeVariants = new HashSet<string>(_targetPack.activeVariants);
                else
                    _activeVariants = new HashSet<string> { _provider.AvailableVariants[0] };
            }

            _manifest = _provider?.LoadManifest();

            if (_provider != null && _manifest == null && _targetPack)
                EditorUtility.DisplayDialog("No Manifest", "No icon manifest found. Please import icons first.", "OK");

            _pendingDeletions.Clear();
            _pendingAdditions.Clear();
        }

        private void DrawProviderVariantBar()
        {
            EditorGUILayout.LabelField("Provider & Variant", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            var changed = false;
            foreach (var variant in _provider.AvailableVariants)
            {
                var label = char.ToUpper(variant[0]) + variant[1..];
                var wasActive = _activeVariants.Contains(variant);
                var isActive = GUILayout.Toggle(wasActive, label, GUILayout.Width(80));

                if (isActive == wasActive) continue;
                if (isActive) _activeVariants.Add(variant);
                else _activeVariants.Remove(variant);
                changed = true;
            }

            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                _targetPack.activeVariants = new List<string>(_activeVariants);
                EditorUtility.SetDirty(_targetPack);
                AssetDatabase.SaveAssets();
                _browsePage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.Space(8);
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);
            var newSearch = EditorGUILayout.TextField("Filter icons...", _searchText);

            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                _browsePage = 0;
                _includedPage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.Space(8);
        }

        private void DrawIncludedGrid()
        {
            EnsureStyles();
            EditorGUILayout.LabelField($"Included Icons ({_filteredIncluded.Count} shown)", EditorStyles.boldLabel);

            var pageCount = Mathf.Max(1, (_filteredIncluded.Count + IconsPerPage - 1) / IconsPerPage);
            _includedPage = Mathf.Clamp(_includedPage, 0, pageCount - 1);

            var startIdx = _includedPage * IconsPerPage;
            var endIdx = Mathf.Min(startIdx + IconsPerPage, _filteredIncluded.Count);

            _includedScroll = EditorGUILayout.BeginScrollView(_includedScroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.Height(160));
            EditorGUILayout.BeginHorizontal();
            var column = 0;

            for (var i = startIdx; i < endIdx; i++)
            {
                var icon = _filteredIncluded[i];
                var key = PendingKey(icon.name, icon.variant);
                var isPendingDeletion = _pendingDeletions.Contains(key);
                var state = isPendingDeletion ? IconCellState.PendingDeletion : IconCellState.Imported;

                if (DrawIconCell(icon.name, icon.variant, state, true))
                {
                    if (isPendingDeletion)
                        _pendingDeletions.Remove(key);
                    else
                        _pendingDeletions.Add(key);
                    GUI.changed = true;
                }

                column++;
                if (column < IconGridColumns) continue;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                column = 0;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Page {_includedPage + 1} of {pageCount}", GUILayout.Width(100));
            if (GUILayout.Button("< Prev", GUILayout.Width(70))) _includedPage = Mathf.Max(0, _includedPage - 1);
            if (GUILayout.Button("Next >", GUILayout.Width(70))) _includedPage = Mathf.Min(pageCount - 1, _includedPage + 1);
            GUILayout.FlexibleSpace();
            if (_pendingDeletions.Any())
                if (GUILayout.Button("Unmark all", GUILayout.Width(90)))
                    foreach (var icon in _targetPack.icons)
                        _pendingDeletions.Remove(PendingKey(icon.iconName, icon.variant));

            if (GUILayout.Button("Mark all for removal", GUILayout.Width(130)))
                foreach (var icon in _targetPack.icons)
                    _pendingDeletions.Add(PendingKey(icon.iconName, icon.variant));

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
        }

        private void DrawBrowseGrid()
        {
            EnsureStyles();
            EditorGUILayout.LabelField("Available Icons", EditorStyles.boldLabel);

            var pageCount = Mathf.Max(1, (_filteredBrowse.Count + IconsPerPage - 1) / IconsPerPage);
            _browsePage = Mathf.Clamp(_browsePage, 0, pageCount - 1);

            var startIndex = _browsePage * IconsPerPage;
            var endIndex = Mathf.Min(startIndex + IconsPerPage, _filteredBrowse.Count);
            var showVariantHint = _activeVariants.Count > 1;

            var importedKeys = new HashSet<string>(_targetPack.icons.Select(i => PendingKey(i.iconName, i.variant)));

            _browseScroll = EditorGUILayout.BeginScrollView(_browseScroll, GUILayout.Height(265));
            EditorGUILayout.BeginHorizontal();
            var column = 0;

            for (var i = startIndex; i < endIndex; i++)
            {
                var icon = _filteredBrowse[i];
                var key = PendingKey(icon.name, icon.variant);
                var isImported = importedKeys.Contains(key);
                var isPendingDeletion = _pendingDeletions.Contains(key);
                var isPendingAddition = _pendingAdditions.Contains(key);

                IconCellState state;
                if (isImported)
                    state = isPendingDeletion ? IconCellState.PendingDeletion : IconCellState.Imported;
                else
                    state = isPendingAddition ? IconCellState.PendingAdd : IconCellState.NotImported;

                if (DrawIconCell(icon.name, icon.variant, state, showVariantHint))
                {
                    if (isImported)
                    {
                        if (isPendingDeletion) _pendingDeletions.Remove(key);
                        else _pendingDeletions.Add(key);
                    }
                    else
                    {
                        if (isPendingAddition) _pendingAdditions.Remove(key);
                        else _pendingAdditions.Add(key);
                    }

                    GUI.changed = true;
                }

                column++;
                if (column < IconGridColumns) continue;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                column = 0;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Page {_browsePage + 1} of {pageCount}", GUILayout.Width(100));
            if (GUILayout.Button("< Prev", GUILayout.Width(70))) _browsePage = Mathf.Max(0, _browsePage - 1);
            if (GUILayout.Button("Next >", GUILayout.Width(70))) _browsePage = Mathf.Min(pageCount - 1, _browsePage + 1);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Browsing {_filteredBrowse.Count} icons", GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
        }

        private bool DrawIconCell(string iconName, string variant, IconCellState state, bool showVariantHint = false)
        {
            var preview = GetPreview(iconName, variant);

            var tooltip = showVariantHint ? $"{iconName} ({variant})" : iconName;
            var cellRect = GUILayoutUtility.GetRect(
                IconPreviewSize, IconPreviewSize, _iconCellStyle,
                GUILayout.Width(IconPreviewSize), GUILayout.Height(IconPreviewSize));

            var clicked = GUI.Button(cellRect, new GUIContent("", tooltip), _iconCellStyle);

            if (Event.current.type != EventType.Repaint)
                return clicked;

            if (preview)
                GUI.DrawTexture(cellRect, preview, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(cellRect, "…", _centeredLabelStyle);

            switch (state)
            {
                case IconCellState.Imported:
                    DrawBorder(cellRect, UpdateColor, PreviewBorderWidth);
                    break;
                case IconCellState.PendingDeletion:
                    DrawBorder(cellRect, DangerColor, PreviewBorderWidth);
                    break;
                case IconCellState.PendingAdd:
                    DrawBorder(cellRect, PendingAddColor, PreviewBorderWidth);
                    break;
            }

            return clicked;
        }

        private static string PendingKey(string iconName, string variant)
        {
            return $"{variant}/{iconName}";
        }

        private static void DrawBorder(Rect rect, Color color, int width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private Texture2D GetPreview(string iconName, string variant)
        {
            var key = $"{iconName}-{variant}";
            if (_previewCache.TryGetValue(key, out var cached))
                return cached;

            _previewCache[key] = null; // reserve slot — prevents re-queuing on subsequent frames

            _previewTempFolder ??= Path.Combine(Application.temporaryCachePath, "IconPreviews");
            Directory.CreateDirectory(_previewTempFolder);

            var svgPath = _provider?.GetSvgPath(iconName, variant);
            if (string.IsNullOrEmpty(svgPath) || !File.Exists(svgPath))
                return null;

            var pngPath = Path.Combine(_previewTempFolder, $"{key}.png");

            if (File.Exists(pngPath))
            {
                // Already on disk — skip ImageMagick, just load it.
                _readyToLoad.Enqueue(key);
            }
            else
            {
                Interlocked.Increment(ref _inFlightCount);
                Task.Run(async () =>
                {
                    await GenerationThrottle.WaitAsync();
                    try
                    {
                        ImageMagickConverter.TryGeneratePreview(svgPath, pngPath, IconPreviewSize);
                    }
                    finally
                    {
                        GenerationThrottle.Release();
                        _readyToLoad.Enqueue(key);
                        Interlocked.Decrement(ref _inFlightCount);
                    }
                });
            }

            _previewCoroutine ??= EditorCoroutineUtility.StartCoroutine(DrainReadyQueue(), this);
            return null;
        }

        // Runs on the main thread each frame, loading completed PNGs as Texture2D.
        private IEnumerator DrainReadyQueue()
        {
            while (_inFlightCount > 0 || !_readyToLoad.IsEmpty)
            {
                var repaintNeeded = false;

                while (_readyToLoad.TryDequeue(out var key))
                {
                    var pngPath = Path.Combine(_previewTempFolder, $"{key}.png");
                    if (!File.Exists(pngPath))
                        continue;

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(File.ReadAllBytes(pngPath));
                    _previewCache[key] = tex;
                    repaintNeeded = true;
                }

                if (repaintNeeded) Repaint();
                yield return null;
            }

            _previewCoroutine = null;
        }

        private void DrawControls()
        {
            GUI.backgroundColor = UpdateColor;
            if (GUILayout.Button("Update", GUILayout.Height(32)))
                OnUpdateIcons();
            GUI.backgroundColor = Color.white;

            var addCount = _pendingAdditions.Count;
            var removeCount = _pendingDeletions.Count;

            GUILayout.Space(10);

            if (addCount > 0 || removeCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (addCount > 0)
                {
                    var addStyle = new GUIStyle(GUI.skin.label)
                    {
                        normal = { textColor = PendingAddColor }
                    };
                    GUILayout.Label($"Add: {addCount} icon{(addCount == 1 ? "" : "s")}", addStyle);
                }

                if (removeCount > 0)
                {
                    var removeStyle = new GUIStyle(GUI.skin.label)
                    {
                        normal = { textColor = DangerColor }
                    };
                    GUILayout.Label($"Remove: {removeCount} icon{(removeCount == 1 ? "" : "s")}", removeStyle);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (!(_updateCompleteTime > 0) || !(EditorApplication.timeSinceStartup - _updateCompleteTime < 5.0))
                return;

            var successStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = UpdateColor },
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Update complete.", successStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            Repaint();
        }

        private void OnUpdateIcons()
        {
            // Apply deletions
            foreach (var key in _pendingDeletions)
            {
                var parts = key.Split('/');
                if (parts.Length == 2)
                    _targetPack.icons.RemoveAll(i => i.variant == parts[0] && i.iconName == parts[1]);
            }

            // Apply additions
            foreach (var key in _pendingAdditions)
            {
                var parts = key.Split('/');
                if (parts.Length == 2)
                {
                    var iconName = parts[1];
                    var variant = parts[0];
                    if (!_targetPack.icons.Any(i => i.iconName == iconName && i.variant == variant))
                        _targetPack.icons.Add(new IconPack.PackedIcon { iconName = iconName, variant = variant });
                }
            }

            _pendingDeletions.Clear();
            _pendingAdditions.Clear();
            RefreshFiltered();

            if (!ConfirmIfWillRemoveIcons())
                return;

            IconImportProcessor.StartUpdate(_targetPack, _provider, this, () =>
            {
                _updateCompleteTime = EditorApplication.timeSinceStartup;
                Repaint();
            });
        }

        private bool ConfirmIfWillRemoveIcons()
        {
            var deletedKeys = new HashSet<string>(_pendingDeletions);
            var removed = _targetPack.icons
                .Where(i => deletedKeys.Contains($"{i.variant}/{i.iconName}"))
                .Select(i => $"{i.iconName} ({i.variant})")
                .ToList();

            if (removed.Count == 0)
                return true;

            var sb = new StringBuilder();
            sb.AppendLine($"{removed.Count} icon(s) will be removed. Any references to them in the project will be lost:\n");

            const int maxDisplay = 10;
            for (var i = 0; i < Mathf.Min(removed.Count, maxDisplay); i++)
                sb.AppendLine($"  • {removed[i]}");

            if (removed.Count > maxDisplay)
                sb.AppendLine($"  ... and {removed.Count - maxDisplay} more.");

            sb.AppendLine("\nContinue with update?");

            return EditorUtility.DisplayDialog("Remove Icons?", sb.ToString(), "Remove & Update", "Cancel");
        }

        private void RefreshFiltered()
        {
            _filteredIncluded.Clear();
            _filteredBrowse.Clear();

            if (_manifest == null || !_targetPack)
                return;

            var hasSearch = !string.IsNullOrEmpty(_searchText);
            var currentIconKeys = new HashSet<string>(_targetPack.icons.Select(i => $"{i.variant}/{i.iconName}"));

            foreach (var icon in _manifest.icons)
            {
                if (!_activeVariants.Contains(icon.variant))
                    continue;

                var key = $"{icon.variant}/{icon.name}";

                if (currentIconKeys.Contains(key))
                    if (!hasSearch || icon.name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                        _filteredIncluded.Add(icon);

                if (hasSearch)
                {
                    var nameMatch = icon.name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
                    var aliasMatch = !nameMatch && icon.aliases != null && icon.aliases.Any(a => a.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
                    if (!nameMatch && !aliasMatch)
                        continue;
                }

                _filteredBrowse.Add(icon);
            }
        }

        private enum IconCellState
        {
            NotImported,
            PendingAdd,
            Imported,
            PendingDeletion
        }
    }
}