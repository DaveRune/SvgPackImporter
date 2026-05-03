using System.Linq;
using KnightForge.IconImporter.Editor.Utilities;
using KnightForge.IconImporter.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Inspectors
{
    [CustomEditor(typeof(IconPack))]
    public sealed class IconPackInspector : UnityEditor.Editor
    {
        private const int IconCellSize = 64;
        private const int IconCellSpacing = 4;
        private const int BorderWidth = 2;

        private static readonly Color UpdateColor = new(0.25f, 0.65f, 0.25f);
        private static readonly Color DragHighlightColor = new(0.24f, 0.49f, 0.91f, 1f);
        private Object _dragTarget;
        private GUIStyle _iconCellStyle;
        private SerializedProperty _iconColor;
        private SerializedProperty _icons;
        private SerializedProperty _iconSize;
        private SerializedProperty _providers;
        private SerializedProperty _dragAsSprite;
        private SerializedProperty _strokeWidth;
        private double _updateCompleteTime = -1;

        private void OnEnable()
        {
            _dragAsSprite = serializedObject.FindProperty("_dragAsSprite");
            _strokeWidth = serializedObject.FindProperty("_strokeWidth");
            _providers = serializedObject.FindProperty("_providers");
            _iconColor = serializedObject.FindProperty("_iconColor");
            _iconSize = serializedObject.FindProperty("_iconSize");
            _icons = serializedObject.FindProperty("_icons");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            var pack = (IconPack)target;

            EditorGUILayout.LabelField("Icon Pack Configuration", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Providers", EditorStyles.boldLabel);
            DrawProvidersList();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Conversion Settings", EditorStyles.boldLabel);
            EditorGUILayout.IntSlider(_iconSize, 16, 256, new GUIContent("Icon Size (px)"));
            EditorGUILayout.Slider(_strokeWidth, 0.5f, 3.0f, new GUIContent("Stroke Width"));
            EditorGUILayout.PropertyField(_iconColor, new GUIContent("Icon Color"));

            EditorGUILayout.Space(10);

            var hasProvider = pack.Providers.Any(provider => provider);
            EditorGUI.BeginDisabledGroup(!hasProvider);
            if (GUILayout.Button("Manage Icons", GUILayout.Height(30)))
                IconManagerWindow.ShowWindow(pack);

            GUI.backgroundColor = UpdateColor;
            if (GUILayout.Button("Update", GUILayout.Height(30)))
                IconImportProcessor.StartUpdate(pack, this, () =>
                {
                    _updateCompleteTime = EditorApplication.timeSinceStartup;
                    Repaint();
                });

            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Icons ({_icons.arraySize})", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (pack.Icons.Count > 0)
            {
                EditorGUILayout.LabelField("Drag and drop as:", GUILayout.Width(105));
                var dragMode = _dragAsSprite.boolValue ? 0 : 1;
                dragMode = GUILayout.Toolbar(dragMode, new[] { "Sprite", "Texture2D" }, EditorStyles.miniButton, GUILayout.Width(150));
                _dragAsSprite.boolValue = dragMode == 0;
            }
            EditorGUILayout.EndHorizontal();

            if (!_dragAsSprite.boolValue)
            {
                EditorGUILayout.LabelField($"Be aware. Unity has a bug. Dragging a Texture2D into a Sprite field will assign the first found Sprite.", EditorStyles.helpBox);
                EditorGUILayout.Space(10);
            }
            
            if (_updateCompleteTime > 0 && EditorApplication.timeSinceStartup - _updateCompleteTime < 5.0)
            {
                var successStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = UpdateColor },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField("Update complete.", successStyle);
                Repaint();
            }

            if (pack.Icons.Count > 0)
            {
                DrawIconGrid(pack);
            }

            serializedObject.ApplyModifiedProperties();

            HandleDragEvents();
        }

        private void DrawProvidersList()
        {
            var toRemove = -1;

            for (var i = 0; i < _providers.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(_providers.GetArrayElementAtIndex(i), GUIContent.none);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                    toRemove = i;
                EditorGUILayout.EndHorizontal();
            }

            if (toRemove >= 0)
            {
                // Unity sets object references to null before removing — handle both steps.
                var element = _providers.GetArrayElementAtIndex(toRemove);
                if (element.objectReferenceValue)
                    element.objectReferenceValue = null;
                _providers.DeleteArrayElementAtIndex(toRemove);
            }

            if (GUILayout.Button("Add Provider", GUILayout.Height(22)))
            {
                _providers.InsertArrayElementAtIndex(_providers.arraySize);
                _providers.GetArrayElementAtIndex(_providers.arraySize - 1).objectReferenceValue = null;
            }
        }

        private void DrawIconGrid(IconPack pack)
        {
            const int totalCellSize = IconCellSize + IconCellSpacing;
            var availableWidth = EditorGUIUtility.currentViewWidth - 20f;
            var columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / totalCellSize));

            var column = 0;
            EditorGUILayout.BeginHorizontal();

            foreach (var icon in pack.Icons)
            {
                // Allocate a stable control ID per cell so Unity routes MouseDrag/MouseUp
                // back to this window even after the mouse leaves the inspector panel.
                var id = GUIUtility.GetControlID(FocusType.Passive);
                var variantDisplay = string.IsNullOrEmpty(icon.variant) ? "Root" : icon.variant;
                var tooltip = $"{icon.iconName} ({variantDisplay}) [{icon.provider?.name}]";
                var cellRect = GUILayoutUtility.GetRect(IconCellSize, IconCellSize, _iconCellStyle,
                    GUILayout.Width(IconCellSize), GUILayout.Height(IconCellSize));

                var evt = Event.current;
                var isHover = cellRect.Contains(evt.mousePosition);
                var isActive = GUIUtility.hotControl == id;

                switch (evt.type)
                {
                    case EventType.Repaint:
                        _iconCellStyle.Draw(cellRect, new GUIContent("", tooltip), isHover, isActive, false, false);
                        if (icon.texture)
                            GUI.DrawTexture(cellRect, icon.texture, ScaleMode.ScaleToFit, true);
                        if (_dragTarget == icon.sprite || _dragTarget == icon.texture)
                            DrawBorder(cellRect, DragHighlightColor, BorderWidth);
                        break;

                    case EventType.MouseDown when evt.button == 0 && isHover:
                        GUIUtility.hotControl = id;
                        _dragTarget = _dragAsSprite.boolValue ? icon.sprite : icon.texture;
                        Repaint();
                        evt.Use();
                        break;

                    case EventType.MouseUp when isActive:
                        GUIUtility.hotControl = 0;
                        _dragTarget = null;
                        Repaint();
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
            if (Event.current.type == EventType.MouseDrag && _dragTarget != null)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { _dragTarget };
                DragAndDrop.StartDrag(_dragTarget.name);
                GUIUtility.hotControl = 0;
                _dragTarget = null;
                Event.current.Use();
            }
        }

        private static void DrawBorder(Rect rect, Color color, int width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
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