using System.IO;
using System.Linq;
using KnightForge.IconImporter.Editor.Utilities;
using KnightForge.IconImporter.Editor.Windows;
using KnightForge.IconImporter.Providers;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Inspectors
{
    [CustomEditor(typeof(IconPack))]
    public sealed class IconPackInspector : UnityEditor.Editor
    {
        private static readonly Color UpdateColor = new(0.25f, 0.65f, 0.25f);
        private static readonly Color MissingSourceColor = new(0.85f, 0.2f, 0.2f, 1f);
        private const int ButtonHeight = 30;

        private readonly IconGridView _grid = new();
        private SerializedProperty _dragAsSprite;
        private SerializedProperty _iconColor;
        private SerializedProperty _iconSize;
        private SerializedProperty _providers;
        private SerializedProperty _strokeWidth;
        private double _updateCompleteTime = -1;

        private void OnEnable()
        {
            _dragAsSprite = serializedObject.FindProperty("_dragAsSprite");
            _strokeWidth = serializedObject.FindProperty("_strokeWidth");
            _providers = serializedObject.FindProperty("_providers");
            _iconColor = serializedObject.FindProperty("_iconColor");
            _iconSize = serializedObject.FindProperty("_iconSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

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

            var hasProvider = pack.Providers.Any();
            EditorGUI.BeginDisabledGroup(!hasProvider);

            if (GUILayout.Button("Manage Icons", GUILayout.Height(ButtonHeight)))
                IconManagerWindow.ShowWindow(pack);

            GUI.backgroundColor = UpdateColor;
            if (GUILayout.Button("Update", GUILayout.Height(ButtonHeight)))
                IconImportProcessor.StartUpdate(pack, this, () =>
                {
                    _updateCompleteTime = EditorApplication.timeSinceStartup;
                    Repaint();
                });
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

            if (pack.Icons.Count > 0)
            {
                EditorGUILayout.Space(10);
                _grid.Draw(pack, _dragAsSprite.boolValue, val =>
                {
                    _dragAsSprite.boolValue = val;
                    serializedObject.ApplyModifiedProperties();
                }, Repaint);

                if (GUILayout.Button("Pop-out Icons", GUILayout.Height(ButtonHeight)))
                {
                    IconGridWindow.Show(pack, _dragAsSprite.boolValue);
                }
                
                var hasMissingSvg = pack.Icons.Any(i =>
                {
                    var path = i.provider?.GetSvgPath(i.iconName, i.variant);
                    return !string.IsNullOrEmpty(path) && !File.Exists(path);
                });
                if (hasMissingSvg)
                {
                    var missingStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = MissingSourceColor },
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Missing some source files.", missingStyle);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProvidersList()
        {
            var toRemove = -1;

            for (var i = 0; i < _providers.arraySize; i++)
            {
                var providerProp = _providers.GetArrayElementAtIndex(i);
                var provider = providerProp.objectReferenceValue as IconProvider;
                var supportsStroke = provider && provider.SupportsStroke;

                var strokeStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { textColor = supportsStroke ? new Color(0.4f, 1f, 0.4f) : new Color(0.65f, 0.65f, 0.65f) },
                    alignment = TextAnchor.MiddleCenter
                };

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(providerProp, GUIContent.none);
                if (provider)
                {
                    var strokeText = "Stroke " + (supportsStroke ? "✓" : "✗");
                    GUILayout.Label(strokeText, strokeStyle, GUILayout.Width(60));
                }
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
    }
}
