using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightForge.IconImporter.Editor.Utilities;
using KnightForge.IconImporter.Providers;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Windows
{
    public sealed class IconManagerWindow : EditorWindow
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const int IconPreviewSize = 50;
        private const int IconsPerPage = 50;
        private const int IconGridColumns = 10;
        private const int PreviewBorderWidth = 2;

        private const string NoVariantsName = "All";

        private static readonly Color UpdateColor = new(0.25f, 0.65f, 0.25f);
        private static readonly Color DangerColor = new(0.75f, 0.25f, 0.25f);
        private static readonly Color PendingAddColor = new(0.2f, 0.45f, 0.75f);
        private static readonly Color VariantOnColor = new(0.3f, 0.75f, 0.3f);
        private static readonly Color VariantOffColor = new(0.45f, 0.45f, 0.45f);

        // ── Preview throttle ──────────────────────────────────────────────────
        private static readonly SemaphoreSlim GenerationThrottle = new(
            Mathf.Max(1, Environment.ProcessorCount - 1),
            Mathf.Max(1, Environment.ProcessorCount - 1));

        private readonly List<ProviderIconEntry> _filteredBrowse = new();
        private readonly List<ProviderIconEntry> _filteredIncluded = new();

        private readonly Dictionary<string, IconManifest> _manifestCache = new();
        private readonly Dictionary<string, ProviderIconEntry> _pendingAdditions = new();
        private readonly HashSet<string> _pendingDeletions = new();

        private readonly Dictionary<string, Texture2D> _previewCache = new();
        private readonly ConcurrentQueue<string> _readyToLoad = new();
        private HashSet<string> _activeVariants = new();
        private List<string> _allVariants = new();
        private int _browsePage;
        private Vector2 _browseScroll;
        private GUIStyle _centeredLabelStyle;

        private GUIStyle _iconCellStyle;
        private int _includedPage;
        private Vector2 _includedScroll;
        private int _inFlightCount;
        private EditorCoroutine _previewCoroutine;
        private string _previewTempFolder;
        private List<IconProvider> _providers = new();

        private string _searchText = "";

        // ── Shift-click anchor ────────────────────────────────────────────────
        private ShiftClickGrid _shiftAnchorGrid = ShiftClickGrid.None;
        private int _shiftAnchorIndex = -1;

        // ── State ─────────────────────────────────────────────────────────────
        private IconPack _targetPack;
        private double _updateCompleteTime = -1;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            InitialiseProviders();
        }

        private void OnDisable()
        {
            _previewCoroutine?.Stop();
            foreach (var tex in _previewCache.Values.Where(t => t))
                DestroyImmediate(tex);
            _previewCache.Clear();
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_targetPack)
            {
                EditorGUILayout.HelpBox("No icon pack loaded. Close and reopen from the pack inspector.", MessageType.Warning);
                return;
            }

            if (_providers.Count == 0)
            {
                EditorGUILayout.HelpBox("No providers assigned. Add providers to the icon pack first.", MessageType.Warning);
                return;
            }

            if (!_manifestCache.Values.Any(m => m != null))
            {
                EditorGUILayout.HelpBox("No manifest found for any provider. Download or build a manifest first.", MessageType.Info);
                return;
            }

            DrawVariantBar();
            DrawSearchBar();
            DrawIncludedGrid();
            DrawBrowseGrid();
            DrawControls();
        }

        public static void ShowWindow(IconPack pack)
        {
            var window = GetWindow<IconManagerWindow>("Icon Manager");
            window._targetPack = pack;
            window.minSize = new Vector2(522, 750);
            window.maxSize = new Vector2(522, 2000);
            window.InitialiseProviders();
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitialiseProviders()
        {
            _providers = _targetPack?.Providers?.Where(p => p != null).ToList() ?? new List<IconProvider>();
            _manifestCache.Clear();

            var variantSet = new HashSet<string>();
            foreach (var p in _providers)
            {
                _manifestCache[ProviderKey(p)] = p.LoadManifest();

                if (p.Variants.Count == 0)
                    variantSet.Add("");
                else
                    foreach (var v in p.Variants)
                        variantSet.Add(v);
            }

            _allVariants = variantSet
                .OrderBy(v => string.IsNullOrEmpty(v) ? NoVariantsName : v)
                .ToList();

            if (_targetPack && _targetPack.ActiveVariants.Any())
                _activeVariants = new HashSet<string>(_targetPack.ActiveVariants);
            else
                _activeVariants = new HashSet<string>(_allVariants);

            _pendingAdditions.Clear();
            _pendingDeletions.Clear();
            RefreshFiltered();
        }

        // ── Variant bar ───────────────────────────────────────────────────────

        private void DrawVariantBar()
        {
            EditorGUILayout.LabelField("Variants", EditorStyles.boldLabel);

            var changed = false;
            foreach (var provider in _providers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(provider.name, EditorStyles.boldLabel, GUILayout.Width(150));

                IEnumerable<string> variants = provider.Variants.Count == 0
                    ? new[] { "" }
                    : provider.Variants;

                foreach (var variant in variants)
                {
                    var label = string.IsNullOrEmpty(variant) ? NoVariantsName : char.ToUpper(variant[0]) + variant[1..];
                    var wasActive = _activeVariants.Contains(variant);

                    GUI.backgroundColor = wasActive ? VariantOnColor : VariantOffColor;
                    var isActive = GUILayout.Toggle(wasActive, label, EditorStyles.miniButton, GUILayout.Width(70));
                    GUI.backgroundColor = Color.white;

                    if (isActive == wasActive) continue;
                    if (isActive) _activeVariants.Add(variant);
                    else _activeVariants.Remove(variant);
                    changed = true;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (changed)
            {
                _targetPack.ActiveVariants.Clear();
                _targetPack.ActiveVariants.AddRange(_activeVariants);
                EditorUtility.SetDirty(_targetPack);
                AssetDatabase.SaveAssets();
                _browsePage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.Space(8);
        }

        // ── Search ────────────────────────────────────────────────────────────

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

        // ── Included grid ─────────────────────────────────────────────────────

        private void DrawIncludedGrid()
        {
            EnsureStyles();
            EditorGUILayout.LabelField($"Included Icons ({_filteredIncluded.Count} shown)", EditorStyles.boldLabel);

            var pageCount = Mathf.Max(1, (_filteredIncluded.Count + IconsPerPage - 1) / IconsPerPage);
            _includedPage = Mathf.Clamp(_includedPage, 0, pageCount - 1);

            var startIdx = _includedPage * IconsPerPage;
            var endIdx = Mathf.Min(startIdx + IconsPerPage, _filteredIncluded.Count);

            _includedScroll = EditorGUILayout.BeginScrollView(_includedScroll, false, false,
                GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.Height(160));
            EditorGUILayout.BeginHorizontal();
            var col = 0;

            for (var i = startIdx; i < endIdx; i++)
            {
                var entry = _filteredIncluded[i];
                var key = EntryKey(entry);
                var isPendingDeletion = _pendingDeletions.Contains(key);
                var state = isPendingDeletion ? IconCellState.PendingDeletion : IconCellState.Imported;

                var clickType = DrawIconCell(entry, state);
                if (clickType != CellClickType.None)
                {
                    if (clickType == CellClickType.Shift &&
                        _shiftAnchorGrid == ShiftClickGrid.Included &&
                        _shiftAnchorIndex >= 0)
                    {
                        ApplyRangeToggleIncluded(_shiftAnchorIndex, i);
                    }
                    else
                    {
                        if (isPendingDeletion) _pendingDeletions.Remove(key);
                        else _pendingDeletions.Add(key);
                        _shiftAnchorGrid = ShiftClickGrid.Included;
                        _shiftAnchorIndex = i;
                    }

                    GUI.changed = true;
                }

                col++;
                if (col < IconGridColumns) continue;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                col = 0;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Page {_includedPage + 1} of {pageCount}", GUILayout.Width(100));
            if (GUILayout.Button("< Prev", GUILayout.Width(70)))
            {
                _includedPage = Mathf.Max(0, _includedPage - 1);
                ClearShiftAnchor();
            }

            if (GUILayout.Button("Next >", GUILayout.Width(70)))
            {
                _includedPage = Mathf.Min(pageCount - 1, _includedPage + 1);
                ClearShiftAnchor();
            }

            GUILayout.FlexibleSpace();

            if (_pendingDeletions.Any())
                if (GUILayout.Button("Unmark all", GUILayout.Width(90)))
                    _pendingDeletions.Clear();

            if (GUILayout.Button("Mark all for removal", GUILayout.Width(130)))
                foreach (var icon in _targetPack.Icons.Where(i => i.provider != null))
                    _pendingDeletions.Add(EntryKey(icon));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        // ── Browse grid ───────────────────────────────────────────────────────

        private void DrawBrowseGrid()
        {
            EnsureStyles();
            EditorGUILayout.LabelField("Available Icons", EditorStyles.boldLabel);

            var pageCount = Mathf.Max(1, (_filteredBrowse.Count + IconsPerPage - 1) / IconsPerPage);
            _browsePage = Mathf.Clamp(_browsePage, 0, pageCount - 1);

            var startIdx = _browsePage * IconsPerPage;
            var endIdx = Mathf.Min(startIdx + IconsPerPage, _filteredBrowse.Count);

            var importedKeys = new HashSet<string>(_targetPack.Icons
                .Where(i => i.provider != null)
                .Select(EntryKey));

            _browseScroll = EditorGUILayout.BeginScrollView(_browseScroll, GUILayout.Height(265));
            EditorGUILayout.BeginHorizontal();
            var col = 0;

            for (var i = startIdx; i < endIdx; i++)
            {
                var entry = _filteredBrowse[i];
                var key = EntryKey(entry);
                var isImported = importedKeys.Contains(key);
                var isPendingDeletion = _pendingDeletions.Contains(key);
                var isPendingAddition = _pendingAdditions.ContainsKey(key);

                IconCellState state;
                if (isImported)
                    state = isPendingDeletion ? IconCellState.PendingDeletion : IconCellState.Imported;
                else
                    state = isPendingAddition ? IconCellState.PendingAdd : IconCellState.NotImported;

                var clickType = DrawIconCell(entry, state);
                if (clickType != CellClickType.None)
                {
                    if (clickType == CellClickType.Shift &&
                        _shiftAnchorGrid == ShiftClickGrid.Browse &&
                        _shiftAnchorIndex >= 0)
                    {
                        ApplyRangeToggleBrowse(_shiftAnchorIndex, i, importedKeys);
                    }
                    else
                    {
                        if (isImported)
                        {
                            if (isPendingDeletion) _pendingDeletions.Remove(key);
                            else _pendingDeletions.Add(key);
                        }
                        else
                        {
                            if (isPendingAddition) _pendingAdditions.Remove(key);
                            else _pendingAdditions[key] = entry;
                        }

                        _shiftAnchorGrid = ShiftClickGrid.Browse;
                        _shiftAnchorIndex = i;
                    }

                    GUI.changed = true;
                }

                col++;
                if (col < IconGridColumns) continue;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                col = 0;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Page {_browsePage + 1} of {pageCount}", GUILayout.Width(100));
            if (GUILayout.Button("< Prev", GUILayout.Width(70)))
            {
                _browsePage = Mathf.Max(0, _browsePage - 1);
                ClearShiftAnchor();
            }

            if (GUILayout.Button("Next >", GUILayout.Width(70)))
            {
                _browsePage = Mathf.Min(pageCount - 1, _browsePage + 1);
                ClearShiftAnchor();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Browsing {_filteredBrowse.Count} icons", GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        // ── Controls ──────────────────────────────────────────────────────────

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
                    GUILayout.Label($"Add: {addCount} icon{(addCount == 1 ? "" : "s")}",
                        new GUIStyle(GUI.skin.label) { normal = { textColor = PendingAddColor } });

                if (removeCount > 0)
                    GUILayout.Label($"Remove: {removeCount} icon{(removeCount == 1 ? "" : "s")}",
                        new GUIStyle(GUI.skin.label) { normal = { textColor = DangerColor } });

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (_updateCompleteTime > 0 && EditorApplication.timeSinceStartup - _updateCompleteTime < 5.0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Update complete.", new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = UpdateColor },
                    fontStyle = FontStyle.Bold
                });
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                Repaint();
            }
        }

        // ── Icon cell ─────────────────────────────────────────────────────────

        private CellClickType DrawIconCell(ProviderIconEntry entry, IconCellState state)
        {
            var preview = GetPreview(entry);
            var variantDisplay = string.IsNullOrEmpty(entry.entry.variant) ? NoVariantsName : entry.entry.variant;
            var tooltip = $"{entry.entry.name} ({variantDisplay}) [{entry.provider.name}]";

            var cellRect = GUILayoutUtility.GetRect(IconPreviewSize, IconPreviewSize, _iconCellStyle,
                GUILayout.Width(IconPreviewSize), GUILayout.Height(IconPreviewSize));

            var clicked = GUI.Button(cellRect, new GUIContent("", tooltip), _iconCellStyle);

            // Capture shift state immediately after GUI.Button — Event.current.Use() sets type to Used
            // but leaves modifiers intact, so Event.current.shift is still valid here.
            var result = clicked
                ? Event.current.shift ? CellClickType.Shift : CellClickType.Normal
                : CellClickType.None;

            if (Event.current.type != EventType.Repaint)
                return result;

            if (preview)
                GUI.DrawTexture(cellRect, preview, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(cellRect, "…", _centeredLabelStyle);

            switch (state)
            {
                case IconCellState.Imported: DrawBorder(cellRect, UpdateColor, PreviewBorderWidth); break;
                case IconCellState.PendingDeletion: DrawBorder(cellRect, DangerColor, PreviewBorderWidth); break;
                case IconCellState.PendingAdd: DrawBorder(cellRect, PendingAddColor, PreviewBorderWidth); break;
            }

            return result;
        }

        private static void DrawBorder(Rect rect, Color color, int width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        // ── Shift-click range helpers ─────────────────────────────────────────

        private void ApplyRangeToggleIncluded(int anchorIndex, int targetIndex)
        {
            var lo = Mathf.Min(anchorIndex, targetIndex);
            var hi = Mathf.Max(anchorIndex, targetIndex);
            for (var r = lo; r <= hi; r++)
            {
                if (r == anchorIndex) continue;
                var rangeKey = EntryKey(_filteredIncluded[r]);
                if (!_pendingDeletions.Add(rangeKey))
                    _pendingDeletions.Remove(rangeKey);
            }
        }

        private void ApplyRangeToggleBrowse(int anchorIndex, int targetIndex, HashSet<string> importedKeys)
        {
            var lo = Mathf.Min(anchorIndex, targetIndex);
            var hi = Mathf.Max(anchorIndex, targetIndex);
            for (var r = lo; r <= hi; r++)
            {
                if (r == anchorIndex) continue;
                var rangeEntry = _filteredBrowse[r];
                var rangeKey = EntryKey(rangeEntry);

                if (importedKeys.Contains(rangeKey))
                {
                    if (_pendingDeletions.Contains(rangeKey)) _pendingDeletions.Remove(rangeKey);
                    else _pendingDeletions.Add(rangeKey);
                }
                else
                {
                    if (_pendingAdditions.ContainsKey(rangeKey)) _pendingAdditions.Remove(rangeKey);
                    else _pendingAdditions[rangeKey] = rangeEntry;
                }
            }
        }

        private void ClearShiftAnchor()
        {
            _shiftAnchorGrid = ShiftClickGrid.None;
            _shiftAnchorIndex = -1;
        }

        // ── Preview ───────────────────────────────────────────────────────────

        private Texture2D GetPreview(ProviderIconEntry entry)
        {
            var key = PreviewKey(entry);
            if (_previewCache.TryGetValue(key, out var cached)) return cached;

            _previewCache[key] = null;

            _previewTempFolder ??= Path.Combine(Application.temporaryCachePath, "IconPreviews");
            Directory.CreateDirectory(_previewTempFolder);

            var svgPath = entry.provider?.GetSvgPath(entry.entry.name, entry.entry.variant);
            if (string.IsNullOrEmpty(svgPath) || !File.Exists(svgPath)) return null;

            var pngPath = Path.Combine(_previewTempFolder, $"{key}.png");

            if (File.Exists(pngPath))
            {
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

        // Runs on main thread each frame, loading completed PNGs as Texture2D.
        private IEnumerator DrainReadyQueue()
        {
            while (_inFlightCount > 0 || !_readyToLoad.IsEmpty)
            {
                var repaintNeeded = false;
                while (_readyToLoad.TryDequeue(out var key))
                {
                    var pngPath = Path.Combine(_previewTempFolder, $"{key}.png");
                    if (!File.Exists(pngPath)) continue;

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

        // ── Update ────────────────────────────────────────────────────────────

        private void OnUpdateIcons()
        {
            var iconsToRemove = new HashSet<string>(_pendingDeletions);
            if (!ConfirmIfWillRemoveIcons(iconsToRemove)) return;

            _targetPack.Icons.RemoveAll(i => i.provider && iconsToRemove.Contains(EntryKey(i)));

            var currentKeys = new HashSet<string>(_targetPack.Icons
                .Where(i => i.provider)
                .Select(EntryKey));

            foreach (var (key, entry) in _pendingAdditions)
                if (!currentKeys.Contains(key))
                    _targetPack.Icons.Add(new IconPack.PackedIcon
                    {
                        iconName = entry.entry.name,
                        variant = entry.entry.variant,
                        provider = entry.provider
                    });

            _pendingDeletions.Clear();
            _pendingAdditions.Clear();
            RefreshFiltered();

            IconImportProcessor.StartUpdate(_targetPack, this, () =>
            {
                _updateCompleteTime = EditorApplication.timeSinceStartup;
                Repaint();
            });
        }

        private bool ConfirmIfWillRemoveIcons(HashSet<string> deletionKeys)
        {
            var removed = _targetPack.Icons
                .Where(i => i.provider && deletionKeys.Contains(EntryKey(i)))
                .Select(i =>
                {
                    var v = string.IsNullOrEmpty(i.variant) ? NoVariantsName : i.variant;
                    return $"{i.iconName} ({v}) [{i.provider?.name}]";
                })
                .ToList();

            if (removed.Count == 0) return true;

            var sb = new StringBuilder();
            sb.AppendLine($"{removed.Count} icon(s) will be removed. Any references will be lost:\n");
            const int maxDisplay = 10;
            for (var i = 0; i < Mathf.Min(removed.Count, maxDisplay); i++)
                sb.AppendLine($"  - {removed[i]}");
            if (removed.Count > maxDisplay)
                sb.AppendLine($"  ... and {removed.Count - maxDisplay} more.");
            sb.AppendLine("\nContinue with update?");

            return EditorUtility.DisplayDialog("Remove Icons?", sb.ToString(), "Remove and Update", "Cancel");
        }

        // ── Filtering ─────────────────────────────────────────────────────────

        private void RefreshFiltered()
        {
            _filteredIncluded.Clear();
            _filteredBrowse.Clear();
            ClearShiftAnchor();

            if (!_targetPack) return;

            var hasSearch = !string.IsNullOrEmpty(_searchText);
            var currentKeys = new HashSet<string>(_targetPack.Icons
                .Where(i => i.provider)
                .Select(EntryKey));

            foreach (var provider in _providers)
            {
                if (!_manifestCache.TryGetValue(ProviderKey(provider), out var manifest) || manifest == null)
                    continue;

                foreach (var icon in manifest.icons)
                {
                    if (!_activeVariants.Contains(icon.variant)) continue;

                    var entry = new ProviderIconEntry(icon, provider);
                    var key = EntryKey(entry);

                    if (currentKeys.Contains(key))
                        if (!hasSearch || MatchesSearch(icon))
                            _filteredIncluded.Add(entry);

                    if (hasSearch && !MatchesSearch(icon)) continue;
                    _filteredBrowse.Add(entry);
                }
            }
        }

        private bool MatchesSearch(IconEntry icon)
        {
            return icon.name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                   (icon.aliases != null && icon.aliases.Any(a =>
                       a.Contains(_searchText, StringComparison.OrdinalIgnoreCase)));
        }

        // ── Keys ──────────────────────────────────────────────────────────────

        private static string ProviderKey(IconProvider provider)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(provider));
        }

        private static string EntryKey(ProviderIconEntry entry)
        {
            return $"{ProviderKey(entry.provider)}/{entry.entry.variant}/{entry.entry.name}";
        }

        private static string EntryKey(IconPack.PackedIcon icon)
        {
            return $"{ProviderKey(icon.provider)}/{icon.variant}/{icon.iconName}";
        }

        private static string PreviewKey(ProviderIconEntry entry)
        {
            return $"{ProviderKey(entry.provider)}-{entry.entry.name}-{entry.entry.variant}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_iconCellStyle != null) return;
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

        // ── Icon entry type ───────────────────────────────────────────────────
        private readonly struct ProviderIconEntry
        {
            public readonly IconEntry entry;
            public readonly IconProvider provider;

            public ProviderIconEntry(IconEntry entry, IconProvider provider)
            {
                this.entry = entry;
                this.provider = provider;
            }
        }

        private enum IconCellState
        {
            NotImported,
            PendingAdd,
            Imported,
            PendingDeletion
        }

        private enum CellClickType
        {
            None,
            Normal,
            Shift
        }

        private enum ShiftClickGrid
        {
            None,
            Included,
            Browse
        }
    }
}