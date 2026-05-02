using System;
using System.Collections.Generic;
using KnightForge.IconImporter.Providers;
using UnityEngine;

namespace KnightForge.IconImporter
{
    [CreateAssetMenu(menuName = IconImporterConstants.IconPack, order = -1)]
    public sealed class IconPack : ScriptableObject
    {
        public List<PackedIcon> icons = new();
        public List<IconProvider> providers = new();

        public float strokeWidth = 2f;
        public Color iconColor = Color.white;
        public int iconSize = 64;

        [HideInInspector] public List<string> activeVariants = new();

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