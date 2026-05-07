using KnightForge.SvgPackImporter.Windows;
using UnityEditor;

namespace KnightForge.SvgPackImporter
{
    internal static class SvgPackImporterMenu
    {
        [MenuItem(EditorMenuConstants.MenuRoot + "/Setup")]
        private static void OpenSetupWindow()
        {
            FirstTimeSetupWindow.ShowSetupWindow();
        }
    }
}