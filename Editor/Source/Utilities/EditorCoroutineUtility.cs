using System.Collections;

namespace KnightForge.SvgPackImporter.Editor.Utilities
{
    internal static class EditorCoroutineUtility
    {
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner)
        {
            return new EditorCoroutine(routine, owner);
        }
    }
}
