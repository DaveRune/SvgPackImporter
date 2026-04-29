using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    [CustomEditor(typeof(IconPackSO))]
    public class IconPackSOInspector : UnityEditor.Editor
    {
        private SerializedProperty _packName;
        private SerializedProperty _provider;
        private SerializedProperty _variant;
        private SerializedProperty _iconSize;
        private SerializedProperty _strokeWidth;
        private SerializedProperty _iconColor;
        private SerializedProperty _icons;
        private SerializedProperty _selectedIconNames;

        private void OnEnable()
        {
            _packName = serializedObject.FindProperty("packName");
            _provider = serializedObject.FindProperty("provider");
            _variant = serializedObject.FindProperty("variant");
            _iconSize = serializedObject.FindProperty("iconSize");
            _strokeWidth = serializedObject.FindProperty("strokeWidth");
            _iconColor = serializedObject.FindProperty("iconColor");
            _icons = serializedObject.FindProperty("icons");
            _selectedIconNames = serializedObject.FindProperty("selectedIconNames");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Icon Pack Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_packName, new GUIContent("Pack Name"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Provider Info", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_provider, new GUIContent("Provider"));
            EditorGUILayout.PropertyField(_variant, new GUIContent("Variant"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Conversion Settings", EditorStyles.boldLabel);
            EditorGUILayout.IntSlider(_iconSize, 16, 256, new GUIContent("Icon Size (px)"));
            EditorGUILayout.Slider(_strokeWidth, 0.5f, 3.0f, new GUIContent("Stroke Width"));
            EditorGUILayout.PropertyField(_iconColor, new GUIContent("Icon Color"));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Icons", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Imported Icons: {_icons.arraySize}");

            if (GUILayout.Button("Manage Icons", GUILayout.Height(30)))
            {
                OnManageIcons();
            }

            EditorGUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_icons, new GUIContent("Packed Icons"), false);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void OnManageIcons()
        {
            var pack = (IconPackSO)target;
            IconSelectionWindow.ShowWindow(pack);
        }
    }
}
