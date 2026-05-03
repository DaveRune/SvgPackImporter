using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Data
{
    [Icon("Packages/com.knightforge.iconimporter/Editor/Icons/IconImporterSettings.png")]
    public sealed class IconImporterSettings : ScriptableObject
    {
        private const string SettingsAssetPath = "Assets/Resources/IconImporterSettings.asset";
        private static IconImporterSettings _instance;
        public string imageMagickPath = "";
        public bool imageMagickDetected;
        public bool hasCompletedSetup;

        public static IconImporterSettings Instance
        {
            get
            {
                if (_instance) return _instance;
                _instance = AssetDatabase.LoadAssetAtPath<IconImporterSettings>(SettingsAssetPath);

                if (_instance) return _instance;
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                _instance = CreateInstance<IconImporterSettings>();
                AssetDatabase.CreateAsset(_instance, SettingsAssetPath);
                AssetDatabase.SaveAssets();

                return _instance;
            }
        }
    }
}