using System;
using KnightForge.IconImporter.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KnightForge.IconImporter.Editor.Inspectors
{
    public sealed class IconGridView
    {
        private const int IconCellSpacing = 4;
        private const int IconCellSize = 64;
        private const int BorderWidth = 2;
        private static readonly Color DragHighlightColor = new(0.24f, 0.49f, 0.91f, 1f);
        private static readonly Color MissingBgColor = new(0.55f, 0.1f, 0.1f, 0.45f);
        private Object _dragTarget;

        private GUIStyle _iconCellStyle;

        public IconGridView(bool isPopOutWindow = false)
        {
            WidthOffset = isPopOutWindow ? 20 : 0;
        }

        private int WidthOffset { get; }

        public void Draw(IconPack pack, bool dragAsSprite, Action<bool> onDragModeChanged, Action repaint)
        {
            EnsureStyles();
            DrawDragModeToolbar(pack.Icons.Count, dragAsSprite, onDragModeChanged);
            DrawGrid(pack, dragAsSprite, repaint);
            HandleDragEvents();
        }

        private static void DrawDragModeToolbar(int iconCount, bool dragAsSprite, Action<bool> onDragModeChanged)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Icons ({iconCount})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Drag as:", GUILayout.Width(55));
            var newMode = GUILayout.Toolbar(dragAsSprite ? 0 : 1, new[] { "Sprite", "Texture2D" },
                EditorStyles.miniButton, GUILayout.Width(150)) == 0;
            EditorGUILayout.EndHorizontal();

            if (newMode != dragAsSprite)
                onDragModeChanged(newMode);

            if (!newMode)
                EditorGUILayout.LabelField(
                    "Unity bug: dragging a Texture2D into a Sprite field assigns the first Sprite found.",
                    EditorStyles.helpBox);
        }

        private void DrawGrid(IconPack pack, bool dragAsSprite, Action repaint)
        {
            const int totalCellSize = IconCellSize + IconCellSpacing;
            var availableWidth = EditorGUIUtility.currentViewWidth + WidthOffset;
            var columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / totalCellSize));

            var column = 0;
            EditorGUILayout.BeginHorizontal();

            foreach (var icon in pack.Icons)
            {
                // Stable control ID per cell so Unity routes MouseDrag/MouseUp back to this window
                // even after the mouse leaves the panel.
                var id = GUIUtility.GetControlID(FocusType.Passive);
                var variantDisplay = string.IsNullOrEmpty(icon.variant) ? "Root" : icon.variant;

                var isMissingSvg = icon.provider && !icon.provider.HasSourceFor(icon.iconName, icon.variant);
                var providerName = icon.provider ? icon.provider.name : null;

                var tooltip = isMissingSvg
                    ? $"{icon.iconName} ({variantDisplay}) [{providerName}]\n⚠ Missing Source SVG"
                    : $"{icon.iconName} ({variantDisplay}) [{providerName}]";

                var cellRect = GUILayoutUtility.GetRect(IconCellSize, IconCellSize, _iconCellStyle,
                    GUILayout.Width(IconCellSize), GUILayout.Height(IconCellSize));

                var evt = Event.current;
                var isHover = cellRect.Contains(evt.mousePosition);
                var isActive = GUIUtility.hotControl == id;

                switch (evt.type)
                {
                    case EventType.Repaint:
                        _iconCellStyle.Draw(cellRect, new GUIContent("", tooltip), isHover, isActive, false, false);
                        if (isMissingSvg)
                            EditorGUI.DrawRect(cellRect, MissingBgColor);
                        if (icon.texture)
                            GUI.DrawTexture(cellRect, icon.texture, ScaleMode.ScaleToFit, true);
                        if (_dragTarget == icon.sprite || _dragTarget == icon.texture)
                            EditorGuiHelpers.DrawBorder(cellRect, DragHighlightColor, BorderWidth);
                        break;

                    case EventType.MouseDown when evt.button == 0 && isHover:
                        GUIUtility.hotControl = id;
                        _dragTarget = dragAsSprite ? icon.sprite : icon.texture;
                        repaint();
                        evt.Use();
                        break;

                    case EventType.MouseUp when isActive:
                        GUIUtility.hotControl = 0;
                        _dragTarget = null;
                        repaint();
                        evt.Use();
                        break;
                }

                column++;
                if (column < columns) continue;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                column = 0;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void HandleDragEvents()
        {
            if (Event.current.type != EventType.MouseDrag || !_dragTarget)
                return;

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { _dragTarget };
            DragAndDrop.StartDrag(_dragTarget.name);
            GUIUtility.hotControl = 0;
            _dragTarget = null;
            Event.current.Use();
        }

        private void EnsureStyles()
        {
            if (_iconCellStyle != null) return;
            _iconCellStyle = new GUIStyle(GUI.skin.button)
            {
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(2, 2, 2, 2)
            };
        }
    }
}
