using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KnightForge.IconImporter
{
    [CreateAssetMenu(menuName = "IconImporter/Icon Providers/Local")]
    public class IconProvider : ScriptableObject
    {
        private const string ProvidersRoot = "IconProviders";

        [SerializeField] protected string _svgRootFolder = "My Local Icons";
        [SerializeField] protected List<string> _variants = new();

        public IReadOnlyList<string> Variants => _variants;

        public string GetSvgPath(string iconName, string variant)
        {
            return string.IsNullOrEmpty(variant)
                ? Path.Combine(GetRootPath(), $"{iconName}.svg")
                : Path.Combine(GetRootPath(), variant, $"{iconName}.svg");
        }

        public string GetRootPath()
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectPath, ProvidersRoot, _svgRootFolder);
        }

        public IconManifest LoadManifest()
        {
            var manifestPath = Path.Combine(GetRootPath(), "manifest.json");
            return !File.Exists(manifestPath)
                ? null
                : JsonUtility.FromJson<IconManifest>(File.ReadAllText(manifestPath));
        }

        public virtual IconManifest BuildManifest(string versionOverride = null)
        {
            var root = GetRootPath();

            var manifest = new IconManifest
            {
                providerName = name,
                version = versionOverride ?? GetManifestVersion(),
                cachedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                icons = new List<IconEntry>()
            };

            var aliasMap = LoadAliases(root);

            if (_variants == null || _variants.Count == 0)
                ScanVariant(root, string.Empty, manifest, aliasMap);
            else
                foreach (var variant in _variants)
                    ScanVariant(root, variant, manifest, aliasMap);

            SaveManifest(root, manifest);
            return manifest;
        }

        protected virtual string GetManifestVersion() => "local";

        protected virtual Dictionary<string, List<string>> LoadAliases(string root) => new();

        private static void ScanVariant(string root, string variant, IconManifest manifest, Dictionary<string, List<string>> aliasMap)
        {
            var variantPath = string.IsNullOrEmpty(variant) ? root : Path.Combine(root, variant);

            if (!Directory.Exists(variantPath))
            {
                if (!string.IsNullOrEmpty(variant))
                    Debug.LogWarning($"Variant folder not found: '{variantPath}'.");
                return;
            }

            foreach (var svgFile in Directory.GetFiles(variantPath, "*.svg"))
            {
                var fileName = Path.GetFileNameWithoutExtension(svgFile);
                manifest.icons.Add(new IconEntry
                {
                    name = fileName,
                    variant = variant,
                    aliases = aliasMap.TryGetValue(fileName, out var aliases)
                        ? new List<string>(aliases)
                        : new List<string>()
                });
            }
        }

        private static void SaveManifest(string root, IconManifest manifest)
        {
            try
            {
                Directory.CreateDirectory(root);
                var manifestPath = Path.Combine(root, "manifest.json");
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                Debug.Log($"Manifest saved to '{manifestPath}' ({manifest.icons.Count} icons).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save manifest: {ex.Message}");
            }
        }
    }
}
