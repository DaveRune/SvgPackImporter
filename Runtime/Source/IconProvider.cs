using UnityEngine;

namespace KnightForge.IconImporter
{
    [CreateAssetMenu(menuName = "Icon Packs/Icon Provider")]
    public class IconProvider : ScriptableObject
    {
        public string svgRootFolder;
    }
}