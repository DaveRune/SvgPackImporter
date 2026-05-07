using KnightForge.SvgPackImporter.Editor.Windows;
using UnityEditor;

namespace KnightForge.SvgPackImporter.Editor
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