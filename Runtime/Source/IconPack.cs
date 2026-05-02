using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter
{
    [CreateAssetMenu(menuName = "Icon Packs/Create Icon Pack")]
    public sealed class IconPack : ScriptableObject
    {
        public List<PackedIcon> icons = new();
        public IconProvider provider;

        public float strokeWidth = 2f;
        public Color iconColor = Color.white;
        public int iconSize = 64;

        [HideInInspector] public List<string> activeVariants = new();

        [Serializable]
        public class PackedIcon
        {
            public string iconName;
            public string variant;
            public Texture2D texture;
            public Sprite sprite;
        }
    }
}