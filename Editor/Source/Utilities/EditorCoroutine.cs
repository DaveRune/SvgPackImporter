using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Editor.Utilities
{
    internal sealed class EditorCoroutine
    {
        private readonly Stack<IEnumerator> _stack;
        private readonly Object _unityOwner;
        private readonly bool _hasUnityOwner;

        public EditorCoroutine(IEnumerator routine, object owner = null)
        {
            _stack = new Stack<IEnumerator>();
            _stack.Push(routine);
            _unityOwner = owner as Object;
            _hasUnityOwner = _unityOwner;
            EditorApplication.update += Update;
        }

        public void Stop()
        {
            EditorApplication.update -= Update;
            _stack.Clear();
        }

        private void Update()
        {
            // Unity-null check (overloaded !): true if the owning Object was destroyed.
            if (_hasUnityOwner && !_unityOwner)
            {
                Stop();
                return;
            }

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
