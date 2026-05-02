using KnightForge.IconImporter.Editor.Windows;
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
    }
}
