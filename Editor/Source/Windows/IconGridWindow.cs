using KnightForge.SvgPackImporter.Inspectors;
using UnityEditor;
using UnityEngine;

namespace KnightForge.SvgPackImporter.Windows
{
    internal sealed class IconGridWindow : EditorWindow
    {
        private bool _dragAsSprite = true;
        private IconGridView _grid;
        private IconPack _pack;
        private Vector2 _scroll;

        private void OnEnable()
        {
            _grid = new IconGridView(true);
        }

        private void OnGUI()
        {
            if (!_pack)
            {
                EditorGUILayout.LabelField("IconPack reference lost. Close and reopen from the inspector.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _grid.Draw(_pack, _dragAsSprite, val => _dragAsSprite = val, Repaint);
            if (GUILayout.Button("Manage Icons"))
                IconManagerWindow.ShowWindow(_pack);
            if (GUILayout.Button("Locate Icon Pack"))
            {
                Selection.activeObject = _pack;
                EditorGUIUtility.PingObject(_pack);
            }
            EditorGUILayout.EndScrollView();
        }

        public static void Show(IconPack pack, bool dragAsSprite)
        {
            var window = GetWindow<IconGridWindow>($"Icons ({pack.name})");
            window._pack = pack;
            window._dragAsSprite = dragAsSprite;
            window.minSize = new Vector2(200, 200);
        }
    }
}