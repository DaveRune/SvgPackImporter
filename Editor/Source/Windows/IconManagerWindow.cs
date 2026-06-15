using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightForge.SvgPackImporter.Providers;
using KnightForge.SvgPackImporter.Utilities;
using UnityEditor;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Windows
{
    internal sealed class IconManagerWindow : EditorWindow
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const int IconPreviewSize = 50;
        private const int IconsPerPage = 50;
        private const int IconGridColumns = 10;
        private const int PreviewBorderWidth = 2;
        private const int PreviewMaxAttempts = 3;

        private const string NoVariantsName = "All";

        private static readonly Color UpdateColor = new(0.25f, 0.65f, 0.25f);
        private static readonly Color DangerColor = new(0.75f, 0.25f, 0.25f);
        private static readonly Color PendingAddColor = new(0.2f, 0.45f, 0.75f);
        private static readonly Color VariantOnColor = new(0.3f, 0.75f, 0.3f);
        private static readonly Color VariantOffColor = new(0.45f, 0.45f, 0.45f);
        private static readonly Color MissingBgColor = new(0.55f, 0.1f, 0.1f, 0.45f);
        private static readonly Color TooltipBgColor = new(0.1f, 0.1f, 0.1f, 0.93f);
        private static readonly Color MissingTooltipTextColor = new(0.95f, 0.25f, 0.25f, 1f);

        // ── Preview throttle ──────────────────────────────────────────────────
        private static readonly SemaphoreSlim GenerationThrottle = new(
            Mathf.Max(1, Environment.ProcessorCount - 1),
            Mathf.Max(1, Environment.ProcessorCount - 1));
        private readonly Dictionary<string, Texture2D> _embeddedTextureFallback = new();

        private readonly List<ProviderIconEntry> _filteredBrowse = new();
        private readonly List<ProviderIconEntry> _filteredIncluded = new();

        private readonly Dictionary<string, IconManifest> _manifestCache = new();
        private readonly HashSet<string> _missingSvgKeys = new();
        private readonly Dictionary<string, ProviderIconEntry> _pendingAdditions = new();
        private readonly HashSet<string> _pendingDeletions = new();

        private readonly Dictionary<string, Texture2D> _previewCache = new();
        private readonly Dictionary<string, int> _previewAttempts = new();
        private readonly HashSet<string> _failedPreviews = new();
        private readonly ConcurrentQueue<string> _readyToLoad = new();

        private HashSet<string> _activeVariants = new();
        private int _browsePage;
        private Vector2 _browseScroll;

        private GUIStyle _centeredLabelStyle;
        private GUIStyle _clearButtonStyle;
        private bool _hoveredIsMissing;

        // ── Hover tooltip ─────────────────────────────────────────────────────
        private string _hoveredTooltip;
        private GUIStyle _iconCellStyle;

        private int _includedPage;
        private Vector2 _includedScroll;
        private int _inFlightCount;
        private GUIStyle _missingTooltipLabelStyle;
        private GUIStyle _pendingAddLabelStyle;
        private GUIStyle _pendingRemoveLabelStyle;
        private EditorCoroutine _previewCoroutine;
        private string _previewTempFolder;
        private List<IconProvider> _providers = new();

        private string _searchText = "";
        private bool _filterIncluded;
        private bool _pendingAdditionsDirty;

        // ── Shift-click anchor ────────────────────────────────────────────────
        private ShiftClickGrid _shiftAnchorGrid = ShiftClickGrid.None;

        private int _shiftAnchorIndex = -1;

        // Providers that have pack icons still in the manifest but with missing source SVGs (State 1).
        private List<IconProvider> _staleManifestProviders = new();

        // ── State ─────────────────────────────────────────────────────────────
        private IconPack _targetPack;
        private GUIStyle _tooltipLabelStyle;
        private GUIStyle _updateCompleteLabelStyle;
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
            _previewAttempts.Clear();
            _failedPreviews.Clear();
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            UpdateUnsavedChangesFlag();

            _hoveredTooltip = null;
            _hoveredIsMissing = false;

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

            if (_manifestCache.Values.All(m => m == null))
            {
                EditorGUILayout.HelpBox("No manifest found for any provider. Download or build a manifest first.", MessageType.Info);
                return;
            }

            EnsureStyles();
            DrawVariantBar();
            DrawSearchBar();
            DrawMissingSourceWarning();
            DrawIncludedGrid();
            DrawBrowseGrid();
            DrawControls();
            DrawHoverTooltip();

            if (_pendingAdditionsDirty)
            {
                _pendingAdditionsDirty = false;
                RefreshFiltered();
                Repaint();
            }
        }

        public static void ShowWindow(IconPack pack)
        {
            var window = GetWindow<IconManagerWindow>("Icon Manager");
            window._targetPack = pack;
            window.minSize = new Vector2(522, 750);
            window.maxSize = new Vector2(522, 2000);
            window.saveChangesMessage = "You have pending icon additions or removals.\nApply them before closing?";
            window.InitialiseProviders();
        }

        // ── Unsaved changes ──────────────────────────────────────────────────

        private void UpdateUnsavedChangesFlag()
        {
            hasUnsavedChanges = _pendingAdditions.Count > 0 || _pendingDeletions.Count > 0;
        }

        public override void SaveChanges()
        {
            OnUpdateIcons();
            UpdateUnsavedChangesFlag();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            _pendingAdditions.Clear();
            _pendingDeletions.Clear();
            _pendingAdditionsDirty = true;
            UpdateUnsavedChangesFlag();
            base.DiscardChanges();
        }

        // ── Initialisation ────────────────────────────────────────────────────

        private void InitialiseProviders()
        {
            _providers = _targetPack?.Providers?.Where(p => p).ToList() ?? new List<IconProvider>();
            _manifestCache.Clear();

            var variantSet = new HashSet<string>();
            foreach (var provider in _providers)
            {
                _manifestCache[ProviderKey(provider)] = provider.LoadManifest();
                var providerKey = ProviderKey(provider);

                if (provider.Variants.Count == 0)
                    variantSet.Add($"{providerKey}/");
                else
                    foreach (var v in provider.Variants)
                        variantSet.Add($"{providerKey}/{v}");
            }

            if (_targetPack && _targetPack.ActiveVariants.Any())
            {
                var saved = new HashSet<string>(_targetPack.ActiveVariants);
                var trackedGuids = new HashSet<string>(saved.Select(v => v.Split('/')[0]));

                var loaded = new HashSet<string>(saved);
                loaded.IntersectWith(variantSet);

                foreach (var variant in variantSet.Where(variant => !trackedGuids.Contains(variant.Split('/')[0])))
                    loaded.Add(variant);

                _activeVariants = loaded.Count > 0 ? loaded : new HashSet<string>(variantSet);
            }
            else
            {
                _activeVariants = new HashSet<string>(variantSet);
            }

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
                    var activeKey = $"{ProviderKey(provider)}/{variant}";
                    var wasActive = _activeVariants.Contains(activeKey);

                    GUI.backgroundColor = wasActive ? VariantOnColor : VariantOffColor;
                    var isActive = GUILayout.Toggle(wasActive, label, EditorStyles.miniButton, GUILayout.Width(70));
                    GUI.backgroundColor = Color.white;

                    if (isActive == wasActive) continue;
                    if (isActive) _activeVariants.Add(activeKey);
                    else _activeVariants.Remove(activeKey);
                    changed = true;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (changed)
            {
                _targetPack.ActiveVariants.Clear();
                _targetPack.ActiveVariants.AddRange(_activeVariants);
                EditorUtility.SetDirty(_targetPack);
                _browsePage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.Space(8);
        }

        // ── Search ────────────────────────────────────────────────────────────

        private void DrawSearchBar()
        {
            EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);

            EnsureClearButtonStyle();
            EditorGUILayout.BeginHorizontal();
            var newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(newSearch)))
            {
                if (GUILayout.Button("✕", _clearButtonStyle, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    newSearch = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                _browsePage = 0;
                _includedPage = 0;
                RefreshFiltered();
            }

            EditorGUILayout.Space(8);
        }

        // ── Missing source warning ─────────────────────────────────────────────

        private void DrawMissingSourceWarning()
        {
            if (_staleManifestProviders.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            foreach (var provider in _staleManifestProviders.Where(provider => GUILayout.Button($"Rebuild: {provider.name}", GUILayout.Height(22))))
            {
                provider.BuildManifest();
                InitialiseProviders();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // ── Included grid ─────────────────────────────────────────────────────

        private void DrawIncludedGrid()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Included Icons ({_filteredIncluded.Count} shown)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var newFilterIncluded = GUILayout.Toggle(_filterIncluded, "Filter");
            EditorGUILayout.EndHorizontal();

            if (newFilterIncluded != _filterIncluded)
            {
                _filterIncluded = newFilterIncluded;
                _includedPage = 0;
                RefreshFiltered();
            }

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
                var isPendingAddition = _pendingAdditions.ContainsKey(key);

                IconCellState state;
                if (isPendingAddition) state = IconCellState.PendingAdd;
                else if (isPendingDeletion) state = IconCellState.PendingDeletion;
                else if (_missingSvgKeys.Contains(key)) state = IconCellState.MissingFromManifest;
                else state = IconCellState.Imported;

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
                        if (isPendingAddition)
                        {
                            _pendingAdditions.Remove(key);
                            _pendingAdditionsDirty = true;
                        }
                        else if (isPendingDeletion) _pendingDeletions.Remove(key);
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
            EditorGUILayout.LabelField("Available Icons", EditorStyles.boldLabel);

            var pageCount = Mathf.Max(1, (_filteredBrowse.Count + IconsPerPage - 1) / IconsPerPage);
            _browsePage = Mathf.Clamp(_browsePage, 0, pageCount - 1);

            var startIdx = _browsePage * IconsPerPage;
            var endIdx = Mathf.Min(startIdx + IconsPerPage, _filteredBrowse.Count);

            var importedKeys = new HashSet<string>(_targetPack.Icons
                .Where(i => i.provider)
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
                        _pendingAdditionsDirty = true;
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
                            _pendingAdditionsDirty = true;
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
                    GUILayout.Label($"Add: {addCount} icon{(addCount == 1 ? "" : "s")}", _pendingAddLabelStyle);

                if (removeCount > 0)
                    GUILayout.Label($"Remove: {removeCount} icon{(removeCount == 1 ? "" : "s")}", _pendingRemoveLabelStyle);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (_updateCompleteTime > 0 && EditorApplication.timeSinceStartup - _updateCompleteTime < 5.0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Update complete.", _updateCompleteLabelStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                Repaint();
            }
        }

        // ── Icon cell ─────────────────────────────────────────────────────────

        private CellClickType DrawIconCell(ProviderIconEntry entry, IconCellState state)
        {
            var preview = GetPreview(entry);

            // Fall back to the embedded texture for any icon whose source SVG is missing,
            // regardless of pending-deletion state, so the icon always shows its graphic.
            if (!preview)
                _embeddedTextureFallback.TryGetValue(EntryKey(entry), out preview);

            var variantDisplay = string.IsNullOrEmpty(entry.entry.variant) ? NoVariantsName : entry.entry.variant;
            var tooltip = $"{entry.entry.name} ({variantDisplay}) [{entry.provider.name}]";

            var cellRect = GUILayoutUtility.GetRect(IconPreviewSize, IconPreviewSize, _iconCellStyle,
                GUILayout.Width(IconPreviewSize), GUILayout.Height(IconPreviewSize));

            // Empty GUIContent — Unity's built-in tooltip is suppressed in favour of DrawHoverTooltip.
            var clicked = GUI.Button(cellRect, GUIContent.none, _iconCellStyle);

            // Capture shift state immediately after GUI.Button — Event.current.Use() sets type to Used
            // but leaves modifiers intact, so Event.current.shift is still valid here.
            var result = clicked
                ? Event.current.shift ? CellClickType.Shift : CellClickType.Normal
                : CellClickType.None;

            if (Event.current.type != EventType.Repaint)
                return result;

            if (state == IconCellState.MissingFromManifest || _missingSvgKeys.Contains(EntryKey(entry)))
                EditorGUI.DrawRect(cellRect, MissingBgColor);

            if (preview)
                GUI.DrawTexture(cellRect, preview, ScaleMode.ScaleToFit, true);
            else if (_failedPreviews.Contains(PreviewKey(entry)))
                GUI.Label(cellRect, "⚠", _centeredLabelStyle);
            else
                GUI.Label(cellRect, "…", _centeredLabelStyle);

            switch (state)
            {
                case IconCellState.Imported: EditorGuiHelpers.DrawBorder(cellRect, UpdateColor, PreviewBorderWidth); break;
                case IconCellState.PendingDeletion: EditorGuiHelpers.DrawBorder(cellRect, DangerColor, PreviewBorderWidth); break;
                case IconCellState.PendingAdd: EditorGuiHelpers.DrawBorder(cellRect, PendingAddColor, PreviewBorderWidth); break;
            }

            // Track hover for the custom tooltip drawn at the end of OnGUI (window-space coordinates).
            if (cellRect.Contains(Event.current.mousePosition))
            {
                _hoveredTooltip = tooltip;
                _hoveredIsMissing = _missingSvgKeys.Contains(EntryKey(entry));
            }

            return result;
        }

        // ── Hover tooltip ─────────────────────────────────────────────────────

        private void DrawHoverTooltip()
        {
            if (string.IsNullOrEmpty(_hoveredTooltip)) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();

            const float padding = 5f;
            const float lineH = 15f;
            const float gap = 2f;

            var mainSize = _tooltipLabelStyle.CalcSize(new GUIContent(_hoveredTooltip));
            var boxW = mainSize.x + padding * 2f;
            var boxH = lineH + padding * 2f;

            if (_hoveredIsMissing)
            {
                var missingSize = _missingTooltipLabelStyle.CalcSize(new GUIContent("Missing Source SVG"));
                boxW = Mathf.Max(boxW, missingSize.x + padding * 2f);
                boxH += gap + lineH;
            }

            var mouse = Event.current.mousePosition;
            var x = Mathf.Min(mouse.x + 14f, position.width - boxW - 4f);
            var y = Mathf.Min(mouse.y + 14f, position.height - boxH - 4f);

            EditorGUI.DrawRect(new Rect(x, y, boxW, boxH), TooltipBgColor);
            GUI.Label(new Rect(x + padding, y + padding, boxW - padding * 2f, lineH),
                _hoveredTooltip, _tooltipLabelStyle);

            if (_hoveredIsMissing)
                GUI.Label(new Rect(x + padding, y + padding + lineH + gap, boxW - padding * 2f, lineH),
                    "Missing Source SVG", _missingTooltipLabelStyle);

            Repaint();
        }

        // ── Shift-click range helpers ─────────────────────────────────────────

        private void ApplyRangeToggleIncluded(int anchorIndex, int targetIndex)
        {
            var low = Mathf.Min(anchorIndex, targetIndex);
            var high = Mathf.Max(anchorIndex, targetIndex);
            for (var range = low; range <= high; range++)
            {
                if (range == anchorIndex) continue;
                var rangeKey = EntryKey(_filteredIncluded[range]);
                if (!_pendingDeletions.Add(rangeKey))
                    _pendingDeletions.Remove(rangeKey);
            }
        }

        private void ApplyRangeToggleBrowse(int anchorIndex, int targetIndex, HashSet<string> importedKeys)
        {
            var low = Mathf.Min(anchorIndex, targetIndex);
            var high = Mathf.Max(anchorIndex, targetIndex);
            for (var range = low; range <= high; range++)
            {
                if (range == anchorIndex) continue;
                var rangeEntry = _filteredBrowse[range];
                var rangeKey = EntryKey(rangeEntry);

                if (importedKeys.Contains(rangeKey))
                {
                    if (!_pendingDeletions.Add(rangeKey))
                        _pendingDeletions.Remove(rangeKey);
                }
                else
                {
                    if (!_pendingAdditions.Remove(rangeKey))
                        _pendingAdditions[rangeKey] = rangeEntry;
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
            if (_failedPreviews.Contains(key)) return null;
            if (_previewCache.TryGetValue(key, out var cached)) return cached;

            _previewCache[key] = null;

            _previewTempFolder ??= Path.Combine(Application.temporaryCachePath, "IconPreviews");
            Directory.CreateDirectory(_previewTempFolder);

            if (!entry.provider || !entry.provider.HasSourceFor(entry.entry.name, entry.entry.variant)) return null;
            var svgPath = entry.provider.GetSvgPath(entry.entry.name, entry.entry.variant);

            var pngPath = Path.Combine(_previewTempFolder, $"{key}.png");

            if (File.Exists(pngPath))
            {
                _readyToLoad.Enqueue(key);
            }
            else
            {
                var provider = entry.provider;
                var variant = entry.entry.variant;

                Interlocked.Increment(ref _inFlightCount);
                Task.Run(async () =>
                {
                    await GenerationThrottle.WaitAsync();
                    try
                    {
                        ImageMagickConverter.TryGeneratePreviewFromContent(svgPath, pngPath, IconPreviewSize, provider, variant);
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
                    {
                        // Conversion produced no file. Retry a few times, then mark the key failed so the
                        // cell shows a warning instead of looking identical to a still-loading cell forever.
                        var attempts = (_previewAttempts.TryGetValue(key, out var a) ? a : 0) + 1;
                        _previewAttempts[key] = attempts;
                        if (attempts >= PreviewMaxAttempts) _failedPreviews.Add(key);
                        else _previewCache.Remove(key);
                        repaintNeeded = true;
                        continue;
                    }

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

            _targetPack.RemoveIconsWhere(i => i.provider && iconsToRemove.Contains(EntryKey(i)));

            var currentKeys = new HashSet<string>(_targetPack.Icons
                .Where(i => i.provider)
                .Select(EntryKey));

            foreach (var (key, entry) in _pendingAdditions)
                if (!currentKeys.Contains(key))
                    _targetPack.AddIcon(new IconPack.PackedIcon
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
            _missingSvgKeys.Clear();
            _embeddedTextureFallback.Clear();
            ClearShiftAnchor();

            if (!_targetPack) return;

            var hasSearch = !string.IsNullOrEmpty(_searchText);
            var currentKeys = new HashSet<string>(_targetPack.Icons
                .Where(i => i.provider)
                .Select(EntryKey));

            // Pre-build lookup so we can grab embedded textures without repeated LINQ scans.
            var packIconByKey = _targetPack.Icons
                .Where(i => i.provider != null)
                .ToDictionary(EntryKey, i => i);

            var matchedKeys = new HashSet<string>();
            var staleProviders = new HashSet<IconProvider>();

            foreach (var provider in _providers)
            {
                if (!_manifestCache.TryGetValue(ProviderKey(provider), out var manifest) || manifest == null)
                    continue;

                foreach (var icon in manifest.icons)
                {
                    if (!_activeVariants.Contains($"{ProviderKey(provider)}/{icon.variant}")) continue;

                    var entry = new ProviderIconEntry(icon, provider);
                    var key = EntryKey(entry);

                    if (currentKeys.Contains(key))
                    {
                        matchedKeys.Add(key);

                        // State 1: icon is in the pack and in the manifest, but SVG is gone from disk.
                        if (!provider.HasSourceFor(icon.name, icon.variant))
                        {
                            _missingSvgKeys.Add(key);
                            staleProviders.Add(provider);
                            if (packIconByKey.TryGetValue(key, out var packed) && packed.texture != null)
                                _embeddedTextureFallback[key] = packed.texture;
                        }

                        if (!hasSearch || !_filterIncluded || MatchesSearch(icon))
                            _filteredIncluded.Add(entry);
                    }

                    if (hasSearch && !MatchesSearch(icon)) continue;
                    _filteredBrowse.Add(entry);
                }
            }

            _staleManifestProviders = staleProviders.ToList();

            // Pending additions are mirrored into the Included grid so the user can see what's queued.
            foreach (var (key, entry) in _pendingAdditions)
            {
                if (matchedKeys.Contains(key)) continue;
                if (!entry.provider) continue;
                if (!_activeVariants.Contains($"{ProviderKey(entry.provider)}/{entry.entry.variant}")) continue;
                if (hasSearch && _filterIncluded && !MatchesSearch(entry.entry)) continue;
                _filteredIncluded.Add(entry);
            }

            // State 2: icon is in the pack but no longer in any manifest (manifest was rebuilt after SVG deletion).
            // Skip variants that are currently inactive — they are simply hidden, not orphaned.
            foreach (var packed in _targetPack.Icons)
            {
                if (!packed.provider) continue;
                if (!_activeVariants.Contains($"{ProviderKey(packed.provider)}/{packed.variant}")) continue;
                var key = EntryKey(packed);
                if (matchedKeys.Contains(key)) continue;

                _missingSvgKeys.Add(key);
                if (packed.texture != null)
                    _embeddedTextureFallback[key] = packed.texture;

                var synthetic = new IconEntry { name = packed.iconName, variant = packed.variant };
                if (hasSearch && _filterIncluded && !MatchesSearch(synthetic)) continue;
                _filteredIncluded.Add(new ProviderIconEntry(synthetic, packed.provider));
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
            _pendingAddLabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = PendingAddColor }
            };
            _pendingRemoveLabelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = DangerColor }
            };
            _updateCompleteLabelStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = UpdateColor },
                fontStyle = FontStyle.Bold
            };
            _tooltipLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white }
            };
            _missingTooltipLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = MissingTooltipTextColor },
                fontStyle = FontStyle.Bold
            };
        }

        private void EnsureClearButtonStyle()
        {
            if (_clearButtonStyle != null) return;
            _clearButtonStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(2, 2, 2, 2),
                hover = { textColor = Color.white }
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
            PendingDeletion,
            MissingFromManifest
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