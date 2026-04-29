using UnityEditor;

namespace KnightForge.IconImporter.Editor
{
    public static class IconImporterMenu
    {
        [MenuItem("Tools/Icon Importer/Manage Icon Packs")]
        public static void OpenIconPackManager()
        {
            IconPackManagerWindow.ShowWindow();
        }
    }
}
