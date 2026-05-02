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
            EditorApplication.delayCall += EnsureProviders;
        }

        private static void CheckFirstTimeSetup()
        {
            if (!IconImporterSettings.Instance.hasCompletedSetup)
                FirstTimeSetupWindow.ShowSetupWindow();
        }

        private static void EnsureProviders()
        {
            Providers.Tabler.TablerIconProvider.EnsureProvider();
        }
    }
}