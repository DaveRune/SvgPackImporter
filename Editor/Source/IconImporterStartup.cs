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
            TablerIconProvider.EnsureProviderSO();
        }
    }
}
