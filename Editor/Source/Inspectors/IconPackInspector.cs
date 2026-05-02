using KnightForge.IconImporter.Editor.Providers;
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
        private int _dragControlId;
        private Sprite _dragTarget;
        private GUIStyle _iconCellStyle;
        private SerializedProperty _iconColor;
        private SerializedProperty _icons;
        private SerializedProperty _iconSize;

        private SerializedProperty _provider;
        private SerializedProperty _strokeWidth;

        private double _updateCompleteTime = -1;

        private void OnEnable()
        {
            _provider = serializedObject.FindProperty("provider");
            _iconSize = serializedObject.FindProperty("iconSize");
            _strokeWidth = serializedObject.FindProperty("strokeWidth");
            _iconColor = serializedObject.FindProperty("iconColor");
            _icons = serializedObject.FindProperty("icons");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            var pack = (IconPack)target;

            EditorGUILayout.LabelField("Icon Pack Configuration", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Provider", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_provider, new GUIContent("Icon Provider"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Conversion Settings", EditorStyles.boldLabel);
            EditorGUILayout.IntSlider(_iconSize, 16, 256, new GUIContent("Icon Size (px)"));
            EditorGUILayout.Slider(_strokeWidth, 0.5f, 3.0f, new GUIContent("Stroke Width"));
            EditorGUILayout.PropertyField(_iconColor, new GUIContent("Icon Color"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Icons", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Imported: {_icons.arraySize}");

            var hasProvider = _provider.objectReferenceValue != null;
            EditorGUI.BeginDisabledGroup(!hasProvider);

            if (GUILayout.Button("Manage Icons", GUILayout.Height(30)))
                IconImportWindow.ShowWindow(pack);

            GUI.backgroundColor = UpdateColor;
            if (GUILayout.Button("Update", GUILayout.Height(30)))
            {
                var provider = IconProviderFactory.Create(pack.provider);
                IconImportProcessor.StartUpdate(pack, provider, this, () =>
                {
                    _updateCompleteTime = EditorApplication.timeSinceStartup;
                    Repaint();
                });
            }

            GUI.backgroundColor = Color.white;

            EditorGUI.EndDisabledGroup();

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

            if (pack.icons.Count > 0)
            {
                EditorGUILayout.Space(10);
                DrawIconGrid(pack);
            }

            serializedObject.ApplyModifiedProperties();

            HandleDragEvents();
        }

        private void DrawIconGrid(IconPack pack)
        {
            var totalCellSize = IconCellSize + IconCellSpacing;
            var availableWidth = EditorGUIUtility.currentViewWidth - 20f;
            var columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / totalCellSize));

            var col = 0;
            EditorGUILayout.BeginHorizontal();

            foreach (var icon in pack.icons)
            {
                // Allocate a stable control ID per cell so Unity routes MouseDrag/MouseUp
                // back to this window even after the mouse leaves the inspector panel.
                var id = GUIUtility.GetControlID(FocusType.Passive);
                var tooltip = $"{icon.iconName} ({icon.variant})";
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
                        if (_dragTarget == icon.sprite)
                            DrawBorder(cellRect, DragHighlightColor, BorderWidth);
                        break;

                    case EventType.MouseDown when evt.button == 0 && isHover:
                        GUIUtility.hotControl = id;
                        _dragControlId = id;
                        _dragTarget = icon.sprite;
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

                col++;
                if (col >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }
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
            if (_iconCellStyle != null)
                return;

            _iconCellStyle = new GUIStyle(GUI.skin.button)
            {
                margin = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(2, 2, 2, 2)
            };
        }
    }
}