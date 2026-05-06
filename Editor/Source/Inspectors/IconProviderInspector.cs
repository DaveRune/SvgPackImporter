using System.IO;
using KnightForge.IconImporter.Providers;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Inspectors
{
    [CustomEditor(typeof(IconProvider), true)]
    public class IconProviderInspector : UnityEditor.Editor
    {
        private static readonly Color OkTextColor = new(0.3f, 0.75f, 0.3f);
        private static readonly Color VariantPrefixColor = new(0.55f, 0.55f, 0.55f);
        private static readonly Color RemoveButtonColor = new(0.75f, 0.15f, 0.15f);

        protected IconManifest manifest;

        private GUIStyle _okStyle;
        private GUIStyle _variantPrefixStyle;

        protected virtual void OnEnable()
        {
            var provider = target as IconProvider;
            if (provider)
                manifest = provider.LoadManifest();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            var provider = (IconProvider)target;

            DrawProviderHeader();
            DrawCoreFields();
            DrawManifestStatus();
            DrawManifestActions(provider);
            DrawAdditionalContent(provider);
            DrawRemoveButton(provider);

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

        protected virtual void DrawAdditionalContent(IconProvider provider)
        {
        }

        protected virtual void DrawManifestActions(IconProvider provider)
        {
            EditorGUILayout.Space(8);

            var label = manifest != null ? "Update Manifest" : "Build Manifest";
            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                manifest = provider.BuildManifest();
                Repaint();
            }
        }

        protected void LoadManifestStatus(IconProvider provider)
        {
            manifest = provider.LoadManifest();
        }

        protected void DrawSvgRootFolder()
        {
            var svgRootProp = serializedObject.FindProperty("svgRootFolder");
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
            var svgRoot = serializedObject.FindProperty("svgRootFolder").stringValue;
            var projectPath = Path.GetDirectoryName(Application.dataPath) ?? "";
            var rootPath = Path.Combine(projectPath, "IconProviders", svgRoot);

            var prefixText = $"{svgRoot}/";
            var prefixWidth = _variantPrefixStyle.CalcSize(new GUIContent(prefixText)).x;

            var toRemove = -1;

            for (var i = 0; i < variantsProp.arraySize; i++)
            {
                var element = variantsProp.GetArrayElementAtIndex(i);
                var variantPath = Path.Combine(rootPath, element.stringValue);
                var exists = !string.IsNullOrEmpty(element.stringValue) && Directory.Exists(variantPath);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(prefixText, _variantPrefixStyle, GUILayout.Width(prefixWidth));
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

            if (manifest != null && manifest.icons != null)
            {
                EditorGUILayout.LabelField($"✓  {manifest.icons.Count:N0} icons  ({manifest.version})", _okStyle);
            }
            else
            {
                EditorGUILayout.LabelField(GetNoFilesMessage(), EditorStyles.helpBox);
            }
        }

        protected virtual string GetNoFilesMessage() => "No icons on disk.";

        protected virtual void DrawRemoveButton(IconProvider provider)
        {
            EditorGUILayout.Space(16);

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = RemoveButtonColor;
            var clicked = GUILayout.Button("Remove", GUILayout.Height(30));
            GUI.backgroundColor = prevColor;

            if (!clicked)
                return;

            var rootPath = provider.GetRootPath();
            if (!EditorUtility.DisplayDialog(
                    "Remove Icon Provider Data",
                    GetRemoveConfirmMessage(rootPath),
                    "Remove",
                    "Cancel"))
                return;

            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);

            manifest = null;
            Repaint();
        }

        protected virtual string GetRemoveConfirmMessage(string rootPath) =>
            $"This will permanently delete all SVG files and supporting data from:\n\n{rootPath}\n\n" +
            "Any previously generated PNG textures will persist, but you will not be able to " +
            "import or update icons from this source until the files are restored.\n\n" +
            "This cannot be undone.";

        private void EnsureStyles()
        {
            if (_okStyle != null) return;

            _okStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = OkTextColor },
                fontStyle = FontStyle.Bold
            };

            _variantPrefixStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = VariantPrefixColor }
            };
        }
    }
}
