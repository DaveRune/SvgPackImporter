using UnityEngine;

namespace KnightForge.IconImporter
{
    [CreateAssetMenu(menuName = "Icon Packs/Icon Provider")]
    public class IconProviderSO : ScriptableObject
    {
        public string providerName;
        public string svgRootFolder;
    }
}
