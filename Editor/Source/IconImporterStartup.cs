using KnightForge.SvgPackImporter.Editor.Data;
using KnightForge.SvgPackImporter.Editor.Windows;
using UnityEditor;

namespace KnightForge.SvgPackImporter.Editor
{
    [InitializeOnLoad]
    internal static class IconImporterStartup
    {
        static IconImporterStartup()
        {
            EditorApplication.delayCall += CheckFirstTimeSetup;
        }

        private static void CheckFirstTimeSetup()
        {
            if (!IconImporterSettings.Instance.HasCompletedSetup)
                FirstTimeSetupWindow.ShowSetupWindow();
        }
    }
}