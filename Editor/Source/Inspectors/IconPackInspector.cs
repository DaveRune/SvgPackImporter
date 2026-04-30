using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    [CustomEditor(typeof(IconPack))]
    public sealed class IconPackInspector : UnityEditor.Editor
    {
        private SerializedProperty _provider;
        private SerializedProperty _iconSize;
        private SerializedProperty _strokeWidth;
        private SerializedProperty _iconColor;
        private SerializedProperty _icons;

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

            EditorGUI.BeginDisabledGroup(!_provider.objectReferenceValue);
            if (GUILayout.Button("Manage Icons", GUILayout.Height(30)))
                IconImportWindow.ShowWindow((IconPack)target);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_icons, new GUIContent("Packed Icons"), false);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }
    }
}