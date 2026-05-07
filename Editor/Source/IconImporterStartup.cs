using KnightForge.IconImporter.Editor.Data;
using KnightForge.IconImporter.Editor.Windows;
using UnityEditor;

namespace KnightForge.IconImporter.Editor
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