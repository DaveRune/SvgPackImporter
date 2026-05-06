using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Data
{
    /// Editor-only project settings persisted to the ProjectSettings folder so they do not
    /// ship with player builds and do not force consumers to create an Assets/Resources folder.
    [Icon("Packages/com.knightforge.iconimporter/Editor/Icons/IconImporterSettings.png")]
    internal sealed class IconImporterSettings : ScriptableObject
    {
        private const string SettingsPath = "ProjectSettings/IconImporterSettings.asset";
        private static IconImporterSettings _instance;

        public string imageMagickPath = "";
        public bool imageMagickDetected;
        public bool hasCompletedSetup;

        public static IconImporterSettings Instance
        {
            get
            {
                if (_instance) return _instance;

                var loaded = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath);
                if (loaded.Length > 0 && loaded[0] is IconImporterSettings existing)
                {
                    _instance = existing;
                    return _instance;
                }

                _instance = CreateInstance<IconImporterSettings>();
                return _instance;
            }
        }

        public static void Save()
        {
            if (!_instance) return;
            InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { _instance }, SettingsPath, true);
        }
    }
}
