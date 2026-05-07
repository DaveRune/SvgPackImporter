using KnightForge.IconImporter.Editor.Windows;
using UnityEditor;

namespace KnightForge.IconImporter.Editor
{
    internal static class IconImporterMenu
    {
        [MenuItem(EditorMenuConstants.MenuRoot + "/Setup")]
        private static void OpenSetupWindow()
        {
            FirstTimeSetupWindow.ShowSetupWindow();
        }
    }
}