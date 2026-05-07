using System;
using System.Collections.Generic;
using KnightForge.IconImporter.Providers;
using UnityEngine;

namespace KnightForge.IconImporter
{
    [Icon("Packages/com.knightforge.iconimporter/Editor/Icons/IconPack.png")]
    [CreateAssetMenu(fileName = "Icon Pack", menuName = IconImporterConstants.IconPack, order = -1)]
    public sealed class IconPack : ScriptableObject
    {
        [SerializeField] private List<PackedIcon> _icons = new();
        [SerializeField] private List<IconProvider> _providers = new();

        [SerializeField] private float _strokeWidth = 2f;
        [SerializeField] private Color _iconColor = Color.white;
        [SerializeField] private int _iconSize = 64;

        [HideInInspector] [SerializeField] private List<string> _activeVariants = new();
        [HideInInspector] [SerializeField] private bool _dragAsSprite = true;

        public IReadOnlyList<PackedIcon> Icons => _icons;
        public IReadOnlyList<IconProvider> Providers => _providers;
        public float StrokeWidth => _strokeWidth;
        public Color IconColor => _iconColor;
        public int IconSize => _iconSize;
        public List<string> ActiveVariants => _activeVariants;

        public void ClearIcons() => _icons.Clear();
        public void AddIcon(PackedIcon icon) => _icons.Add(icon);
        public int RemoveIconsWhere(Predicate<PackedIcon> predicate) => _icons.RemoveAll(predicate);

        [Serializable]
        public class PackedIcon
        {
            public string iconName;
            public string variant;
            public IconProvider provider;
            public Texture2D texture;
            public Sprite sprite;
        }
    }
}