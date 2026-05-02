using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Utilities
{
    public sealed class EditorCoroutine
    {
        private readonly Stack<IEnumerator> _stack;

        public EditorCoroutine(IEnumerator routine)
        {
            _stack = new Stack<IEnumerator>();
            _stack.Push(routine);
            EditorApplication.update += Update;
        }

        public void Stop()
        {
            EditorApplication.update -= Update;
            _stack.Clear();
        }

        private void Update()
        {
            if (_stack.Count == 0)
            {
                EditorApplication.update -= Update;
                return;
            }

            var top = _stack.Peek();

            if (top.Current is AsyncOperation { isDone: false })
                return;

            if (!top.MoveNext())
            {
                _stack.Pop();
                if (_stack.Count == 0)
                    EditorApplication.update -= Update;
                return;
            }

            if (top.Current is IEnumerator nested)
                _stack.Push(nested);
        }
    }
}