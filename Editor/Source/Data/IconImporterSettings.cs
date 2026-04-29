using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public class IconImporterSettings : ScriptableObject
    {
        public string tablerVersion = "latest";
        public string imageMagickPath = "";
        public bool imageMagickDetected = false;

        private const string SettingsAssetPath = "Assets/Package/Editor/Resources/IconImporterSettings.asset";
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
                        _instance = CreateInstance<IconImporterSettings>();
                        _instance.DetectImageMagick();
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
