using UnityEditorInternal;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Data
{
    /// Editor-only project settings persisted in the ProjectSettings folder
    internal sealed class SvgPackImporterSettings : ScriptableObject
    {
        private const string SettingsPath = "ProjectSettings/SvgPackImporterSettings.asset";
        private static SvgPackImporterSettings _instance;

        [SerializeField] private string _imageMagickPath = "";
        [SerializeField] private bool _imageMagickDetected;
        [SerializeField] private bool _hasCompletedSetup;

        public string ImageMagickPath
        {
            get => _imageMagickPath;
            set => _imageMagickPath = value;
        }

        public bool ImageMagickDetected
        {
            get => _imageMagickDetected;
            set => _imageMagickDetected = value;
        }

        public bool HasCompletedSetup
        {
            get => _hasCompletedSetup;
            set => _hasCompletedSetup = value;
        }

        public static SvgPackImporterSettings Instance
        {
            get
            {
                if (_instance) return _instance;

                var loaded = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath);
                if (loaded.Length > 0 && loaded[0] is SvgPackImporterSettings existing)
                {
                    _instance = existing;
                    return _instance;
                }

                _instance = CreateInstance<SvgPackImporterSettings>();
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
