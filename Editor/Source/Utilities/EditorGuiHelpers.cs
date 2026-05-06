using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Utilities
{
    /// Shared GUI drawing helpers reused across inspectors and editor windows.
    internal static class EditorGuiHelpers
    {
        internal static void DrawBorder(Rect rect, Color color, int width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }
    }
}
