using System.Collections;

namespace KnightForge.IconImporter.Editor.Utilities
{
    public static class EditorCoroutineUtility
    {
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object owner)
        {
            return new EditorCoroutine(routine, owner);
        }
    }
}
