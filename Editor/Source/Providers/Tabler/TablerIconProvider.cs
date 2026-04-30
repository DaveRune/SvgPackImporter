using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor
{
    public class TablerIconProvider : IIconProvider
    {
        private static readonly string[] _variants = { "outline", "filled" };
        private readonly string _tablerRoot;

        public string ProviderName => "Tabler";
        public IReadOnlyList<string> AvailableVariants => _variants;

        public TablerIconProvider(string svgRootFolder = "~TablerIcons")
        {
            _tablerRoot = Path.Combine(Path.GetDirectoryName(Application.dataPath), svgRootFolder);
        }

        public IconManifest LoadManifest()
        {
            var manifestPath = Path.Combine(_tablerRoot, "manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            return JsonUtility.FromJson<IconManifest>(File.ReadAllText(manifestPath));
        }

        public string GetSvgPath(string iconName, string variant)
        {
            return Path.Combine(_tablerRoot, variant, $"{iconName}.svg");
        }

        public static TablerIconProviderSO EnsureProviderSO()
        {
            const string assetPath = "Assets/Resources/IconProviders/Tabler.asset";

            var existing = AssetDatabase.LoadAssetAtPath<TablerIconProviderSO>(assetPath);
            if (existing != null)
                return existing;

            var oldSo = AssetDatabase.LoadAssetAtPath<IconProviderSO>(assetPath);
            if (oldSo != null)
                AssetDatabase.DeleteAsset(assetPath);

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/IconProviders"))
                AssetDatabase.CreateFolder("Assets/Resources", "IconProviders");

            var so = ScriptableObject.CreateInstance<TablerIconProviderSO>();
            so.providerName = "Tabler";
            so.svgRootFolder = "~TablerIcons";
            so.version = "latest";
            AssetDatabase.CreateAsset(so, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created Tabler icon provider asset at '{assetPath}'.");
            return so;
        }
    }
}
