using System.Collections.Generic;
using UnityEngine;

namespace KnightForge.IconImporter
{
    [CreateAssetMenu(menuName = "Icon Packs/Create Icon Pack")]
    public class IconPackSO : ScriptableObject
    {
        public string packName;
        public string provider;
        public string variant;
        public int iconSize = 24;
        public float strokeWidth = 2f;
        public Color iconColor = Color.white;

        [System.Serializable]
        public class PackedIcon
        {
            public string iconName;
            public Sprite sprite;
        }

        public List<PackedIcon> icons = new();
        public List<string> selectedIconNames = new();
    }
}
