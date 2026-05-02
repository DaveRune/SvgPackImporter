using KnightForge.IconImporter.Editor.Data;
using KnightForge.IconImporter.Editor.Windows;
using UnityEditor;

namespace KnightForge.IconImporter.Editor
{
    [InitializeOnLoad]
    public static class IconImporterStartup
    {
        static IconImporterStartup()
        {
            EditorApplication.delayCall += CheckFirstTimeSetup;
        }

        private static void CheckFirstTimeSetup()
        {
            if (!IconImporterSettings.Instance.hasCompletedSetup)
                FirstTimeSetupWindow.ShowSetupWindow();
        }
    }
}