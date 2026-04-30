using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public class IconImporterSettings : ScriptableObject
    {
        public string imageMagickPath = "";
        public bool imageMagickDetected = false;
        public bool hasCompletedSetup = false;

        private const string SettingsAssetPath = "Assets/Resources/IconImporterSettings.asset";
        private static IconImporterSettings _instance;

        public static IconImporterSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<IconImporterSettings>(SettingsAssetPath);

                    if (_instance == null)
                    {
                        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                            AssetDatabase.CreateFolder("Assets", "Resources");

                        _instance = CreateInstance<IconImporterSettings>();
                        AssetDatabase.CreateAsset(_instance, SettingsAssetPath);
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }

        public void DetectImageMagick()
        {
            if (ImageMagickConverter.TryDetectImageMagick(out var path))
            {
                imageMagickPath = path;
                imageMagickDetected = true;
            }
            else
            {
                imageMagickPath = "";
                imageMagickDetected = false;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
