using UnityEditor;

namespace KnightForge.IconImporter.Editor
{
    public static class IconImporterMenu
    {
        [MenuItem(IconImporterConstants.MenuRoot + "/Setup")]
        public static void OpenSetupWindow()
        {
            FirstTimeSetupWindow.ShowSetupWindow();
        }
        
        [MenuItem(IconImporterConstants.MenuRoot + "/Manage Icon Packs", priority = 1000 )]
        public static void OpenIconPackManager()
        {
            IconPackManagerWindow.ShowWindow();
        }
    }
}
