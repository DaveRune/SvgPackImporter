using System.IO;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Inspectors
{
    [CustomEditor(typeof(IconProvider), true)]
    public class IconProviderEditor : UnityEditor.Editor
    {
        protected bool _manifestLoaded;
        protected IconManifest _manifest;

        protected virtual void OnEnable()
        {
            _manifestLoaded = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var provider = (IconProvider)target;

            if (!_manifestLoaded)
                LoadManifestStatus(provider);

            DrawProviderHeader();
            DrawCoreFields();
            DrawManifestStatus();
            DrawManifestActions(provider);
            DrawAdditionalContent(provider);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(provider);
        }

        protected virtual void DrawProviderHeader()
        {
            EditorGUILayout.LabelField("Icon Provider", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
        }

        protected virtual void DrawCoreFields()
        {
            DrawSvgRootFolder();
            EditorGUILayout.Space(4);
            DrawVariantsList();
        }

        protected virtual void DrawAdditionalContent(IconProvider provider) { }

        protected virtual void DrawManifestActions(IconProvider provider)
        {
            EditorGUILayout.Space(8);

            var label = _manifest != null ? "Update Manifest" : "Build Manifest";
            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                _manifest = provider.BuildManifest();
                _manifestLoaded = true;
                Repaint();
            }
        }

        protected void LoadManifestStatus(IconProvider provider)
        {
            _manifestLoaded = true;
            _manifest = provider.LoadManifest();
        }

        protected void DrawSvgRootFolder()
        {
            var svgRootProp = serializedObject.FindProperty("_svgRootFolder");
            var projectPath = Path.GetDirectoryName(Application.dataPath) ?? "";
            var fullPath = Path.Combine(projectPath, "IconProviders", svgRootProp.stringValue);
            var exists = Directory.Exists(fullPath);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(svgRootProp, new GUIContent("SVG Root Folder"));
            if (GUILayout.Button(exists ? "Browse" : "Create", GUILayout.Width(60), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                Directory.CreateDirectory(fullPath);
                EditorUtility.RevealInFinder(fullPath);
            }
            EditorGUILayout.EndHorizontal();

            var projectName = Path.GetFileName(projectPath);
            EditorGUILayout.LabelField($"{projectName}/IconProviders/{svgRootProp.stringValue}", EditorStyles.helpBox);
        }

        private void DrawVariantsList()
        {
            EditorGUILayout.LabelField("Variants", EditorStyles.boldLabel);

            var variantsProp = serializedObject.FindProperty("_variants");
            var svgRoot = serializedObject.FindProperty("_svgRootFolder").stringValue;
            var projectPath = Path.GetDirectoryName(Application.dataPath) ?? "";
            var rootPath = Path.Combine(projectPath, "IconProviders", svgRoot);

            var prefixText = $"{svgRoot}/";
            var prefixStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
            };
            var prefixWidth = prefixStyle.CalcSize(new GUIContent(prefixText)).x;

            var toRemove = -1;

            for (var i = 0; i < variantsProp.arraySize; i++)
            {
                var element = variantsProp.GetArrayElementAtIndex(i);
                var variantPath = Path.Combine(rootPath, element.stringValue);
                var exists = !string.IsNullOrEmpty(element.stringValue) && Directory.Exists(variantPath);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(prefixText, prefixStyle, GUILayout.Width(prefixWidth));
                element.stringValue = EditorGUILayout.TextField(element.stringValue);
                if (GUILayout.Button(exists ? "Browse" : "Create", GUILayout.Width(60), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    Directory.CreateDirectory(variantPath);
                    EditorUtility.RevealInFinder(variantPath);
                }
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                    toRemove = i;
                EditorGUILayout.EndHorizontal();
            }

            if (toRemove >= 0)
                variantsProp.DeleteArrayElementAtIndex(toRemove);

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Add Variant", GUILayout.Height(22)))
            {
                variantsProp.InsertArrayElementAtIndex(variantsProp.arraySize);
                variantsProp.GetArrayElementAtIndex(variantsProp.arraySize - 1).stringValue = "";
            }
        }

        private void DrawManifestStatus()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            if (_manifest != null && _manifest.icons != null)
            {
                var okStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.3f, 0.75f, 0.3f) },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField($"✓  {_manifest.icons.Count:N0} icons  ({_manifest.version})", okStyle);
            }
            else
            {
                EditorGUILayout.LabelField("No manifest found. Add SVGs to the variant folders then build.", EditorStyles.helpBox);
            }
        }
    }
}
