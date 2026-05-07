using KnightForge.SvgPackImporter.Data;
using KnightForge.SvgPackImporter.Windows;
using UnityEditor;

namespace KnightForge.SvgPackImporter
{
    [InitializeOnLoad]
    internal static class SvgPackImporterStartup
    {
        static SvgPackImporterStartup()
        {
            EditorApplication.delayCall += CheckFirstTimeSetup;
        }

        private static void CheckFirstTimeSetup()
        {
            if (!SvgPackImporterSettings.Instance.HasCompletedSetup)
                FirstTimeSetupWindow.ShowSetupWindow();
        }
    }
}