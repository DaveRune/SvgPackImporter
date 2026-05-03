using KnightForge.IconImporter.Editor.Inspectors;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Windows
{
    public sealed class IconGridWindow : EditorWindow
    {
        private IconPack _pack;
        private IconGridView _grid;
        private bool _dragAsSprite = true;
        private Vector2 _scroll;

        public static void Show(IconPack pack, bool dragAsSprite)
        {
            var window = GetWindow<IconGridWindow>($"Icons ({pack.name})");
            window._pack = pack;
            window._dragAsSprite = dragAsSprite;
            window.minSize = new Vector2(200, 200);
        }

        private void OnEnable()
        {
            _grid = new IconGridView();
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
            EditorGUILayout.EndScrollView();
        }
    }
}
