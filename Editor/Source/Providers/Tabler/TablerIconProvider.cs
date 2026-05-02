using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KnightForge.IconImporter.Editor.Providers.Tabler
{
    public sealed class TablerIconProvider : IIconProvider
    {
        private static readonly string[] Variants = { "outline", "filled" };
        private readonly string _tablerRoot;

        public TablerIconProvider(string svgRootFolder = "~TablerIcons")
        {
            _tablerRoot = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, svgRootFolder);
        }

        public IReadOnlyList<string> AvailableVariants => Variants;

        public IconManifest LoadManifest()
        {
            var manifestPath = Path.Combine(_tablerRoot, "manifest.json");
            return !File.Exists(manifestPath)
                ? null
                : JsonUtility.FromJson<IconManifest>(File.ReadAllText(manifestPath));
        }

        public string GetSvgPath(string iconName, string variant)
        {
            return Path.Combine(_tablerRoot, variant, $"{iconName}.svg");
        }

        public static IconImporter.TablerIconProvider EnsureProvider()
        {
            const string assetPath = "Assets/Resources/IconProviders/Tabler.asset";

            var existing = AssetDatabase.LoadAssetAtPath<IconImporter.TablerIconProvider>(assetPath);
            if (existing)
                return existing;

            var oldSo = AssetDatabase.LoadAssetAtPath<IconProvider>(assetPath);
            if (oldSo)
                AssetDatabase.DeleteAsset(assetPath);

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/IconProviders"))
                AssetDatabase.CreateFolder("Assets/Resources", "IconProviders");

            var so = ScriptableObject.CreateInstance<IconImporter.TablerIconProvider>();
            so.svgRootFolder = "~TablerIcons";
            so.version = "latest";
            AssetDatabase.CreateAsset(so, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created Tabler icon provider asset at '{assetPath}'.");
            return so;
        }
    }
}